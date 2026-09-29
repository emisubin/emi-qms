using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Audit;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.ReviewSafe;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.BusinessUnits;

public sealed record BusinessUnitAccessAdministrationProfile(
    string BusinessUnitCode,
    bool MembershipActive,
    bool LocalProfileExists,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    IReadOnlyList<string> Roles,
    bool IsDepartmentHead,
    bool CanManage,
    IReadOnlyList<string> ExplicitRoles,
    bool LocalProfileReady);

public sealed record BusinessUnitAccessAdministrationUnit(
    string Code,
    bool CanManage,
    IReadOnlyList<BusinessUnitAccessAdministrationDepartment> Departments,
    IReadOnlyList<Role> Roles);

public sealed record BusinessUnitAccessAdministrationDepartment(
    Guid DepartmentId,
    string Code,
    string Name,
    string? DefaultRoleCode);

public sealed record BusinessUnitAccessAdministrationUser(
    Guid UserId,
    string AuthProvider,
    string DisplayName,
    string? AccountId,
    string? Email,
    IReadOnlyList<string> Memberships,
    bool IsOverallAdministrator,
    long AccessVersion,
    string? PendingOperationId,
    string? PendingOperationStatus,
    string? PendingFailureCode,
    bool? PendingIsOverallAdministrator,
    IReadOnlyList<BusinessUnitUserAccessProfileRequest> PendingProfiles,
    IReadOnlyList<BusinessUnitAccessAdministrationProfile> Profiles,
    bool ApprovalPending,
    bool PendingOperationStale);

public sealed record BusinessUnitAccessAdministrationSnapshot(
    IReadOnlyList<BusinessUnitAccessAdministrationUser> Users,
    IReadOnlyList<string> AvailableBusinessUnits,
    IReadOnlyList<BusinessUnitAccessAdministrationUnit> BusinessUnits);

public sealed record BusinessUnitUserAccessProfileRequest(
    string BusinessUnitCode,
    Guid? DepartmentId,
    IReadOnlyList<string>? RoleCodes,
    bool IsActive,
    bool IsDepartmentHead,
    bool IsDepartmentHeadConfirmed = false);

public sealed record BusinessUnitUserAccessUpdateRequest(
    Guid OperationId,
    long ExpectedVersion,
    bool IsOverallAdministrator,
    IReadOnlyList<BusinessUnitUserAccessProfileRequest>? Profiles);

public sealed record BusinessUnitUserAccessUpdateResult(
    bool Changed,
    long AccessVersion,
    BusinessUnitAccessAdministrationSnapshot Snapshot);

public sealed partial class BusinessUnitAccessAdministrationStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    BusinessUnitDirectoryMigrationCatalog directoryMigrationCatalog,
    MigrationLedgerInspector businessMigrationLedgerInspector,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions HashJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BusinessUnitAccessAdministrationSnapshot> GetSnapshotAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var directoryState = await ReadDirectoryStateAsync(cancellationToken);
        var businessStates = new Dictionary<string, BusinessState>(StringComparer.Ordinal);
        foreach (var businessUnitCode in directoryState.AvailableBusinessUnits)
        {
            var target = connectionStringProvider.BusinessUnits.GetBusiness(businessUnitCode);
            businessStates.Add(
                businessUnitCode,
                await LocalFor(target).ReadBusinessStateAsync(directoryState.Users, cancellationToken));
        }

        var units = directoryState.AvailableBusinessUnits.Select(code =>
        {
            var business = businessStates[code];
            return new BusinessUnitAccessAdministrationUnit(
                code,
                business.CanManage,
                business.CanManage ? business.Departments : [],
                business.CanManage ? business.Roles : []);
        }).ToList();

        var users = directoryState.Users.Select(user =>
        {
            var identityKey = directoryState.IdentityKeys[user.UserId];
            var identityProfile = directoryState.AvailableBusinessUnits
                .Select(code => businessStates[code].Profiles.GetValueOrDefault(user.UserId))
                .FirstOrDefault(profile => profile is not null && IdentityMatches(identityKey, profile));
            var profiles = directoryState.AvailableBusinessUnits.Select(code =>
            {
                var business = businessStates[code];
                business.Profiles.TryGetValue(user.UserId, out var profile);
                if (profile is not null && !IdentityMatches(identityKey, profile))
                {
                    profile = null;
                }
                var localProfileReady = profile is not null
                    && ApprovalReadinessPolicy.IsReady(
                        profile.IsActive,
                        profile.DepartmentCode,
                        profile.Roles);
                return new BusinessUnitAccessAdministrationProfile(
                    code,
                    user.Memberships.Contains(code, StringComparer.Ordinal),
                    profile is not null,
                    profile?.IsActive == true,
                    profile?.DepartmentId,
                    profile?.DepartmentCode,
                    profile?.DepartmentName,
                    profile?.Roles ?? [],
                    profile?.IsDepartmentHead == true,
                    business.CanManage,
                    profile?.ExplicitRoles ?? [],
                    localProfileReady);
            }).ToList();
            var approvalPending = user.Memberships.Count == 0
                || profiles.Any(profile => profile.MembershipActive && !profile.LocalProfileReady);
            return user with
            {
                DisplayName = identityProfile?.DisplayName ?? user.DisplayName,
                AccountId = identityProfile?.AccountId ?? user.AccountId,
                Email = identityProfile?.Email ?? user.Email,
                Profiles = profiles,
                ApprovalPending = approvalPending
            };
        }).ToList();

        return new BusinessUnitAccessAdministrationSnapshot(users, directoryState.AvailableBusinessUnits, units);
    }

    public async Task<BusinessUnitUserAccessUpdateResult> UpdateAccessAsync(
        Guid targetUserId,
        BusinessUnitUserAccessUpdateRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw Error("user_access_request_invalid", StatusCodes.Status400BadRequest);
        }

        // A dedicated non-pooled session releases this cross-process lock even on cancellation/failure.
        // The namespace differs from Directory functions' transaction locks to avoid self-deadlock.
        var directoryTarget = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        var lockConnectionString = new NpgsqlConnectionStringBuilder(
            connectionStringProvider.GetConnectionString(directoryTarget)) { Pooling = false }.ConnectionString;
        await using var operationLock = new NpgsqlConnection(lockConnectionString);
        await operationLock.OpenAsync(cancellationToken);
        await ValidateDirectoryContractAsync(operationLock, cancellationToken);
        await using (var lockCommand = operationLock.CreateCommand())
        {
            lockCommand.CommandText = "select pg_advisory_lock(hashtextextended('user-access:' || @user_id::text, 0))";
            lockCommand.Parameters.AddWithValue("user_id", targetUserId);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var actorIdentity = await ReadDirectoryIdentityAsync(actorUserId, cancellationToken);
        if (actorIdentity?.IsOverallAdministrator != true)
        {
            throw Error("overall_administrator_required", StatusCodes.Status403Forbidden);
        }

        var normalizedProfiles = NormalizeProfiles(request.Profiles);
        var directoryIdentity = await ReadDirectoryIdentityAsync(targetUserId, cancellationToken)
            ?? throw Error("directory_identity_not_found", StatusCodes.Status404NotFound);
        if (!string.Equals(directoryIdentity.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal))
        {
            throw Error("directory_identity_read_only", StatusCodes.Status400BadRequest);
        }

        var affectedUnits = directoryIdentity.Memberships
            .Concat(normalizedProfiles.Select(profile => profile.BusinessUnitCode))
            .Concat(request.IsOverallAdministrator
                ? connectionStringProvider.BusinessUnits.Businesses.Select(unit => unit.Code)
                : [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => directoryIdentity.Memberships.Contains(code, StringComparer.Ordinal))
            .ThenBy(code => code, StringComparer.Ordinal)
            .ToArray();
        var preparedProfiles = new Dictionary<string, PreparedProfile>(StringComparer.Ordinal);
        foreach (var businessUnitCode in affectedUnits)
        {
            var requested = normalizedProfiles.SingleOrDefault(profile =>
                string.Equals(profile.BusinessUnitCode, businessUnitCode, StringComparison.Ordinal))
                ?? new NormalizedProfile(businessUnitCode, null, [], false, false, false);
            var target = connectionStringProvider.BusinessUnits.GetBusiness(businessUnitCode);
            preparedProfiles.Add(
                businessUnitCode,
                await LocalFor(target).PrepareProfileAsync(requested, request.IsOverallAdministrator, cancellationToken));
        }

        var effectiveProfiles = preparedProfiles
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value)
            .ToArray();
        var desiredMemberships = effectiveProfiles
            .Where(profile => profile.IsActive)
            .Select(profile => profile.BusinessUnitCode)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        var requestPayload = ComputeRequestPayload(effectiveProfiles);
        var requestHash = ComputeRequestHash(targetUserId, requestPayload, request.IsOverallAdministrator);
        var begin = await BeginOperationAsync(
            request.OperationId,
            targetUserId,
            desiredMemberships,
            actorUserId,
            request.ExpectedVersion,
            requestHash,
            requestPayload,
            request.IsOverallAdministrator,
            cancellationToken);
        if (string.Equals(begin.Status, "Completed", StringComparison.Ordinal))
        {
            return new BusinessUnitUserAccessUpdateResult(
                false,
                begin.CurrentVersion,
                await GetSnapshotAsync(actorUserId, cancellationToken));
        }

        try
        {
            foreach (var businessUnitCode in affectedUnits)
            {
                var target = connectionStringProvider.BusinessUnits.GetBusiness(businessUnitCode);
                await LocalFor(target).ApplyLocalProfileAsync(
                    directoryIdentity,
                    preparedProfiles[businessUnitCode],
                    actorUserId,
                    request.OperationId,
                    cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkRetryRequiredAsync(request.OperationId, actorUserId, "local_profile_commit_failed", CancellationToken.None);
            throw Error("user_access_retry_required", StatusCodes.Status503ServiceUnavailable, exception, request.OperationId);
        }

        PublishResult publish;
        try
        {
            publish = await PublishOperationAsync(request.OperationId, actorUserId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            try
            {
                await MarkRetryRequiredAsync(
                    request.OperationId,
                    actorUserId,
                    "directory_publish_failed",
                    CancellationToken.None);
            }
            catch
            {
                // A directory outage can also prevent the retry marker. The existing Preparing
                // operation remains durable and becomes retryable through the stale-operation gate.
            }
            throw Error("user_access_retry_required", StatusCodes.Status503ServiceUnavailable, exception, request.OperationId);
        }
        return new BusinessUnitUserAccessUpdateResult(
            publish.Changed,
            publish.CurrentVersion,
            await GetSnapshotAsync(actorUserId, cancellationToken));
    }

    private static IReadOnlyList<NormalizedProfile> NormalizeProfiles(
        IReadOnlyList<BusinessUnitUserAccessProfileRequest>? profiles)
    {
        var normalized = (profiles ?? [])
            .Select(profile => new NormalizedProfile(
                (profile.BusinessUnitCode ?? string.Empty).Trim().ToUpperInvariant(),
                profile.DepartmentId,
                (profile.RoleCodes ?? [])
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Select(code => code.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray(),
                profile.IsActive,
                profile.IsDepartmentHead,
                profile.IsDepartmentHeadConfirmed))
            .OrderBy(profile => profile.BusinessUnitCode, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0
            || normalized.Select(profile => profile.BusinessUnitCode).Distinct(StringComparer.Ordinal).Count() != normalized.Length
            || normalized.Any(profile => !BusinessUnitCodes.IsKnown(profile.BusinessUnitCode)))
        {
            throw Error("business_unit_unknown", StatusCodes.Status400BadRequest);
        }

        foreach (var profile in normalized.Where(profile => profile.IsActive))
        {
            if (profile.DepartmentId is null || profile.RoleCodes.Count == 0)
            {
                throw Error("active_profile_incomplete", StatusCodes.Status400BadRequest);
            }
        }

        return normalized;
    }

    private async Task<DirectoryState> ReadDirectoryStateAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDirectoryDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateDirectoryContractAsync(connection, cancellationToken);
        var available = new List<string>();
        await using (var units = connection.CreateCommand())
        {
            units.CommandText = "select code from directory_business_units where is_active = true order by code;";
            await using var reader = await units.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) available.Add(reader.GetString(0));
        }

        var users = new List<BusinessUnitAccessAdministrationUser>();
        var identityKeys = new Dictionary<Guid, DirectoryIdentityKey>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select identity_row.user_id, identity_row.auth_provider,
                       coalesce(nullif(btrim(identity_row.display_name), ''),
                           case when identity_row.auth_provider = 'EntraId'
                               then 'Microsoft 365 사용자 (정보 확인 필요)'
                               else identity_row.external_subject end),
                       identity_row.email, identity_row.access_version,
                       coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code)
                           filter (where membership.is_active = true and business_unit.is_active = true), array[]::text[]),
                       exists (select 1 from directory_overall_administrators administrator
                           where administrator.user_id = identity_row.user_id and administrator.is_active = true),
                       operation.operation_id, operation.status, operation.failure_code,
                       operation.requested_profiles::text,
                       operation.requested_is_overall_administrator,
                       identity_row.external_subject,
                       operation.updated_at_utc
                from directory_identities identity_row
                left join directory_business_unit_memberships membership on membership.user_id = identity_row.user_id
                left join directory_business_units business_unit on business_unit.code = membership.business_unit_code
                left join lateral (
                    select operation_row.operation_id, operation_row.status, operation_row.failure_code,
                           operation_row.requested_profiles, operation_row.requested_is_overall_administrator,
                           operation_row.updated_at_utc
                    from directory_user_access_operations operation_row
                    where operation_row.target_user_id = identity_row.user_id
                      and operation_row.status in ('Preparing', 'RetryRequired')
                    order by operation_row.created_at_utc desc limit 1) operation on true
                where identity_row.is_active = true
                group by identity_row.user_id, identity_row.auth_provider, identity_row.external_subject,
                         identity_row.display_name, identity_row.email, identity_row.access_version,
                         operation.operation_id, operation.status, operation.failure_code,
                         operation.requested_profiles, operation.requested_is_overall_administrator,
                         operation.updated_at_utc
                order by 3, identity_row.email, identity_row.user_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var userId = reader.GetGuid(0);
                identityKeys.Add(userId, new DirectoryIdentityKey(reader.GetString(1), reader.GetString(12)));
                users.Add(new BusinessUnitAccessAdministrationUser(
                    userId, reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetFieldValue<string[]>(5),
                    reader.GetBoolean(6), reader.GetInt64(4),
                    reader.IsDBNull(7) ? null : reader.GetGuid(7).ToString("D"),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(11) ? null : reader.GetBoolean(11),
                    reader.IsDBNull(10)
                        ? []
                        : JsonSerializer.Deserialize<IReadOnlyList<BusinessUnitUserAccessProfileRequest>>(
                            reader.GetString(10), HashJsonOptions) ?? [],
                    [],
                    false,
                    !reader.IsDBNull(13)
                        && reader.GetFieldValue<DateTimeOffset>(13)
                            <= timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(2))));
            }
        }
        return new DirectoryState(users, available, identityKeys);
    }

    private static bool IdentityMatches(DirectoryIdentityKey identity, LocalProfile profile) =>
        string.Equals(identity.AuthProvider, profile.AuthProvider, StringComparison.Ordinal)
        && string.Equals(
            identity.ExternalSubject,
            string.Equals(identity.AuthProvider, QmsAuthProviders.EntraId, StringComparison.Ordinal)
                ? profile.EntraObjectId
                : profile.AccountId,
            StringComparison.Ordinal);

    private async Task<DirectoryIdentity?> ReadDirectoryIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDirectoryDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateDirectoryContractAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select identity_row.user_id, identity_row.auth_provider, identity_row.external_subject,
                   coalesce(nullif(btrim(identity_row.display_name), ''), 'Microsoft 365 사용자'),
                   identity_row.email, identity_row.access_version,
                   coalesce(array_agg(membership.business_unit_code order by membership.business_unit_code)
                       filter (where membership.is_active = true and business_unit.is_active = true), array[]::text[]),
                   exists (select 1 from directory_overall_administrators administrator
                       where administrator.user_id = identity_row.user_id and administrator.is_active = true)
            from directory_identities identity_row
            left join directory_business_unit_memberships membership on membership.user_id = identity_row.user_id
            left join directory_business_units business_unit on business_unit.code = membership.business_unit_code
            where identity_row.user_id = @user_id and identity_row.is_active = true
            group by identity_row.user_id, identity_row.auth_provider, identity_row.external_subject,
                     identity_row.display_name, identity_row.email, identity_row.access_version;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new DirectoryIdentity(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt64(5), reader.GetFieldValue<string[]>(6),
                reader.GetBoolean(7))
            : null;
    }

    private async Task<BeginResult> BeginOperationAsync(
        Guid operationId, Guid targetUserId, IReadOnlyList<string> memberships, Guid actorUserId,
        long expectedVersion, string requestHash, string requestPayload, bool isOverallAdministrator,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDirectoryDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await ValidateDirectoryContractAsync(connection, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select operation_status, current_version
                from begin_directory_user_access_operation(
                    @operation_id, @target_user_id, @memberships, @actor_user_id, @expected_version,
                    @request_hash, @requested_profiles, @is_overall_administrator);
                """;
            command.Parameters.AddWithValue("operation_id", operationId);
            command.Parameters.AddWithValue("target_user_id", targetUserId);
            command.Parameters.AddWithValue("memberships", NpgsqlDbType.Array | NpgsqlDbType.Text, memberships.ToArray());
            command.Parameters.AddWithValue("actor_user_id", actorUserId);
            command.Parameters.AddWithValue("expected_version", expectedVersion);
            command.Parameters.AddWithValue("request_hash", requestHash);
            command.Parameters.AddWithValue("requested_profiles", NpgsqlDbType.Jsonb, requestPayload);
            command.Parameters.AddWithValue("is_overall_administrator", isOverallAdministrator);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Directory operation did not return a state.");
            return new BeginResult(reader.GetString(0), reader.GetInt64(1));
        }
        catch (PostgresException exception)
        {
            throw MapDirectoryException(exception, operationId);
        }
    }

    private async Task MarkRetryRequiredAsync(
        Guid operationId, Guid actorUserId, string failureCode, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDirectoryDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "select mark_directory_user_access_retry_required(@operation_id, @actor_user_id, @failure_code);";
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.AddWithValue("failure_code", failureCode);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<PublishResult> PublishOperationAsync(Guid operationId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDirectoryDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "select changed, current_version from publish_directory_user_access_operation(@operation_id, @actor_user_id);";
            command.Parameters.AddWithValue("operation_id", operationId);
            command.Parameters.AddWithValue("actor_user_id", actorUserId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Directory publish did not return a state.");
            return new PublishResult(reader.GetBoolean(0), reader.GetInt64(1));
        }
        catch (PostgresException exception)
        {
            throw MapDirectoryException(exception, operationId);
        }
    }

    private NpgsqlDataSource CreateDirectoryDataSource()
    {
        var directory = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        return NpgsqlDataSource.Create(connectionStringProvider.GetConnectionString(directory, BusinessUnitConnectionPurpose.Runtime));
    }

    private ILocalUserAdministration LocalFor(BusinessUnitDatabaseTarget target)
    {
        var scope = new BusinessDatabaseScope();
        scope.Bind(target);
        return new LocalUserAdministration(new BusinessDatabase(connectionStringProvider, scope),
            businessMigrationLedgerInspector, timeProvider);
    }

    private async Task ValidateDirectoryContractAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var directory = connectionStringProvider.BusinessUnits.Directory
            ?? throw new BusinessUnitContextUnavailableException("directory_not_configured");
        var ledger = await directoryMigrationCatalog.InspectAsync(connection, cancellationToken);
        if (!ledger.MigrationLedgerReady
            || !await BusinessUnitDatabaseIdentity.IsExpectedAsync(connection, directory, cancellationToken))
            throw new BusinessUnitContextUnavailableException("directory_database_contract_mismatch");
    }

    private static string ComputeRequestPayload(IReadOnlyList<PreparedProfile> profiles) =>
        JsonSerializer.Serialize(
            profiles.Select(profile => new BusinessUnitUserAccessProfileRequest(
                profile.BusinessUnitCode,
                profile.DepartmentId,
                profile.RoleCodes,
                profile.IsActive,
                profile.IsDepartmentHead,
                profile.IsDepartmentHeadConfirmed)),
            HashJsonOptions);

    private static string ComputeRequestHash(
        Guid targetUserId,
        string requestPayload,
        bool isOverallAdministrator)
    {
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{targetUserId:D}:{isOverallAdministrator}:{requestPayload}"))).ToLowerInvariant();
    }

    private static BusinessUnitAccessAdministrationException MapDirectoryException(PostgresException exception, Guid operationId) =>
        exception.MessageText switch
        {
            "overall_administrator_required" => Error("overall_administrator_required", StatusCodes.Status403Forbidden, exception),
            "directory_identity_not_found" => Error("directory_identity_not_found", StatusCodes.Status404NotFound, exception),
            "business_unit_unknown" or "user_access_request_invalid" or "user_access_idempotency_mismatch" =>
                Error(exception.MessageText, StatusCodes.Status400BadRequest, exception, operationId),
            "ordinary_user_multiple_memberships_forbidden" or "user_access_version_conflict" or "user_access_operation_in_progress"
                or "last_overall_administrator" =>
                Error(exception.MessageText, StatusCodes.Status409Conflict, exception, operationId),
            _ => Error("user_access_update_failed", StatusCodes.Status503ServiceUnavailable, exception, operationId)
        };

    private static BusinessUnitAccessAdministrationException Error(
        string errorCode, int statusCode, Exception? innerException = null, Guid? operationId = null) =>
        new(errorCode, statusCode, operationId, innerException);

    private sealed record DirectoryState(
        IReadOnlyList<BusinessUnitAccessAdministrationUser> Users,
        IReadOnlyList<string> AvailableBusinessUnits,
        IReadOnlyDictionary<Guid, DirectoryIdentityKey> IdentityKeys);
    private sealed record DirectoryIdentityKey(string AuthProvider, string ExternalSubject);
    private sealed record DirectoryIdentity(
        Guid UserId, string AuthProvider, string ExternalSubject, string DisplayName,
        string? Email, long AccessVersion, IReadOnlyList<string> Memberships, bool IsOverallAdministrator);
    private sealed record LocalProfile(
        string AuthProvider, string? EntraObjectId, bool IsActive, Guid? DepartmentId,
        string? DepartmentCode, string? DepartmentName, IReadOnlyList<string> Roles, bool IsDepartmentHead,
        string DisplayName, string? Email, string? AccountId, IReadOnlyList<string> ExplicitRoles);
    private sealed record ExistingLocalProfile(
        string AuthProvider, string? EntraObjectId, Guid? DepartmentId,
        IReadOnlyList<ExistingRoleAssignment> RoleAssignments);
    private sealed record ExistingRoleAssignment(string RoleCode, string AssignmentSource);
    private sealed record BusinessState(
        bool CanManage, IReadOnlyList<BusinessUnitAccessAdministrationDepartment> Departments, IReadOnlyList<Role> Roles,
        IReadOnlyDictionary<Guid, LocalProfile> Profiles);
    private sealed record NormalizedProfile(
        string BusinessUnitCode, Guid? DepartmentId, IReadOnlyList<string> RoleCodes,
        bool IsActive, bool IsDepartmentHead, bool IsDepartmentHeadConfirmed);
    private sealed record PreparedProfile(
        string BusinessUnitCode, Guid? DepartmentId, string? DepartmentCode, IReadOnlyList<string> RoleCodes,
        bool IsActive, bool IsDepartmentHead, bool IsDepartmentHeadConfirmed,
        string? DefaultRoleCode, bool IsOverallAdministrator);
    private sealed record BeginResult(string Status, long CurrentVersion);
    private sealed record PublishResult(bool Changed, long CurrentVersion);
}

public sealed class BusinessUnitAccessAdministrationException(
    string errorCode,
    int statusCode,
    Guid? operationId = null,
    Exception? innerException = null)
    : InvalidOperationException(errorCode, innerException)
{
    public string ErrorCode { get; } = errorCode;
    public int StatusCode { get; } = statusCode;
    public Guid? OperationId { get; } = operationId;
}
