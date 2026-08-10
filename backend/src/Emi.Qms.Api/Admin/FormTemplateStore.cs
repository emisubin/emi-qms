using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Admin;

public sealed class FormTemplateStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    private static readonly TemplateDescriptor[] Catalog =
    [
        new("IqcReport", "MATERIAL_IQC", "자재 수입검사", "Quality"),
        new("PanelQualityStage", "LQC", "Item별 LQC 검사", "Quality"),
        new("PanelQualityStage", "OQC", "OQC 자체검수", "Quality")
    ];

    public async Task<FormTemplateScopeResponse> GetScopeAsync(Guid userId, bool isSystemAdministrator, CancellationToken token)
    {
        if (isSystemAdministrator) return new(true, true, ["Quality", "Manufacturing", "ProductionPlanning"]);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var domains = await ReadDomainsAsync(connection, null, userId, token);
        return new(domains.Count > 0, false, domains.ToArray());
    }

    public async Task<FormTemplateCatalogResponse> GetCatalogAsync(Guid userId, bool isSystemAdministrator, CancellationToken token)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        var domains = isSystemAdministrator ? new HashSet<string>(["Quality", "Manufacturing", "ProductionPlanning"]) : await ReadDomainsAsync(connection, null, userId, token);
        if (domains.Count == 0) throw new FormTemplateForbiddenException();
        var items = new List<FormTemplateCatalogItemResponse>();
        foreach (var descriptor in Catalog.Where(item => domains.Contains(item.Domain)))
        {
            var summary = await ReadSummaryAsync(connection, descriptor, token);
            items.Add(new(
                descriptor.Family,
                descriptor.Key,
                descriptor.Name,
                descriptor.Domain,
                summary.ActiveVersion,
                summary.ActivatedAt,
                summary.DraftCount));
        }
        return new(items);
    }

    public async Task<LqcItemTemplatesResponse> GetLqcItemsAsync(
        Guid userId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await DemandAccessAsync(connection, null, userId, isSystemAdministrator, "Quality", token);
        return new(
            true,
            isSystemAdministrator,
            await ReadLqcItemsAsync(connection, null, token));
    }

    public async Task<LqcItemTemplatesResponse> UpdateLqcItemOperatingStatusAsync(
        Guid productTypeId,
        UpdateLqcItemOperatingStatusRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        if (!isSystemAdministrator) throw new FormTemplateForbiddenException();
        if (productTypeId == Guid.Empty || request.ExpectedRowVersion < 1)
            throw new ArgumentException("Item과 최신 설정 version을 확인해 주세요.", "productTypeId");

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var current = await LockLqcItemSettingAsync(connection, transaction, productTypeId, token)
            ?? throw new ArgumentException("LQC 설정을 찾을 수 없습니다.", "productTypeId");
        if (current.RowVersion != request.ExpectedRowVersion)
            throw new FormTemplateConflictException("LQC 운영 상태가 변경되었습니다. 새로고침해 주세요.");

        if (current.IsOperational != request.IsOperational)
        {
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update lqc_item_settings
                    set is_operational=@is_operational,
                        row_version=row_version+1,
                        updated_by_user_id=@actor_id,
                        updated_at_utc=now()
                    where product_type_id=@product_type_id
                      and row_version=@expected_row_version;
                    """;
                command.Parameters.AddWithValue("is_operational", request.IsOperational);
                command.Parameters.AddWithValue("actor_id", actorUserId);
                command.Parameters.AddWithValue("product_type_id", productTypeId);
                command.Parameters.AddWithValue("expected_row_version", request.ExpectedRowVersion);
                if (await command.ExecuteNonQueryAsync(token) != 1)
                    throw new FormTemplateConflictException("LQC 운영 상태가 변경되었습니다. 새로고침해 주세요.");
            }

            await AppendLqcSettingAuditAsync(
                connection,
                transaction,
                productTypeId,
                "OperatingStatusChanged",
                actorUserId,
                new { isOperational = current.IsOperational },
                new { isOperational = request.IsOperational },
                token);
        }

        await transaction.CommitAsync(token);
        return new(true, true, await ReadLqcItemsAsync(connection, null, token));
    }

    public async Task<LqcItemTemplatesResponse> SaveLqcItemTemplateAsync(
        Guid productTypeId,
        SaveLqcItemTemplateRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor("PanelQualityStage", "LQC");
        ValidateItems(descriptor, request.Items);
        if (productTypeId == Guid.Empty || request.ExpectedTemplateRowVersion < 1)
            throw new ArgumentException("Item과 최신 양식 version을 확인해 주세요.", "productTypeId");

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, "Quality", token);
        var current = await LockLqcItemSettingAsync(connection, transaction, productTypeId, token)
            ?? throw new ArgumentException("LQC 설정을 찾을 수 없습니다.", "productTypeId");
        if (current.TemplateRowVersion != request.ExpectedTemplateRowVersion)
            throw new FormTemplateConflictException("LQC 검사 양식이 변경되었습니다. 새로고침해 주세요.");

        var existingKeys = await ReadDefinitionKeysAsync(
            connection,
            transaction,
            descriptor,
            current.TemplateVersionId,
            token);
        ValidateDefinitionKeys(request.Items, existingKeys);

        int nextVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select coalesce(max(version_number), 0) + 1
                from panel_quality_template_versions
                where stage_code='LQC' and product_type_id=@product_type_id;
                """;
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            nextVersion = Convert.ToInt32(await command.ExecuteScalarAsync(token));
        }

        var nextVersionId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into panel_quality_template_versions (
                    id,stage_code,product_type_id,version_number,display_name,is_active,
                    lifecycle_status,row_version,created_by_user_id,updated_by_user_id
                )
                select @next_version_id,'LQC',setting.product_type_id,@next_version,
                       product_type.name || ' LQC 검사',false,'Draft',1,@actor_id,@actor_id
                from lqc_item_settings setting
                join production_product_types product_type on product_type.id=setting.product_type_id
                where setting.product_type_id=@product_type_id;
                """;
            command.Parameters.AddWithValue("next_version_id", nextVersionId);
            command.Parameters.AddWithValue("next_version", nextVersion);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            if (await command.ExecuteNonQueryAsync(token) != 1)
                throw new ArgumentException("LQC 설정을 찾을 수 없습니다.", "productTypeId");
        }

        await ReplaceItemsAsync(connection, transaction, descriptor, nextVersionId, request.Items, token);

        await using (var archive = connection.CreateCommand())
        {
            archive.Transaction = transaction;
            archive.CommandText = """
                update panel_quality_template_versions
                set lifecycle_status='Archived',is_active=false,archived_at_utc=now(),
                    row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
                where id=@current_version_id
                  and lifecycle_status='Active'
                  and row_version=@expected_template_row_version;
                """;
            archive.Parameters.AddWithValue("actor_id", actorUserId);
            archive.Parameters.AddWithValue("current_version_id", current.TemplateVersionId);
            archive.Parameters.AddWithValue("expected_template_row_version", request.ExpectedTemplateRowVersion);
            if (await archive.ExecuteNonQueryAsync(token) != 1)
                throw new FormTemplateConflictException("LQC 검사 양식이 변경되었습니다. 새로고침해 주세요.");
        }

        await using (var activate = connection.CreateCommand())
        {
            activate.Transaction = transaction;
            activate.CommandText = """
                update panel_quality_template_versions
                set lifecycle_status='Active',is_active=true,activated_at_utc=now(),
                    row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
                where id=@next_version_id and lifecycle_status='Draft' and row_version=1;
                """;
            activate.Parameters.AddWithValue("actor_id", actorUserId);
            activate.Parameters.AddWithValue("next_version_id", nextVersionId);
            if (await activate.ExecuteNonQueryAsync(token) != 1)
                throw new FormTemplateConflictException("새 LQC 검사 양식을 적용하지 못했습니다.");
        }

        await using (var setting = connection.CreateCommand())
        {
            setting.Transaction = transaction;
            setting.CommandText = """
                update lqc_item_settings
                set current_template_version_id=@next_version_id,
                    row_version=row_version+1,
                    updated_by_user_id=@actor_id,
                    updated_at_utc=now()
                where product_type_id=@product_type_id
                  and row_version=@expected_setting_row_version;
                """;
            setting.Parameters.AddWithValue("next_version_id", nextVersionId);
            setting.Parameters.AddWithValue("actor_id", actorUserId);
            setting.Parameters.AddWithValue("product_type_id", productTypeId);
            setting.Parameters.AddWithValue("expected_setting_row_version", current.RowVersion);
            if (await setting.ExecuteNonQueryAsync(token) != 1)
                throw new FormTemplateConflictException("LQC 설정이 변경되었습니다. 새로고침해 주세요.");
        }

        await AppendLqcSettingAuditAsync(
            connection,
            transaction,
            productTypeId,
            "TemplateChanged",
            actorUserId,
            new { templateVersionNumber = current.TemplateVersionNumber },
            new { templateVersionNumber = nextVersion, itemCount = request.Items.Count },
            token);
        await transaction.CommitAsync(token);
        return new(true, isSystemAdministrator, await ReadLqcItemsAsync(connection, null, token));
    }

    public async Task<FormTemplateVersionsResponse> GetVersionsAsync(
        string family, string templateKey, Guid userId, bool isSystemAdministrator, CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await DemandAccessAsync(connection, null, userId, isSystemAdministrator, descriptor.Domain, token);
        return await ReadVersionsAsync(connection, null, descriptor, token);
    }

    public async Task<FormTemplateVersionsResponse> GetCurrentAsync(
        string family, string templateKey, Guid userId, bool isSystemAdministrator, CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await DemandAccessAsync(connection, null, userId, isSystemAdministrator, descriptor.Domain, token);
        return await ReadCurrentAsync(connection, null, descriptor, token);
    }

    public async Task<FormTemplateVersionsResponse> SaveCurrentAsync(
        string family,
        string templateKey,
        SaveFormTemplateItemsRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        RejectLegacyLqcMutation(descriptor);
        ValidateItems(descriptor, request.Items);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, descriptor.Domain, token);
        var active = await LockActiveAsync(connection, transaction, descriptor, token);
        if (active is null) throw new FormTemplateConflictException("현재 양식을 찾을 수 없습니다.");
        if (active.Value.RowVersion != request.ExpectedRowVersion)
            throw new FormTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요.");

        var existingKeys = await ReadDefinitionKeysAsync(connection, transaction, descriptor, active.Value.Id, token);
        ValidateDefinitionKeys(request.Items, existingKeys);
        await EnsureRemovedDefinitionsAreUnusedAsync(
            connection,
            transaction,
            descriptor,
            existingKeys.Except(request.Items.Where(item => item.DefinitionKey is not null).Select(item => item.DefinitionKey!.Value)).ToArray(),
            token);

        var nextVersion = await NextVersionAsync(connection, transaction, descriptor, token);
        var nextId = Guid.NewGuid();
        await InsertDraftVersionAsync(connection, transaction, descriptor, active.Value.Id, nextId, nextVersion, actorUserId, token);
        await ReplaceItemsAsync(connection, transaction, descriptor, nextId, request.Items, token);
        await ArchiveActiveAsync(connection, transaction, descriptor, actorUserId, token);
        await ActivateDraftAsync(connection, transaction, descriptor, nextId, actorUserId, 1, token);
        await AppendAuditAsync(connection, transaction, "CurrentSaved", descriptor, nextId, null, actorUserId, new { itemCount = request.Items.Count }, token);
        await transaction.CommitAsync(token);
        return await GetCurrentAsync(family, templateKey, actorUserId, isSystemAdministrator, token);
    }

    public async Task<FormTemplateVersionsResponse> CreateDraftAsync(
        string family,
        string templateKey,
        CreateFormTemplateDraftRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        RejectLegacyLqcMutation(descriptor);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, descriptor.Domain, token);
        var active = await LockActiveAsync(connection, transaction, descriptor, token);
        if (active is null) throw new FormTemplateConflictException("활성 양식이 없어 초안을 만들 수 없습니다.");
        if (active.Value.RowVersion != request.ExpectedActiveRowVersion) throw new FormTemplateConflictException("활성 양식이 변경되었습니다. 새로고침해 주세요.");
        var nextVersion = await NextVersionAsync(connection, transaction, descriptor, token);
        var draftId = Guid.NewGuid();
        await InsertDraftAsync(connection, transaction, descriptor, active.Value.Id, draftId, nextVersion, actorUserId, token);
        await AppendAuditAsync(connection, transaction, "DraftCreated", descriptor, draftId, null, actorUserId, new { version = nextVersion }, token);
        await transaction.CommitAsync(token);
        return await GetVersionsAsync(family, templateKey, actorUserId, isSystemAdministrator, token);
    }

    public async Task<FormTemplateVersionsResponse> SaveItemsAsync(
        string family,
        string templateKey,
        Guid versionId,
        SaveFormTemplateItemsRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        RejectLegacyLqcMutation(descriptor);
        ValidateItems(descriptor, request.Items);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, descriptor.Domain, token);
        var version = await LockVersionAsync(connection, transaction, descriptor, versionId, token);
        if (version is null || version.Value.Status != "Draft") throw new FormTemplateConflictException("초안 양식만 수정할 수 있습니다.");
        if (version.Value.RowVersion != request.ExpectedRowVersion) throw new FormTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요.");
        await ReplaceItemsAsync(connection, transaction, descriptor, versionId, request.Items, token);
        await IncrementDraftVersionAsync(connection, transaction, descriptor, versionId, actorUserId, request.ExpectedRowVersion, token);
        await AppendAuditAsync(connection, transaction, "DraftSaved", descriptor, versionId, null, actorUserId, new { itemCount = request.Items.Count }, token);
        await transaction.CommitAsync(token);
        return await GetVersionsAsync(family, templateKey, actorUserId, isSystemAdministrator, token);
    }

    public async Task<FormTemplateVersionsResponse> ActivateAsync(
        string family,
        string templateKey,
        Guid versionId,
        TransitionFormTemplateVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        RejectLegacyLqcMutation(descriptor);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, descriptor.Domain, token);
        var draft = await LockVersionAsync(connection, transaction, descriptor, versionId, token);
        if (draft is null || draft.Value.Status != "Draft") throw new FormTemplateConflictException("활성화할 초안을 찾을 수 없습니다.");
        if (draft.Value.RowVersion != request.ExpectedRowVersion) throw new FormTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요.");
        if (await CountItemsAsync(connection, transaction, descriptor, versionId, token) == 0) throw new ArgumentException("항목을 한 개 이상 등록해 주세요.", "items");
        await ArchiveActiveAsync(connection, transaction, descriptor, actorUserId, token);
        await ActivateDraftAsync(connection, transaction, descriptor, versionId, actorUserId, request.ExpectedRowVersion, token);
        await AppendAuditAsync(connection, transaction, "VersionActivated", descriptor, versionId, null, actorUserId, new { }, token);
        await transaction.CommitAsync(token);
        return await GetVersionsAsync(family, templateKey, actorUserId, isSystemAdministrator, token);
    }

    public async Task<FormTemplateVersionsResponse> CancelAsync(
        string family,
        string templateKey,
        Guid versionId,
        TransitionFormTemplateVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        RejectLegacyLqcMutation(descriptor);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, descriptor.Domain, token);
        var changed = await UpdateDraftStatusAsync(connection, transaction, descriptor, versionId, request.ExpectedRowVersion, actorUserId, "Archived", token);
        if (!changed) throw new FormTemplateConflictException("초안이 변경되었습니다. 새로고침해 주세요.");
        await AppendAuditAsync(connection, transaction, "DraftArchived", descriptor, versionId, null, actorUserId, new { }, token);
        await transaction.CommitAsync(token);
        return await GetVersionsAsync(family, templateKey, actorUserId, isSystemAdministrator, token);
    }

    public async Task<FormTemplateManagersResponse> GetManagersAsync(bool isSystemAdministrator, CancellationToken token)
    {
        if (!isSystemAdministrator) throw new FormTemplateForbiddenException();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        return await ReadManagersAsync(connection, token);
    }

    public async Task<FormTemplateManagersResponse> AssignManagerAsync(
        AssignFormTemplateManagerRequest request, Guid actorUserId, bool isSystemAdministrator, CancellationToken token)
    {
        if (!isSystemAdministrator) throw new FormTemplateForbiddenException();
        var domain = NormalizeDomain(request.Domain);
        var expectedDepartment = domain switch
        {
            "Quality" => "quality",
            "Manufacturing" => "manufacturing",
            "ProductionPlanning" => "production-planning",
            _ => throw new ArgumentException("지원하지 않는 양식 관리 영역입니다.", "domain")
        };
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        Guid departmentId;
        await using (var user = connection.CreateCommand())
        {
            user.Transaction = transaction;
            user.CommandText = """
                select department.id from qms_users actor
                join departments department on department.id=actor.department_id
                where actor.id=@user_id and actor.is_active and department.code=@department_code;
                """;
            user.Parameters.AddWithValue("user_id", request.UserId);
            user.Parameters.AddWithValue("department_code", expectedDepartment);
            var value = await user.ExecuteScalarAsync(token);
            if (value is not Guid id) throw new ArgumentException("선택한 사용자의 부서를 확인해 주세요.", "userId");
            departmentId = id;
        }
        var bindingId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into form_template_manager_bindings (
                    id,user_id,department_id,domain,assigned_by_user_id)
                values (@id,@user_id,@department_id,@domain,@actor_id);
                """;
            command.Parameters.AddWithValue("id", bindingId);
            command.Parameters.AddWithValue("user_id", request.UserId);
            command.Parameters.AddWithValue("department_id", departmentId);
            command.Parameters.AddWithValue("domain", domain);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            try { await command.ExecuteNonQueryAsync(token); }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            { throw new FormTemplateConflictException("이미 지정된 부서 양식 관리자입니다."); }
        }
        await AppendAuditAsync(connection, transaction, "ManagerAssigned", new("Administration", domain, "양식 관리자", "Administration"), null, bindingId, actorUserId, new { userId = request.UserId, domain }, token);
        await transaction.CommitAsync(token);
        return await ReadManagersAsync(connection, token);
    }

    public async Task<FormTemplateManagersResponse> RevokeManagerAsync(Guid bindingId, Guid actorUserId, bool isSystemAdministrator, CancellationToken token)
    {
        if (!isSystemAdministrator) throw new FormTemplateForbiddenException();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        string domain;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update form_template_manager_bindings
                set revoked_by_user_id=@actor_id,revoked_at_utc=now()
                where id=@id and revoked_at_utc is null returning domain;
                """;
            command.Parameters.AddWithValue("id", bindingId);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            var value = await command.ExecuteScalarAsync(token);
            if (value is not string currentDomain) throw new FormTemplateConflictException("이미 해제된 관리자 지정입니다.");
            domain = currentDomain;
        }
        await AppendAuditAsync(connection, transaction, "ManagerRevoked", new("Administration", domain, "양식 관리자", "Administration"), null, bindingId, actorUserId, new { domain }, token);
        await transaction.CommitAsync(token);
        return await ReadManagersAsync(connection, token);
    }

    public async Task RecordExportAsync(
        string family,
        string templateKey,
        Guid actorUserId,
        bool isSystemAdministrator,
        int rowCount,
        CancellationToken token)
    {
        var descriptor = ResolveDescriptor(family, templateKey);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, descriptor.Domain, token);
        await AppendAuditAsync(connection, transaction, "VersionsExported", descriptor, null, null, actorUserId, new { rowCount }, token);
        await transaction.CommitAsync(token);
    }

    private static TemplateDescriptor ResolveDescriptor(string family, string key)
        => Catalog.SingleOrDefault(item => item.Family == family && item.Key == key)
           ?? throw new ArgumentException("지원하지 않는 양식 종류입니다.", "templateKey");

    private static void RejectLegacyLqcMutation(TemplateDescriptor descriptor)
    {
        if (descriptor is { Family: "PanelQualityStage", Key: "LQC" })
            throw new ArgumentException("LQC 검사 항목은 Item별 LQC 관리 화면에서 수정해 주세요.", "templateKey");
    }

    private static async Task<IReadOnlyList<LqcItemTemplateResponse>> ReadLqcItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken token)
    {
        var rows = new List<LqcItemSettingSnapshot>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select product_type.id,
                       product_type.code,
                       product_type.name,
                       setting.is_operational,
                       setting.row_version,
                       version.id,
                       version.version_number,
                       version.row_version
                from production_product_types product_type
                join lqc_item_settings setting on setting.product_type_id=product_type.id
                join panel_quality_template_versions version on version.id=setting.current_template_version_id
                where product_type.is_active
                order by product_type.code;
                """;
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                rows.Add(new(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetBoolean(3),
                    reader.GetInt32(4),
                    reader.GetGuid(5),
                    reader.GetInt32(6),
                    reader.GetInt32(7)));
            }
        }

        var descriptor = ResolveDescriptor("PanelQualityStage", "LQC");
        var result = new List<LqcItemTemplateResponse>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new(
                row.ProductTypeId,
                row.ProductTypeCode,
                row.ProductTypeName,
                row.IsOperational,
                row.RowVersion,
                row.TemplateVersionId,
                row.TemplateVersionNumber,
                row.TemplateRowVersion,
                await ReadItemsAsync(connection, transaction, descriptor, row.TemplateVersionId, token)));
        }
        return result;
    }

    private static async Task<LqcItemSettingSnapshot?> LockLqcItemSettingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid productTypeId,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select setting.product_type_id,
                   product_type.code,
                   product_type.name,
                   setting.is_operational,
                   setting.row_version,
                   version.id,
                   version.version_number,
                   version.row_version
            from lqc_item_settings setting
            join production_product_types product_type on product_type.id=setting.product_type_id
            join panel_quality_template_versions version on version.id=setting.current_template_version_id
            where setting.product_type_id=@product_type_id
              and product_type.is_active
            for update of setting, version;
            """;
        command.Parameters.AddWithValue("product_type_id", productTypeId);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetInt32(4),
                reader.GetGuid(5),
                reader.GetInt32(6),
                reader.GetInt32(7))
            : null;
    }

    private static async Task AppendLqcSettingAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid productTypeId,
        string action,
        Guid actorUserId,
        object oldValue,
        object newValue,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into lqc_item_setting_audit_events (
                product_type_id,action,actor_user_id,old_value,new_value
            )
            values (@product_type_id,@action,@actor_id,@old_value::jsonb,@new_value::jsonb);
            """;
        command.Parameters.AddWithValue("product_type_id", productTypeId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("old_value", JsonSerializer.Serialize(oldValue));
        command.Parameters.AddWithValue("new_value", JsonSerializer.Serialize(newValue));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<HashSet<string>> ReadDomainsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid userId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select distinct binding.domain
            from form_template_manager_bindings binding
            join qms_users actor on actor.id=binding.user_id and actor.department_id=binding.department_id
            where binding.user_id=@user_id and binding.revoked_at_utc is null and actor.is_active;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task DemandAccessAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid userId, bool isAdmin, string domain, CancellationToken token)
    {
        if (isAdmin) return;
        var domains = await ReadDomainsAsync(connection, transaction, userId, token);
        if (!domains.Contains(domain)) throw new FormTemplateForbiddenException();
    }

    private static async Task<(int? ActiveVersion, DateTimeOffset? ActivatedAt, int DraftCount)> ReadSummaryAsync(NpgsqlConnection connection, TemplateDescriptor descriptor, CancellationToken token)
    {
        var source = Source(descriptor);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            select max(version_number) filter (where lifecycle_status='Active'),
                   max(activated_at_utc) filter (where lifecycle_status='Active'),
                   count(*) filter (where lifecycle_status='Draft')::int
            from {source.VersionTable} where {source.KeyColumn}={source.KeyValue};
            """;
        command.Parameters.AddWithValue("key", descriptor.Key);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return (reader.IsDBNull(0) ? null : reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1), reader.GetInt32(2));
    }

    private static async Task<FormTemplateVersionsResponse> ReadVersionsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TemplateDescriptor descriptor, CancellationToken token)
    {
        var source = Source(descriptor);
        var versions = new List<FormTemplateVersionResponse>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            select id,version_number,{source.NameExpression},lifecycle_status,row_version,activated_at_utc,archived_at_utc
            from {source.VersionTable} where {source.KeyColumn}={source.KeyValue} order by version_number desc;
            """;
        command.Parameters.AddWithValue("key", descriptor.Key);
        var rows = new List<(Guid Id, int Number, string Name, string Status, int RowVersion, DateTimeOffset? Activated, DateTimeOffset? Archived)>();
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token)) rows.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        }
        foreach (var row in rows)
        {
            versions.Add(new(row.Id, row.Number, row.Name, row.Status, row.RowVersion, row.Activated, row.Archived, await ReadItemsAsync(connection, transaction, descriptor, row.Id, token)));
        }
        return new(descriptor.Family, descriptor.Key, descriptor.Name, descriptor.Domain, versions);
    }

    private static async Task<FormTemplateVersionsResponse> ReadCurrentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        TemplateDescriptor descriptor,
        CancellationToken token)
    {
        var all = await ReadVersionsAsync(connection, transaction, descriptor, token);
        return all with
        {
            Versions = all.Versions
                .Where(version => version.LifecycleStatus == "Active")
                .Take(1)
                .ToArray()
        };
    }

    private static async Task<IReadOnlyList<FormTemplateItemResponse>> ReadItemsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, TemplateDescriptor descriptor, Guid versionId, CancellationToken token)
    {
        var source = Source(descriptor);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = descriptor.Family == "Manufacturing"
            ? $"select id,item_code,display_order,label,null,'Check',true,false,null,id from {source.ItemTable} where template_version_id=@version_id order by display_order;"
            : $"select id,item_code,display_order,label,guidance,response_type,is_required,requires_photo,max_text_length,definition_key from {source.ItemTable} where template_version_id=@version_id order by display_order;";
        command.Parameters.AddWithValue("version_id", versionId);
        var items = new List<FormTemplateItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.GetGuid(9)));
        return items;
    }

    private static async Task<(Guid Id, int RowVersion)?> LockActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, CancellationToken token)
    {
        var source = Source(descriptor);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = $"select id,row_version from {source.VersionTable} where {source.KeyColumn}={source.KeyValue} and lifecycle_status='Active' for update;";
        command.Parameters.AddWithValue("key", descriptor.Key);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? (reader.GetGuid(0), reader.GetInt32(1)) : null;
    }

    private static async Task<(string Status, int RowVersion)?> LockVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid versionId, CancellationToken token)
    {
        var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = $"select lifecycle_status,row_version from {source.VersionTable} where id=@id and {source.KeyColumn}={source.KeyValue} for update;";
        command.Parameters.AddWithValue("id", versionId); command.Parameters.AddWithValue("key", descriptor.Key);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? (reader.GetString(0), reader.GetInt32(1)) : null;
    }

    private static async Task<int> NextVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, CancellationToken token)
    { var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"select coalesce(max(version_number),0)+1 from {source.VersionTable} where {source.KeyColumn}={source.KeyValue};"; command.Parameters.AddWithValue("key", descriptor.Key); return Convert.ToInt32(await command.ExecuteScalarAsync(token)); }

    private static async Task InsertDraftAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid activeId, Guid draftId, int nextVersion, Guid actor, CancellationToken token)
    {
        var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = descriptor.Family switch
        {
            "IqcReport" => "insert into iqc_report_template_versions(id,template_id,version_number,is_active,lifecycle_status,row_version,created_by_user_id,updated_by_user_id) select @draft_id,template_id,@version,false,'Draft',1,@actor,@actor from iqc_report_template_versions where id=@active_id;",
            "PanelQualityStage" => "insert into panel_quality_template_versions(id,stage_code,version_number,display_name,is_active,lifecycle_status,row_version,created_by_user_id,updated_by_user_id) select @draft_id,stage_code,@version,display_name||' 초안',false,'Draft',1,@actor,@actor from panel_quality_template_versions where id=@active_id;",
            _ => "insert into manufacturing_step_template_versions(id,template_id,version_number,display_name,lifecycle_status,is_active,row_version,created_by_user_id,updated_by_user_id) select @draft_id,template_id,@version,display_name||' 초안','Draft',false,1,@actor,@actor from manufacturing_step_template_versions where id=@active_id;"
        };
        command.Parameters.AddWithValue("draft_id", draftId); command.Parameters.AddWithValue("active_id", activeId); command.Parameters.AddWithValue("version", nextVersion); command.Parameters.AddWithValue("actor", actor); await command.ExecuteNonQueryAsync(token);
        await using var copy = connection.CreateCommand(); copy.Transaction = transaction; copy.CommandText = descriptor.Family == "Manufacturing"
            ? $"insert into {source.ItemTable}(id,template_version_id,item_code,display_order,label) select uuid_generate_v4(),@draft_id,item_code,display_order,label from {source.ItemTable} where template_version_id=@active_id;"
            : $"insert into {source.ItemTable}(id,template_version_id,item_code,display_order,label,guidance,response_type,is_required,requires_photo,max_text_length,definition_key) select uuid_generate_v4(),@draft_id,item_code,display_order,label,guidance,response_type,is_required,requires_photo,max_text_length,definition_key from {source.ItemTable} where template_version_id=@active_id;";
        copy.Parameters.AddWithValue("draft_id", draftId); copy.Parameters.AddWithValue("active_id", activeId); await copy.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertDraftVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TemplateDescriptor descriptor,
        Guid activeId,
        Guid draftId,
        int nextVersion,
        Guid actor,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = descriptor.Family switch
        {
            "IqcReport" => """
                insert into iqc_report_template_versions(
                    id,template_id,version_number,is_active,lifecycle_status,row_version,
                    created_by_user_id,updated_by_user_id)
                select @draft_id,template_id,@version,false,'Draft',1,@actor,@actor
                from iqc_report_template_versions where id=@active_id;
                """,
            "PanelQualityStage" => """
                insert into panel_quality_template_versions(
                    id,stage_code,version_number,display_name,is_active,lifecycle_status,row_version,
                    created_by_user_id,updated_by_user_id)
                select @draft_id,stage_code,@version,display_name,false,'Draft',1,@actor,@actor
                from panel_quality_template_versions where id=@active_id;
                """,
            _ => throw new ArgumentException("현재 양식 저장을 지원하지 않는 종류입니다.", "family")
        };
        command.Parameters.AddWithValue("draft_id", draftId);
        command.Parameters.AddWithValue("active_id", activeId);
        command.Parameters.AddWithValue("version", nextVersion);
        command.Parameters.AddWithValue("actor", actor);
        await command.ExecuteNonQueryAsync(token);
    }

    private static void ValidateItems(TemplateDescriptor descriptor, IReadOnlyList<SaveFormTemplateItemRequest> items)
    {
        var max = descriptor.Family == "Manufacturing" ? 10 : 50;
        if (items.Count < 1 || items.Count > max) throw new ArgumentException($"항목은 1개부터 {max}개까지 등록해 주세요.", "items");
        if (items.Select(x => x.ItemCode.Trim().ToUpperInvariant()).Distinct().Count() != items.Count || items.Select(x => x.DisplayOrder).Distinct().Count() != items.Count) throw new ArgumentException("항목 코드와 순서는 중복될 수 없습니다.", "items");
        foreach (var item in items)
        {
            if (item.DisplayOrder < 1 || item.DisplayOrder > max) throw new ArgumentException("항목 순서를 확인해 주세요.", "items");
            if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Trim().Length > 200) throw new ArgumentException("항목명은 1자부터 200자까지 입력해 주세요.", "items");
            if (descriptor.Family != "Manufacturing" && item.ResponseType is not ("Check" or "Text")) throw new ArgumentException("지원하지 않는 응답 형식입니다.", "items");
            if (item.RequiresPhoto && item.ResponseType != "Check") throw new ArgumentException("사진 필수는 확인형 항목에만 설정할 수 있습니다.", "items");
        }
    }

    private static async Task ReplaceItemsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid versionId, IReadOnlyList<SaveFormTemplateItemRequest> items, CancellationToken token)
    {
        var source = Source(descriptor); await using (var delete = connection.CreateCommand()) { delete.Transaction = transaction; delete.CommandText = $"delete from {source.ItemTable} where template_version_id=@id;"; delete.Parameters.AddWithValue("id", versionId); await delete.ExecuteNonQueryAsync(token); }
        foreach (var item in items.OrderBy(x => x.DisplayOrder))
        {
            await using var insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = descriptor.Family == "Manufacturing"
            ? $"insert into {source.ItemTable}(id,template_version_id,item_code,display_order,label) values(uuid_generate_v4(),@version,@code,@display_order,@label);"
            : $"insert into {source.ItemTable}(id,template_version_id,item_code,display_order,label,guidance,response_type,is_required,requires_photo,max_text_length,definition_key) values(uuid_generate_v4(),@version,@code,@display_order,@label,@guidance,@response_type,@required,@photo,@max_length,@definition_key);";
            insert.Parameters.AddWithValue("version", versionId); insert.Parameters.AddWithValue("code", item.ItemCode.Trim().ToUpperInvariant()); insert.Parameters.AddWithValue("display_order", item.DisplayOrder); insert.Parameters.AddWithValue("label", item.Label.Trim());
            if (descriptor.Family != "Manufacturing") { insert.Parameters.Add("guidance", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(item.Guidance) ? DBNull.Value : item.Guidance.Trim(); insert.Parameters.AddWithValue("response_type", item.ResponseType); insert.Parameters.AddWithValue("required", item.IsRequired); insert.Parameters.AddWithValue("photo", item.RequiresPhoto); insert.Parameters.Add("max_length", NpgsqlDbType.Integer).Value = item.ResponseType == "Text" ? (item.MaxTextLength ?? 1000) : DBNull.Value; insert.Parameters.AddWithValue("definition_key", item.DefinitionKey ?? Guid.NewGuid()); }
            await insert.ExecuteNonQueryAsync(token);
        }
    }

    private static async Task<HashSet<Guid>> ReadDefinitionKeysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TemplateDescriptor descriptor,
        Guid versionId,
        CancellationToken token)
    {
        var source = Source(descriptor);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select definition_key from {source.ItemTable} where template_version_id=@version_id;";
        command.Parameters.AddWithValue("version_id", versionId);
        var result = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(reader.GetGuid(0));
        return result;
    }

    private static void ValidateDefinitionKeys(
        IReadOnlyList<SaveFormTemplateItemRequest> items,
        IReadOnlySet<Guid> existingKeys)
    {
        var submitted = items.Where(item => item.DefinitionKey is not null).Select(item => item.DefinitionKey!.Value).ToArray();
        if (submitted.Distinct().Count() != submitted.Length)
            throw new ArgumentException("검사 항목 고유번호는 중복될 수 없습니다.", "items");
        if (submitted.Any(key => !existingKeys.Contains(key)))
            throw new ArgumentException("검사 항목 고유번호를 임의로 변경할 수 없습니다.", "items");
    }

    private static async Task EnsureRemovedDefinitionsAreUnusedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TemplateDescriptor descriptor,
        Guid[] removedKeys,
        CancellationToken token)
    {
        if (removedKeys.Length == 0 || descriptor.Key == "LQC") return;
        var sourceCode = descriptor.Family == "IqcReport" ? "IQC_PASSED" : "OQC_PASSED";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from production_control_plan_connections
                where source_code=@source_code and source_definition_key=any(@keys)
                union all
                select 1
                from project_production_plan_connections
                where source_code=@source_code and source_definition_key=any(@keys)
            );
            """;
        command.Parameters.AddWithValue("source_code", sourceCode);
        command.Parameters.AddWithValue("keys", removedKeys);
        if (await command.ExecuteScalarAsync(token) is true)
        {
            throw new ArgumentException(
                "생산계획 실적에 연결된 검사 항목은 삭제할 수 없습니다. 연결을 다른 항목으로 바꾼 뒤 다시 시도해 주세요.",
                "items");
        }
    }

    private static async Task IncrementDraftVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid id, Guid actor, int expected, CancellationToken token)
    { var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"update {source.VersionTable} set row_version=row_version+1,updated_by_user_id=@actor,updated_at_utc=now() where id=@id and lifecycle_status='Draft' and row_version=@expected;"; command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("expected", expected); if (await command.ExecuteNonQueryAsync(token) != 1) throw new FormTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요."); }

    private static async Task ArchiveActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid actor, CancellationToken token)
    { var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"update {source.VersionTable} set lifecycle_status='Archived',is_active=false,archived_at_utc=now(),row_version=row_version+1,updated_by_user_id=@actor,updated_at_utc=now() where {source.KeyColumn}={source.KeyValue} and lifecycle_status='Active';"; command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("key", descriptor.Key); await command.ExecuteNonQueryAsync(token); }

    private static async Task ActivateDraftAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid id, Guid actor, int expected, CancellationToken token)
    { var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"update {source.VersionTable} set lifecycle_status='Active',is_active=true,activated_at_utc=now(),archived_at_utc=null,row_version=row_version+1,updated_by_user_id=@actor,updated_at_utc=now() where id=@id and lifecycle_status='Draft' and row_version=@expected;"; command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("expected", expected); if (await command.ExecuteNonQueryAsync(token) != 1) throw new FormTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요."); }

    private static async Task<bool> UpdateDraftStatusAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid id, int expected, Guid actor, string status, CancellationToken token)
    { var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"update {source.VersionTable} set lifecycle_status=@status,is_active=false,archived_at_utc=now(),row_version=row_version+1,updated_by_user_id=@actor,updated_at_utc=now() where id=@id and lifecycle_status='Draft' and row_version=@expected;"; command.Parameters.AddWithValue("status", status); command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("expected", expected); return await command.ExecuteNonQueryAsync(token) == 1; }

    private static async Task<int> CountItemsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TemplateDescriptor descriptor, Guid id, CancellationToken token)
    { var source = Source(descriptor); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"select count(*)::int from {source.ItemTable} where template_version_id=@id;"; command.Parameters.AddWithValue("id", id); return Convert.ToInt32(await command.ExecuteScalarAsync(token)); }

    private static async Task AppendAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string action, TemplateDescriptor descriptor, Guid? versionId, Guid? bindingId, Guid actor, object detail, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "insert into form_template_audit_events(action,domain,family,template_key,version_id,binding_id,actor_user_id,detail) values(@action,@domain,@family,@key,@version,@binding,@actor,@detail::jsonb);"; command.Parameters.AddWithValue("action", action); command.Parameters.AddWithValue("domain", descriptor.Domain); command.Parameters.AddWithValue("family", descriptor.Family); command.Parameters.AddWithValue("key", descriptor.Key); command.Parameters.Add("version", NpgsqlDbType.Uuid).Value = versionId ?? (object)DBNull.Value; command.Parameters.Add("binding", NpgsqlDbType.Uuid).Value = bindingId ?? (object)DBNull.Value; command.Parameters.AddWithValue("actor", actor); command.Parameters.AddWithValue("detail", JsonSerializer.Serialize(detail)); await command.ExecuteNonQueryAsync(token); }

    private static async Task<FormTemplateManagersResponse> ReadManagersAsync(NpgsqlConnection connection, CancellationToken token)
    {
        var bindings = new List<FormTemplateManagerBindingResponse>(); await using (var command = connection.CreateCommand()) { command.CommandText = "select binding.id,user_account.id,user_account.display_name,department.id,department.code,department.name,binding.domain,binding.assigned_at_utc,binding.revoked_at_utc from form_template_manager_bindings binding join qms_users user_account on user_account.id=binding.user_id join departments department on department.id=binding.department_id order by binding.revoked_at_utc nulls first,department.code,user_account.display_name;"; await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) bindings.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8))); }
        var candidates = new List<FormTemplateManagerCandidateResponse>(); await using (var command = connection.CreateCommand()) { command.CommandText = "select user_account.id,user_account.display_name,department.id,department.code,department.name from qms_users user_account join departments department on department.id=user_account.department_id where user_account.is_active and department.code in ('quality','manufacturing','production-planning') order by department.code,user_account.display_name;"; await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) candidates.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4))); }
        return new(bindings, candidates);
    }

    private static string NormalizeDomain(string value) => value is "Quality" or "Manufacturing" or "ProductionPlanning" ? value : throw new ArgumentException("지원하지 않는 양식 관리 영역입니다.", "domain");
    private static FamilySource Source(TemplateDescriptor descriptor) => descriptor.Family switch
    { "IqcReport" => new("iqc_report_template_versions", "iqc_report_template_items", "template_id", "(select id from iqc_report_templates where template_code=@key)", "'자재 수입검사 v' || version_number"), "PanelQualityStage" => new("panel_quality_template_versions", "panel_quality_template_items", "stage_code", "@key and product_type_id is null", "display_name"), _ => new("manufacturing_step_template_versions", "manufacturing_step_template_items", "template_id", "(select id from manufacturing_step_templates where template_code=@key)", "display_name") };

    private NpgsqlDataSource CreateDataSource() { var value = connectionStringProvider.GetConnectionString(); if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("QMS database connection string is not configured."); return NpgsqlDataSource.Create(value); }
    private sealed record TemplateDescriptor(string Family, string Key, string Name, string Domain);
    private sealed record FamilySource(string VersionTable, string ItemTable, string KeyColumn, string KeyValue, string NameExpression);
    private sealed record LqcItemSettingSnapshot(
        Guid ProductTypeId,
        string ProductTypeCode,
        string ProductTypeName,
        bool IsOperational,
        int RowVersion,
        Guid TemplateVersionId,
        int TemplateVersionNumber,
        int TemplateRowVersion);
}

public sealed class FormTemplateForbiddenException : Exception { }
public sealed class FormTemplateConflictException(string message) : Exception(message);
