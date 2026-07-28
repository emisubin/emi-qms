using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emi.Qms.Api.Logistics;
using Emi.Qms.Api.Manufacturing;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.ProductionPlanning;
using Emi.Qms.Api.QualityInspections;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Ul891Sets;

public sealed class Ul891SetStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    internal static async Task CreateInitialStructureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        IReadOnlyList<NormalizedUl891SetSpecInput> specs,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var panelSequence = 0;
        for (var specIndex = 0; specIndex < specs.Count; specIndex++)
        {
            var source = specs[specIndex];
            var specId = Guid.NewGuid();
            var versionId = Guid.NewGuid();

            await ExecuteAsync(connection, transaction, """
                insert into ul891_set_specs (
                    id, project_id, spec_no, name, created_by_user_id, updated_by_user_id
                ) values (@spec_id, @project_id, @spec_no, @name, @actor_id, @actor_id);

                insert into ul891_set_spec_versions (
                    id, spec_id, version_number, status, created_by_user_id
                ) values (@version_id, @spec_id, 1, 'Draft', @actor_id);
                """, token,
                ("spec_id", specId), ("project_id", projectId), ("spec_no", specIndex + 1),
                ("name", source.Name), ("actor_id", actorId), ("version_id", versionId));

            for (var componentIndex = 0; componentIndex < source.ComponentCodes.Count; componentIndex++)
            {
                await ExecuteAsync(connection, transaction, """
                    insert into ul891_set_spec_components (
                        id, spec_version_id, component_code, sort_order
                    ) values (@id, @version_id, @component_code, @sort_order);
                    """, token,
                    ("id", Guid.NewGuid()), ("version_id", versionId),
                    ("component_code", source.ComponentCodes[componentIndex]), ("sort_order", componentIndex + 1));
            }

            for (var instanceNumber = 1; instanceNumber <= source.Quantity; instanceNumber++)
            {
                var instanceId = Guid.NewGuid();
                await ExecuteAsync(connection, transaction, """
                    insert into ul891_set_instances (
                        id, spec_id, instance_number, spec_version_id, created_by_user_id
                    ) values (@id, @spec_id, @instance_number, @version_id, @actor_id);
                    """, token,
                    ("id", instanceId), ("spec_id", specId), ("instance_number", instanceNumber),
                    ("version_id", versionId), ("actor_id", actorId));

                foreach (var componentCode in source.ComponentCodes)
                {
                    panelSequence++;
                    var panelId = Guid.NewGuid();
                    var displayCode = ProjectInputNormalizer.FormatPanelDisplayCode(panelSequence);
                    await ExecuteAsync(connection, transaction, """
                        insert into panel_placeholders (
                            id, project_id, sequence_number, display_code, status,
                            panel_info_completed, qr_eligible, set_instance_id, component_code, updated_at_utc
                        ) values (
                            @id, @project_id, @sequence_number, @display_code, 'Active',
                            false, false, @instance_id, @component_code, now()
                        );
                        """, token,
                        ("id", panelId), ("project_id", projectId), ("sequence_number", panelSequence),
                        ("display_code", displayCode), ("instance_id", instanceId), ("component_code", componentCode));
                }
            }

            await InsertAuditAsync(connection, transaction, projectId, "SetSpec", specId,
                "SetSpecCreated", null, null, source.Name, null, actorId, correlationId, token);
        }
    }

    public async Task<ProjectMutationResult<Ul891SetStructureResponse>> GetStructureAsync(
        Guid projectId,
        bool canEditOrder,
        bool canEditDesign,
        CancellationToken token)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);

        string item;
        string? structureMode;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select item, structure_mode from projects where id=@project_id and deleted_at_utc is null;";
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return ProjectMutationResult<Ul891SetStructureResponse>.NotFound();
            }
            item = reader.GetString(0);
            structureMode = reader.IsDBNull(1) ? null : reader.GetString(1);
        }

        if (structureMode != "Ul891Set")
        {
            return ProjectMutationResult<Ul891SetStructureResponse>.Success(new Ul891SetStructureResponse(
                projectId,
                structureMode ?? "FlatPanel",
                string.Equals(item, "UL891", StringComparison.Ordinal),
                false,
                false,
                [],
                await ReadOrderedProcurementItemsAsync(connection, projectId, token),
                []));
        }

        var specs = await ReadSpecsAsync(connection, projectId, token);
        var orderedItems = await ReadOrderedProcurementItemsAsync(connection, projectId, token);
        var recoveryCases = await ReadRecoveryCasesAsync(connection, projectId, token);
        return ProjectMutationResult<Ul891SetStructureResponse>.Success(new Ul891SetStructureResponse(
            projectId, "Ul891Set", false, canEditOrder, canEditDesign, specs, orderedItems, recoveryCases));
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> AddSpecAsync(
        Guid projectId,
        AddUl891SetSpecRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var name = NormalizeText(request.Name);
        var reason = NormalizeText(request.Reason);
        var codes = request.Components?
            .Select(item => NormalizeText(item.ComponentCode)?.ToUpperInvariant())
            .ToList();
        if (request.OperationId == Guid.Empty || request.ExpectedSpecCount is null or < 1
            || name is null || name.Length > 120 || request.Quantity is null or < 1 or > 999
            || reason is null || codes is null || codes.Count == 0 || codes.Count > 200
            || codes.Any(code => code is null || code.Length > 30)
            || codes.Where(code => code is not null).Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]>
            {
                ["SetSpec"] = ["현재 사양 수, 사양명, 주문 수량, 중복 없는 구성 code, 변경 사유와 operationId를 확인해 주세요."]
            });
        }

        var normalizedCodes = codes.Select(code => code!).ToList();
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트에는 세트 사양을 추가할 수 없습니다.", token);

        var fingerprint = Fingerprint("AddSpec", projectId, request.ExpectedSpecCount, name, request.Quantity, string.Join(",", normalizedCodes), reason);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "AddSpec", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        int currentSpecCount;
        int nextSpecNo;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select count(*)::integer, coalesce(max(spec_no),0)::integer + 1 from ul891_set_specs where project_id=@project_id;";
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            currentSpecCount = reader.GetInt32(0);
            nextSpecNo = reader.GetInt32(1);
        }
        if (currentSpecCount != request.ExpectedSpecCount)
            return await RollbackConflict(transaction, "다른 사용자가 세트 사양을 추가했습니다. 최신 내용을 다시 불러와 주세요.", token);

        var maxPanel = await ReadMaxPanelSequenceAsync(connection, transaction, projectId, token);
        if (maxPanel + request.Quantity.Value * normalizedCodes.Count > ProjectDomainRules.MaxPanelsPerProject)
            return await RollbackValidation(transaction, "Quantity", $"프로젝트의 활성 패널은 최대 {ProjectDomainRules.MaxPanelsPerProject}개까지 만들 수 있습니다.", token);

        var specId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            insert into ul891_set_specs (id,project_id,spec_no,name,created_by_user_id,updated_by_user_id)
            values (@spec_id,@project_id,@spec_no,@name,@actor_id,@actor_id);
            insert into ul891_set_spec_versions (id,spec_id,version_number,status,revision_reason,created_by_user_id)
            values (@version_id,@spec_id,1,'Draft',@reason,@actor_id);
            """, token, ("spec_id", specId), ("project_id", projectId), ("spec_no", nextSpecNo),
            ("name", name), ("actor_id", actorId), ("version_id", versionId), ("reason", reason));

        for (var index = 0; index < normalizedCodes.Count; index++)
        {
            await ExecuteAsync(connection, transaction, """
                insert into ul891_set_spec_components (id,spec_version_id,component_code,sort_order)
                values (@id,@version_id,@code,@sort_order);
                """, token, ("id", Guid.NewGuid()), ("version_id", versionId),
                ("code", normalizedCodes[index]), ("sort_order", index + 1));
        }

        for (var instanceNumber = 1; instanceNumber <= request.Quantity.Value; instanceNumber++)
        {
            var instanceId = Guid.NewGuid();
            await ExecuteAsync(connection, transaction, """
                insert into ul891_set_instances (id,spec_id,instance_number,spec_version_id,created_by_user_id)
                values (@id,@spec_id,@instance_number,@version_id,@actor_id);
                """, token, ("id", instanceId), ("spec_id", specId), ("instance_number", instanceNumber),
                ("version_id", versionId), ("actor_id", actorId));
            await ProductionPlanningStore.EnsureSetPlanScopeAsync(
                connection, transaction, projectId, instanceId, actorId, token);
            foreach (var code in normalizedCodes)
            {
                maxPanel++;
                await InsertSetPanelAsync(connection, transaction, projectId, instanceId, code, maxPanel, token);
            }
        }

        await InsertAuditAsync(connection, transaction, projectId, "SetSpec", specId,
            "SetSpecAdded", null, null, name, reason, actorId, correlationId, token);
        var response = new Ul891MutationResponse(request.OperationId, projectId, "SpecAdded", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "AddSpec", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> UpdateDraftAsync(
        Guid projectId,
        Guid specId,
        Guid versionId,
        UpdateUl891DraftRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var errors = ValidateDraft(request);
        if (errors.Count > 0)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null)
        {
            return await RollbackNotFound(transaction, token);
        }
        if (project.Status == "Completed")
        {
            return await RollbackConflict(transaction, "완료된 프로젝트의 세트 사양은 수정할 수 없습니다.", token);
        }

        var spec = await LockSpecVersionAsync(connection, transaction, projectId, specId, versionId, token);
        if (spec is null)
        {
            return await RollbackNotFound(transaction, token);
        }
        if (spec.VersionStatus != "Draft")
        {
            return await RollbackConflict(transaction, "Draft 사양 버전만 수정할 수 있습니다.", token);
        }
        if (spec.SpecRowVersion != request.ExpectedSpecVersion)
        {
            return await RollbackConflict(transaction, "다른 사용자가 세트 사양을 수정했습니다. 최신 내용을 다시 불러와 주세요.", token);
        }

        await ExecuteAsync(connection, transaction, """
            update ul891_set_specs
            set name=@name, row_version=row_version+1, updated_by_user_id=@actor_id, updated_at_utc=now()
            where id=@spec_id;
            delete from ul891_set_spec_components where spec_version_id=@version_id;
            """, token,
            ("name", request.SpecName!.Trim()), ("actor_id", actorId), ("spec_id", specId), ("version_id", versionId));

        for (var index = 0; index < request.Components!.Count; index++)
        {
            var component = request.Components[index];
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into ul891_set_spec_components (
                    id, spec_version_id, component_code, panel_name, panel_specification,
                    width_mm, height_mm, depth_mm, sort_order
                ) values (
                    @id, @version_id, @code, @panel_name, @panel_specification,
                    @width, @height, @depth, @sort_order
                );
                """;
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("version_id", versionId);
            command.Parameters.AddWithValue("code", component.ComponentCode!.Trim().ToUpperInvariant());
            AddNullableText(command, "panel_name", NormalizeText(component.PanelName));
            AddNullableText(command, "panel_specification", NormalizeText(component.PanelSpecification));
            AddNullableDecimal(command, "width", component.WidthMm);
            AddNullableDecimal(command, "height", component.HeightMm);
            AddNullableDecimal(command, "depth", component.DepthMm);
            command.Parameters.AddWithValue("sort_order", index + 1);
            await command.ExecuteNonQueryAsync(token);
        }

        await SyncDraftPanelsAsync(connection, transaction, projectId, specId, versionId, actorId, correlationId, token);
        await InsertAuditAsync(connection, transaction, projectId, "SetSpecVersion", versionId,
            "SetSpecDraftUpdated", null, null, $"v{spec.VersionNumber}", request.RevisionReason, actorId, correlationId, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(new Ul891MutationResponse(Guid.Empty, projectId, "DraftUpdated", false));
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> PublishAsync(
        Guid projectId,
        Guid specId,
        Guid versionId,
        PublishUl891VersionRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        if (request.OperationId == Guid.Empty)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { [nameof(request.OperationId)] = ["operationId가 필요합니다."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null)
        {
            return await RollbackNotFound(transaction, token);
        }
        var fingerprint = Fingerprint("Publish", projectId, specId, versionId, NormalizeText(request.Reason));
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "Publish", fingerprint, token);
        if (replay is not null)
        {
            await transaction.RollbackAsync(token);
            return replay;
        }
        var spec = await LockSpecVersionAsync(connection, transaction, projectId, specId, versionId, token);
        if (spec is null)
        {
            return await RollbackNotFound(transaction, token);
        }
        if (spec.VersionStatus != "Draft")
        {
            return await RollbackConflict(transaction, "Draft 사양 버전만 확정할 수 있습니다.", token);
        }

        var packagingMethod = project.PackagingMethod;
        await using (var validation = connection.CreateCommand())
        {
            validation.Transaction = transaction;
            validation.CommandText = """
                select count(*)::integer,
                       count(*) filter (where panel_name is not null)::integer,
                       count(*) filter (where width_mm is not null and height_mm is not null and depth_mm is not null)::integer,
                       count(*) filter (where width_mm is null and height_mm is null and depth_mm is null)::integer
                from ul891_set_spec_components where spec_version_id=@version_id;
                """;
            validation.Parameters.AddWithValue("version_id", versionId);
            await using var reader = await validation.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            var total = reader.GetInt32(0);
            var named = reader.GetInt32(1);
            var completeDimensions = reader.GetInt32(2);
            var emptyDimensions = reader.GetInt32(3);
            var invalid = total == 0 || total != named
                || (packagingMethod == "WoodenCrate" ? total != completeDimensions : total != completeDimensions + emptyDimensions);
            if (invalid)
            {
                return await RollbackValidation(transaction, "Components", "모든 구성 패널의 이름과 포장방식에 맞는 치수를 입력해 주세요.", token);
            }
        }

        await ExecuteAsync(connection, transaction, """
            update ul891_set_spec_versions
            set status='Superseded'
            where spec_id=@spec_id and status='Published';
            update ul891_set_spec_versions
            set status='Published', published_by_user_id=@actor_id, published_at_utc=now()
            where id=@version_id and status='Draft';
            """, token, ("spec_id", specId), ("actor_id", actorId), ("version_id", versionId));
        await RefreshPanelInformationAsync(connection, transaction, projectId, specId, versionId, token);
        await InsertAuditAsync(connection, transaction, projectId, "SetSpecVersion", versionId,
            "SetSpecVersionPublished", "Status", "Draft", "Published", request.Reason, actorId, correlationId, token);
        var response = new Ul891MutationResponse(request.OperationId, projectId, "Published", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "Publish", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> CreateVersionAsync(
        Guid projectId,
        Guid specId,
        CreateUl891VersionRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var reason = NormalizeText(request.Reason);
        if (request.OperationId == Guid.Empty || reason is null)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { ["Reason"] = ["새 버전 생성 사유가 필요합니다."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        var fingerprint = Fingerprint("CreateVersion", projectId, specId, reason);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "CreateVersion", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        Guid sourceVersionId;
        int nextVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select published.id, coalesce(maximum.max_version, 0) + 1
                from ul891_set_specs spec
                join ul891_set_spec_versions published on published.spec_id=spec.id and published.status='Published'
                cross join lateral (
                    select max(version_number) max_version from ul891_set_spec_versions where spec_id=spec.id
                ) maximum
                where spec.id=@spec_id and spec.project_id=@project_id
                  and not exists(select 1 from ul891_set_spec_versions where spec_id=spec.id and status='Draft')
                for update of spec;
                """;
            command.Parameters.AddWithValue("spec_id", specId);
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return await RollbackConflict(transaction, "확정된 사양이 없거나 이미 편집 중인 Draft 버전이 있습니다.", token);
            }
            sourceVersionId = reader.GetGuid(0);
            nextVersion = reader.GetInt32(1);
        }
        var newVersionId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            insert into ul891_set_spec_versions (
                id, spec_id, version_number, status, revision_reason, created_by_user_id
            ) values (@new_id, @spec_id, @version_number, 'Draft', @reason, @actor_id);
            insert into ul891_set_spec_components (
                id, spec_version_id, component_code, panel_name, panel_specification,
                width_mm, height_mm, depth_mm, sort_order
            )
            select uuid_generate_v4(), @new_id, component_code, panel_name, panel_specification,
                   width_mm, height_mm, depth_mm, sort_order
            from ul891_set_spec_components where spec_version_id=@source_id;
            """, token, ("new_id", newVersionId), ("spec_id", specId), ("version_number", nextVersion),
            ("reason", reason), ("actor_id", actorId), ("source_id", sourceVersionId));
        await InsertAuditAsync(connection, transaction, projectId, "SetSpecVersion", newVersionId,
            "SetSpecVersionCreated", null, null, $"v{nextVersion}", reason, actorId, correlationId, token);
        var response = new Ul891MutationResponse(request.OperationId, projectId, "DraftVersionCreated", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "CreateVersion", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> IncreaseAsync(
        Guid projectId,
        Guid specId,
        IncreaseUl891InstancesRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var reason = NormalizeText(request.Reason);
        if (request.OperationId == Guid.Empty || request.ExpectedActiveInstanceCount is null || request.Quantity is null or < 1 or > 999 || reason is null)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { ["Quantity"] = ["추가 수량·현재 수량·사유·operationId를 확인해 주세요."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트의 주문 수량은 변경할 수 없습니다.", token);
        var fingerprint = Fingerprint("Increase", projectId, specId, request.ExpectedActiveInstanceCount, request.Quantity, reason);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "Increase", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        Guid versionId;
        int currentCount;
        int maxInstance;
        await using (var lockSpec = connection.CreateCommand())
        {
            lockSpec.Transaction = transaction;
            lockSpec.CommandText = "select id from ul891_set_specs where id=@spec_id and project_id=@project_id for update;";
            lockSpec.Parameters.AddWithValue("spec_id", specId);
            lockSpec.Parameters.AddWithValue("project_id", projectId);
            if (await lockSpec.ExecuteScalarAsync(token) is not Guid)
                return await RollbackNotFound(transaction, token);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select version.id,
                       count(instance.id) filter (where instance.status='Active')::integer,
                       coalesce(max(instance.instance_number),0)::integer
                from ul891_set_specs spec
                join ul891_set_spec_versions version on version.spec_id=spec.id and version.status='Published'
                left join ul891_set_instances instance on instance.spec_id=spec.id
                where spec.id=@spec_id and spec.project_id=@project_id
                group by version.id;
                """;
            command.Parameters.AddWithValue("spec_id", specId);
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return await RollbackConflict(transaction, "확정된 세트 사양이 필요합니다.", token);
            versionId = reader.GetGuid(0);
            currentCount = reader.GetInt32(1);
            maxInstance = reader.GetInt32(2);
        }
        if (currentCount != request.ExpectedActiveInstanceCount) return await RollbackConflict(transaction, "다른 사용자가 주문 수량을 변경했습니다. 최신 내용을 다시 불러와 주세요.", token);

        var components = await ReadComponentCodesAsync(connection, transaction, versionId, token);
        var maxPanel = await ReadMaxPanelSequenceAsync(connection, transaction, projectId, token);
        if (maxPanel + request.Quantity.Value * components.Count > ProjectDomainRules.MaxPanelsPerProject)
        {
            return await RollbackValidation(transaction, "Quantity", $"프로젝트 패널은 최대 {ProjectDomainRules.MaxPanelsPerProject}개까지 생성할 수 있습니다.", token);
        }
        for (var offset = 1; offset <= request.Quantity.Value; offset++)
        {
            var instanceId = Guid.NewGuid();
            await ExecuteAsync(connection, transaction, """
                insert into ul891_set_instances (id, spec_id, instance_number, spec_version_id, created_by_user_id)
                values (@id, @spec_id, @instance_number, @version_id, @actor_id);
                """, token, ("id", instanceId), ("spec_id", specId), ("instance_number", maxInstance + offset),
                ("version_id", versionId), ("actor_id", actorId));
            await ProductionPlanningStore.EnsureSetPlanScopeAsync(
                connection, transaction, projectId, instanceId, actorId, token);
            foreach (var code in components)
            {
                maxPanel++;
                await InsertSetPanelAsync(connection, transaction, projectId, instanceId, code, maxPanel, token);
            }
        }
        await InsertAuditAsync(connection, transaction, projectId, "SetSpec", specId,
            "SetInstancesIncreased", "ActiveInstanceCount", currentCount.ToString(CultureInfo.InvariantCulture),
            (currentCount + request.Quantity.Value).ToString(CultureInfo.InvariantCulture), reason, actorId, correlationId, token);
        var response = new Ul891MutationResponse(request.OperationId, projectId, "InstancesIncreased", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "Increase", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> ApplyVersionAsync(
        Guid projectId,
        Guid specId,
        ApplyUl891VersionRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var reason = NormalizeText(request.Reason);
        var instanceIds = request.InstanceIds?.Distinct().ToList() ?? [];
        if (request.OperationId == Guid.Empty || request.VersionId is null || request.ExpectedActiveInstanceCount is null || instanceIds.Count == 0 || reason is null)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { ["InstanceIds"] = ["적용할 버전·세트 인스턴스·현재 수량·사유를 확인해 주세요."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트의 사양 버전은 변경할 수 없습니다.", token);
        var fingerprint = Fingerprint("ApplyVersion", projectId, specId, request.VersionId, request.ExpectedActiveInstanceCount, string.Join(',', instanceIds.Order()), reason);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "ApplyVersion", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        var activeCount = await ReadActiveInstanceCountAsync(connection, transaction, projectId, specId, token);
        if (activeCount is null) return await RollbackNotFound(transaction, token);
        if (activeCount != request.ExpectedActiveInstanceCount) return await RollbackConflict(transaction, "다른 사용자가 주문 수량을 변경했습니다. 최신 내용을 다시 불러와 주세요.", token);
        var targetCodes = await ReadPublishedComponentCodesAsync(connection, transaction, projectId, specId, request.VersionId.Value, token);
        if (targetCodes is null) return await RollbackValidation(transaction, "VersionId", "확정된 사양 버전을 선택해 주세요.", token);

        var maxPanel = await ReadMaxPanelSequenceAsync(connection, transaction, projectId, token);
        foreach (var instanceId in instanceIds)
        {
            Guid currentVersionId;
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    select instance.spec_version_id
                    from ul891_set_instances instance
                    where instance.id=@instance_id and instance.spec_id=@spec_id and instance.status='Active'
                    for update;
                    """;
                command.Parameters.AddWithValue("instance_id", instanceId);
                command.Parameters.AddWithValue("spec_id", specId);
                var value = await command.ExecuteScalarAsync(token);
                if (value is not Guid parsed) return await RollbackValidation(transaction, "InstanceIds", "선택한 활성 세트 인스턴스를 찾을 수 없습니다.", token);
                currentVersionId = parsed;
            }
            var panels = await ReadInstancePanelsAsync(connection, transaction, instanceId, token);
            var delivered = await InstanceHasDeliveryAsync(connection, transaction, instanceId, token);
            if (delivered) return await RollbackConflict(transaction, "납품된 패널이 포함된 세트 인스턴스에는 새 사양을 적용할 수 없습니다.", token);
            var started = false;
            foreach (var panel in panels) started |= await PanelHasStartedAsync(connection, transaction, panel.PanelId, token);
            var currentCodes = panels.Where(panel => panel.Status == "Active").Select(panel => panel.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var structureChanged = !currentCodes.SetEquals(targetCodes);
            if (started && structureChanged) return await RollbackConflict(transaction, "착수한 세트 인스턴스는 구성 code가 달라지는 사양으로 변경할 수 없습니다.", token);

            foreach (var removed in panels.Where(panel => panel.Status == "Active" && !targetCodes.Contains(panel.Code, StringComparer.OrdinalIgnoreCase)))
            {
                await CancelPanelAsync(connection, transaction, projectId, removed.PanelId, reason, actorId, correlationId, token);
            }
            foreach (var code in targetCodes.Where(code => !currentCodes.Contains(code)))
            {
                maxPanel++;
                await InsertSetPanelAsync(connection, transaction, projectId, instanceId, code, maxPanel, token);
            }
            await ExecuteAsync(connection, transaction, "update ul891_set_instances set spec_version_id=@version_id,row_version=row_version+1 where id=@instance_id;", token,
                ("version_id", request.VersionId.Value), ("instance_id", instanceId));
            await InsertAuditAsync(connection, transaction, projectId, "SetInstance", instanceId, "SetVersionApplied", "SpecVersionId", currentVersionId.ToString(), request.VersionId.Value.ToString(), reason, actorId, correlationId, token);
        }
        await RefreshPanelInformationAsync(connection, transaction, projectId, specId, request.VersionId.Value, token);
        var response = new Ul891MutationResponse(request.OperationId, projectId, "VersionApplied", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "ApplyVersion", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> CancelInstancesAsync(
        Guid projectId,
        CancelUl891InstancesRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var reason = NormalizeText(request.Reason);
        var instanceIds = request.InstanceIds?.Distinct().ToList() ?? [];
        var procurementIds = request.ProcurementItemIds?.Distinct().ToList() ?? [];
        if (request.OperationId == Guid.Empty || instanceIds.Count == 0 || reason is null)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { ["InstanceIds"] = ["취소할 세트 인스턴스·사유·operationId를 확인해 주세요."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        if (project.Status == "Completed") return await RollbackConflict(transaction, "완료된 프로젝트의 주문 수량은 변경할 수 없습니다.", token);
        var fingerprint = Fingerprint("CancelInstances", projectId, string.Join(',', instanceIds.Order()), string.Join(',', procurementIds.Order()), reason, request.ExceptionAcknowledged);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "CancelInstances", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        var orderedItems = await ReadOrderedProcurementIdsAsync(connection, transaction, projectId, token);
        if (orderedItems.Count > 0 && procurementIds.Count == 0)
        {
            return await RollbackValidation(transaction, "ProcurementItemIds", "발주일이 입력된 품목이 있습니다. 고객에게 청구·회수할 관련 품목을 선택해 주세요.", token);
        }
        if (procurementIds.Any(id => !orderedItems.Contains(id)))
        {
            return await RollbackValidation(transaction, "ProcurementItemIds", "선택한 품목은 이 프로젝트의 발주일이 입력된 활성 구매품목이어야 합니다.", token);
        }

        foreach (var instanceId in instanceIds)
        {
            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                lockCommand.CommandText = """
                    select instance.id
                    from ul891_set_instances instance join ul891_set_specs spec on spec.id=instance.spec_id
                    where instance.id=@instance_id and spec.project_id=@project_id and instance.status='Active'
                    for update of instance;
                    """;
                lockCommand.Parameters.AddWithValue("instance_id", instanceId);
                lockCommand.Parameters.AddWithValue("project_id", projectId);
                if (await lockCommand.ExecuteScalarAsync(token) is not Guid) return await RollbackValidation(transaction, "InstanceIds", "선택한 활성 세트 인스턴스를 찾을 수 없습니다.", token);
            }
            if (await InstanceHasDeliveryAsync(connection, transaction, instanceId, token)) return await RollbackConflict(transaction, "납품된 패널이 포함된 세트 인스턴스는 취소할 수 없습니다.", token);
            var panels = await ReadInstancePanelsAsync(connection, transaction, instanceId, token);
            var started = false;
            foreach (var panel in panels.Where(panel => panel.Status == "Active")) started |= await PanelHasStartedAsync(connection, transaction, panel.PanelId, token);
            if (started && request.ExceptionAcknowledged != true) return await RollbackValidation(transaction, "ExceptionAcknowledged", "이미 착수한 세트입니다. 영향 확인 후 예외 취소에 동의해 주세요.", token);

            foreach (var panel in panels.Where(panel => panel.Status == "Active"))
            {
                await CancelPanelAsync(connection, transaction, projectId, panel.PanelId, reason, actorId, correlationId, token);
            }
            await ExecuteAsync(connection, transaction, """
                update ul891_set_instances set status='Cancelled',row_version=row_version+1,cancelled_reason=@reason,
                    cancelled_exception_ack=@exception_ack,cancelled_by_user_id=@actor_id,cancelled_at_utc=now()
                where id=@instance_id;
                """, token, ("reason", reason), ("exception_ack", started), ("actor_id", actorId), ("instance_id", instanceId));
            foreach (var procurementId in procurementIds)
            {
                var caseId = Guid.NewGuid();
                await ExecuteAsync(connection, transaction, """
                    insert into ul891_recovery_cases (id,project_id,set_instance_id,procurement_item_id,created_by_user_id)
                    values (@id,@project_id,@instance_id,@procurement_item_id,@actor_id)
                    on conflict (set_instance_id,procurement_item_id) do nothing;
                    """, token, ("id", caseId), ("project_id", projectId), ("instance_id", instanceId), ("procurement_item_id", procurementId), ("actor_id", actorId));
                await ExecuteAsync(connection, transaction, """
                    insert into ul891_recovery_case_events (id,case_id,from_status,to_status,actor_user_id,reason)
                    select uuid_generate_v4(),id,null,'BillingRequired',@actor_id,@reason
                    from ul891_recovery_cases where set_instance_id=@instance_id and procurement_item_id=@procurement_item_id
                    and not exists(select 1 from ul891_recovery_case_events e where e.case_id=ul891_recovery_cases.id);
                    """, token, ("actor_id", actorId), ("reason", reason), ("instance_id", instanceId), ("procurement_item_id", procurementId));
            }
            await InsertAuditAsync(connection, transaction, projectId, "SetInstance", instanceId, "SetInstanceCancelled", "Status", "Active", "Cancelled", reason, actorId, correlationId, token);
        }
        var response = new Ul891MutationResponse(request.OperationId, projectId, "InstancesCancelled", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "CancelInstances", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    public async Task<ProjectMutationResult<Ul891MutationResponse>> RecoverCaseAsync(
        Guid projectId,
        Guid caseId,
        RecoverUl891CaseRequest request,
        Guid actorId,
        string correlationId,
        CancellationToken token)
    {
        var note = NormalizeText(request.Note);
        if (request.OperationId == Guid.Empty || request.ExpectedVersion is null || note is null)
        {
            return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { ["Note"] = ["회수 확인 버전·비고·operationId가 필요합니다."] });
        }
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var project = await LockProjectAsync(connection, transaction, projectId, token);
        if (project is null) return await RollbackNotFound(transaction, token);
        var fingerprint = Fingerprint("RecoverCase", projectId, caseId, request.ExpectedVersion, note);
        var replay = await CheckReplayAsync(connection, transaction, request.OperationId, projectId, actorId, "RecoverCase", fingerprint, token);
        if (replay is not null) { await transaction.RollbackAsync(token); return replay; }

        string status;
        int version;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select status,row_version from ul891_recovery_cases where id=@case_id and project_id=@project_id for update;";
            command.Parameters.AddWithValue("case_id", caseId);
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) return await RollbackNotFound(transaction, token);
            status = reader.GetString(0); version = reader.GetInt32(1);
        }
        if (version != request.ExpectedVersion) return await RollbackConflict(transaction, "다른 사용자가 회수 상태를 변경했습니다. 최신 내용을 다시 불러와 주세요.", token);
        if (status != "InvoiceConfirmed") return await RollbackConflict(transaction, "회계 발행이 확인된 회수 건만 회수 완료할 수 있습니다.", token);
        await ExecuteAsync(connection, transaction, """
            update ul891_recovery_cases set status='Recovered',note=@note,row_version=row_version+1,recovered_by_user_id=@actor_id,recovered_at_utc=now() where id=@case_id;
            insert into ul891_recovery_case_events (id,case_id,from_status,to_status,actor_user_id,reason)
            values (uuid_generate_v4(),@case_id,'InvoiceConfirmed','Recovered',@actor_id,@note);
            """, token, ("note", note), ("actor_id", actorId), ("case_id", caseId));
        await InsertAuditAsync(connection, transaction, projectId, "RecoveryCase", caseId, "RecoveryConfirmed", "Status", "InvoiceConfirmed", "Recovered", note, actorId, correlationId, token);
        var response = new Ul891MutationResponse(request.OperationId, projectId, "RecoveryConfirmed", false);
        await InsertOperationAsync(connection, transaction, request.OperationId, projectId, actorId, "RecoverCase", fingerprint, response, token);
        await transaction.CommitAsync(token);
        return ProjectMutationResult<Ul891MutationResponse>.Success(response);
    }

    private static async Task<int?> ReadActiveInstanceCountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid specId, CancellationToken token)
    {
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "select id from ul891_set_specs where id=@spec_id and project_id=@project_id for update;";
            lockCommand.Parameters.AddWithValue("spec_id", specId);
            lockCommand.Parameters.AddWithValue("project_id", projectId);
            if (await lockCommand.ExecuteScalarAsync(token) is not Guid) return null;
        }
        await using var command = connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="select count(instance.id) filter(where instance.status='Active')::integer from ul891_set_instances instance where instance.spec_id=@spec_id;";
        command.Parameters.AddWithValue("spec_id",specId); command.Parameters.AddWithValue("project_id",projectId);
        var value=await command.ExecuteScalarAsync(token); return value is int count ? count : null;
    }

    private static async Task<HashSet<string>?> ReadPublishedComponentCodesAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid projectId,Guid specId,Guid versionId,CancellationToken token)
    {
        var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="select component.component_code from ul891_set_specs spec join ul891_set_spec_versions version on version.spec_id=spec.id and version.id=@version_id and version.status='Published' join ul891_set_spec_components component on component.spec_version_id=version.id where spec.id=@spec_id and spec.project_id=@project_id order by component.sort_order;";
        command.Parameters.AddWithValue("version_id",versionId); command.Parameters.AddWithValue("spec_id",specId); command.Parameters.AddWithValue("project_id",projectId);
        await using var reader=await command.ExecuteReaderAsync(token); while(await reader.ReadAsync(token)) result.Add(reader.GetString(0));
        return result.Count==0 ? null : result;
    }

    private static async Task<IReadOnlyList<InstancePanelSnapshot>> ReadInstancePanelsAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid instanceId,CancellationToken token)
    {
        var result=new List<InstancePanelSnapshot>(); await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="select id,component_code,status from panel_placeholders where set_instance_id=@instance_id order by sequence_number for update;";
        command.Parameters.AddWithValue("instance_id",instanceId); await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2))); return result;
    }

    private static async Task<bool> InstanceHasDeliveryAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid instanceId,CancellationToken token)
    {
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="select exists(select 1 from logistics_delivery_results result join panel_placeholders panel on panel.id=result.panel_id where panel.set_instance_id=@instance_id);";
        command.Parameters.AddWithValue("instance_id",instanceId); return (bool)(await command.ExecuteScalarAsync(token) ?? false);
    }

    private static async Task<HashSet<Guid>> ReadOrderedProcurementIdsAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid projectId,CancellationToken token)
    {
        var result=new HashSet<Guid>(); await using var command=connection.CreateCommand(); command.Transaction=transaction;
        command.CommandText="select id from project_procurement_items where project_id=@project_id and status='Active' and order_date is not null order by id for update;";
        command.Parameters.AddWithValue("project_id",projectId); await using var reader=await command.ExecuteReaderAsync(token); while(await reader.ReadAsync(token)) result.Add(reader.GetGuid(0)); return result;
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }

    private static async Task<IReadOnlyList<Ul891SetSpecResponse>> ReadSpecsAsync(NpgsqlConnection connection, Guid projectId, CancellationToken token)
    {
        var specs = new Dictionary<Guid, SpecBuilder>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select spec.id, spec.spec_no, spec.name, spec.row_version,
                       count(instance.id) filter (where instance.status='Active')::integer
                from ul891_set_specs spec
                left join ul891_set_instances instance on instance.spec_id=spec.id
                where spec.project_id=@project_id
                group by spec.id order by spec.spec_no;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                specs[reader.GetGuid(0)] = new SpecBuilder(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4));
            }
        }
        if (specs.Count == 0) return [];

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select version.spec_id, version.id, version.version_number, version.status, version.revision_reason,
                       version.published_at_utc, component.id, component.component_code, component.panel_name,
                       component.panel_specification, component.width_mm, component.height_mm, component.depth_mm,
                       component.sort_order
                from ul891_set_spec_versions version
                left join ul891_set_spec_components component on component.spec_version_id=version.id
                join ul891_set_specs spec on spec.id=version.spec_id
                where spec.project_id=@project_id
                order by spec.spec_no, version.version_number desc, component.sort_order;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            var versionMap = new Dictionary<Guid, VersionBuilder>();
            while (await reader.ReadAsync(token))
            {
                var specId = reader.GetGuid(0);
                var versionId = reader.GetGuid(1);
                if (!versionMap.TryGetValue(versionId, out var version))
                {
                    version = new VersionBuilder(versionId, reader.GetInt32(2), reader.GetString(3), GetString(reader, 4), GetDateTimeOffset(reader, 5));
                    versionMap[versionId] = version;
                    specs[specId].Versions.Add(version);
                }
                if (!reader.IsDBNull(6))
                {
                    version.Components.Add(new Ul891SetComponentResponse(
                        reader.GetGuid(6), reader.GetString(7), GetString(reader, 8), GetString(reader, 9),
                        GetDecimal(reader, 10), GetDecimal(reader, 11), GetDecimal(reader, 12), reader.GetInt32(13)));
                }
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select instance.spec_id, instance.id, instance.instance_number, instance.spec_version_id,
                       version.version_number, instance.status, instance.row_version,
                       exists(select 1 from panel_manufacturing_executions execution join panel_placeholders p on p.id=execution.panel_id where p.set_instance_id=instance.id and execution.status in ('InProgress','Blocked','Completed'))
                         or exists(select 1 from panel_kitting_completions k join panel_placeholders p on p.id=k.panel_id where p.set_instance_id=instance.id)
                         or exists(select 1 from panel_quality_inspection_attempts q join panel_placeholders p on p.id=q.panel_id where p.set_instance_id=instance.id)
                         or exists(select 1 from logistics_packing_unit_panels m join panel_placeholders p on p.id=m.panel_id where p.set_instance_id=instance.id and m.active) as has_started,
                       exists(select 1 from logistics_delivery_results d join panel_placeholders p on p.id=d.panel_id where p.set_instance_id=instance.id) as has_delivered,
                       panel.id, panel.sequence_number, panel.display_code, panel.component_code,
                       coalesce(panel.panel_name, component.panel_name), component.panel_specification,
                       panel.status, panel.workflow_stage,
                       case when unit.id is null then null else 'PKG-' || unit.unit_number::text end,
                       departure.departure_date,
                       delivery.panel_id is not null
                from ul891_set_instances instance
                join ul891_set_specs spec on spec.id=instance.spec_id
                join ul891_set_spec_versions version on version.id=instance.spec_version_id
                left join panel_placeholders panel on panel.set_instance_id=instance.id
                left join ul891_set_spec_components component on component.spec_version_id=instance.spec_version_id and component.component_code=panel.component_code
                left join logistics_packing_unit_panels membership on membership.panel_id=panel.id and membership.active
                left join logistics_packing_units unit on unit.id=membership.packing_unit_id and unit.status<>'Cancelled'
                left join lateral (
                    select batch.departure_date
                    from logistics_batch_panels batch_panel join logistics_batches batch on batch.id=batch_panel.batch_id
                    where batch_panel.panel_id=panel.id and batch_panel.active
                      and batch.stage_code='DepartureProcessed' and batch.status='Finalized'
                    order by batch.finalized_at_utc desc limit 1
                ) departure on true
                left join lateral (
                    select result.panel_id from logistics_delivery_results result where result.panel_id=panel.id limit 1
                ) delivery on true
                where spec.project_id=@project_id
                order by spec.spec_no, instance.instance_number, panel.sequence_number;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(token);
            var instanceMap = new Dictionary<Guid, InstanceBuilder>();
            while (await reader.ReadAsync(token))
            {
                var specId = reader.GetGuid(0);
                var instanceId = reader.GetGuid(1);
                if (!instanceMap.TryGetValue(instanceId, out var instance))
                {
                    instance = new InstanceBuilder(instanceId, reader.GetInt32(2), reader.GetGuid(3), reader.GetInt32(4), reader.GetString(5), reader.GetInt32(6), reader.GetBoolean(7), reader.GetBoolean(8));
                    instanceMap[instanceId] = instance;
                    specs[specId].Instances.Add(instance);
                }
                if (!reader.IsDBNull(9))
                {
                    instance.Panels.Add(new Ul891SetPanelResponse(
                        reader.GetGuid(9), reader.GetInt32(10), reader.GetString(11), reader.GetString(12),
                        GetString(reader, 13), GetString(reader, 14), reader.GetString(15), reader.GetString(16),
                        GetString(reader, 17), GetDateOnly(reader, 18), reader.GetBoolean(19)));
                }
            }
        }
        return specs.Values.OrderBy(item => item.SpecNo).Select(item => item.Build()).ToList();
    }

    private static async Task<IReadOnlyList<Ul891OrderedProcurementItemResponse>> ReadOrderedProcurementItemsAsync(NpgsqlConnection connection, Guid projectId, CancellationToken token)
    {
        var result = new List<Ul891OrderedProcurementItemResponse>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select id, sequence_number, coalesce(order_item, '품목 ' || sequence_number::text), order_date
            from project_procurement_items
            where project_id=@project_id and status='Active' and order_date is not null
            order by sequence_number;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetFieldValue<DateOnly>(3)));
        return result;
    }

    private static async Task<IReadOnlyList<Ul891RecoveryCaseResponse>> ReadRecoveryCasesAsync(NpgsqlConnection connection, Guid projectId, CancellationToken token)
    {
        var result = new List<Ul891RecoveryCaseResponse>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select recovery.id, recovery.set_instance_id, instance.instance_number, recovery.procurement_item_id,
                   coalesce(item.order_item, '품목 ' || item.sequence_number::text), item.order_date,
                   recovery.status, recovery.note, recovery.row_version, recovery.created_at_utc, recovery.recovered_at_utc
            from ul891_recovery_cases recovery
            join ul891_set_instances instance on instance.id=recovery.set_instance_id
            join project_procurement_items item on item.id=recovery.procurement_item_id
            where recovery.project_id=@project_id
            order by recovery.created_at_utc, recovery.id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetGuid(3), reader.GetString(4),
                reader.GetFieldValue<DateOnly>(5), reader.GetString(6), GetString(reader, 7), reader.GetInt32(8),
                reader.GetFieldValue<DateTimeOffset>(9), GetDateTimeOffset(reader, 10)));
        }
        return result;
    }

    private static Dictionary<string, string[]> ValidateDraft(UpdateUl891DraftRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var name = NormalizeText(request.SpecName);
        if (request.ExpectedSpecVersion is null or < 1) errors[nameof(request.ExpectedSpecVersion)] = ["현재 사양 버전이 필요합니다."];
        if (name is null || name.Length > 120) errors[nameof(request.SpecName)] = ["세트 사양명은 1자 이상 120자 이하로 입력해 주세요."];
        if (request.Components is null || request.Components.Count == 0 || request.Components.Count > 200)
        {
            errors[nameof(request.Components)] = ["구성 패널은 1개 이상 200개 이하로 입력해 주세요."];
            return errors;
        }
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < request.Components.Count; index++)
        {
            var component = request.Components[index];
            var code = NormalizeText(component.ComponentCode)?.ToUpperInvariant();
            if (code is null || code.Length > 30 || !codes.Add(code)) errors[$"Components[{index}].ComponentCode"] = ["구성 code는 1~30자이며 중복될 수 없습니다."];
            if (NormalizeText(component.PanelName)?.Length > 200) errors[$"Components[{index}].PanelName"] = ["패널명은 200자 이하로 입력해 주세요."];
            if (NormalizeText(component.PanelSpecification)?.Length > 500) errors[$"Components[{index}].PanelSpecification"] = ["패널 규격은 500자 이하로 입력해 주세요."];
            if (new[] { component.WidthMm, component.HeightMm, component.DepthMm }.Any(value => value is < 0)) errors[$"Components[{index}].Dimensions"] = ["치수는 0 이상이어야 합니다."];
        }
        return errors;
    }

    private static async Task SyncDraftPanelsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid specId, Guid versionId, Guid actorId, string correlationId, CancellationToken token)
    {
        var existingCodes = await ReadComponentCodesAsync(connection, transaction, versionId, token);
        var maxPanel = await ReadMaxPanelSequenceAsync(connection, transaction, projectId, token);
        var instances = new List<Guid>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select id from ul891_set_instances where spec_id=@spec_id and spec_version_id=@version_id and status='Active' order by instance_number for update;";
            command.Parameters.AddWithValue("spec_id", specId);
            command.Parameters.AddWithValue("version_id", versionId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) instances.Add(reader.GetGuid(0));
        }
        foreach (var instanceId in instances)
        {
            var current = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "select id, component_code from panel_placeholders where set_instance_id=@instance_id and status='Active' order by sequence_number for update;";
                command.Parameters.AddWithValue("instance_id", instanceId);
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token)) current[reader.GetString(1)] = reader.GetGuid(0);
            }
            foreach (var removed in current.Where(pair => !existingCodes.Contains(pair.Key, StringComparer.OrdinalIgnoreCase)).ToList())
            {
                if (await PanelHasStartedAsync(connection, transaction, removed.Value, token)) throw new InvalidOperationException("착수한 세트 인스턴스의 구성 code는 Draft에서 제거할 수 없습니다.");
                await CancelPanelAsync(connection, transaction, projectId, removed.Value, "세트 Draft 구성 변경", actorId, correlationId, token);
            }
            foreach (var code in existingCodes.Where(code => !current.ContainsKey(code)))
            {
                maxPanel++;
                await InsertSetPanelAsync(connection, transaction, projectId, instanceId, code, maxPanel, token);
            }
        }
        await RefreshPanelInformationAsync(connection, transaction, projectId, specId, versionId, token);
    }

    private static async Task RefreshPanelInformationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid specId, Guid versionId, CancellationToken token)
    {
        await ExecuteAsync(connection, transaction, """
            update panel_placeholders panel
            set panel_name=component.panel_name,
                width_mm=component.width_mm,
                height_mm=component.height_mm,
                depth_mm=component.depth_mm,
                panel_info_completed=(
                    component.panel_name is not null
                    and (
                        (
                            component.width_mm is not null
                            and component.height_mm is not null
                            and component.depth_mm is not null
                        )
                        or (
                            project.packaging_method <> 'WoodenCrate'
                            and component.width_mm is null
                            and component.height_mm is null
                            and component.depth_mm is null
                        )
                    )
                ),
                qr_eligible=(panel.status='Active' and component.panel_name is not null),
                updated_at_utc=now()
            from ul891_set_instances instance,
                 ul891_set_spec_components component,
                 projects project
            where panel.set_instance_id=instance.id
              and instance.spec_id=@spec_id
              and project.id=@project_id
              and component.spec_version_id=instance.spec_version_id
              and component.component_code=panel.component_code;
            """, token, ("spec_id", specId), ("project_id", projectId));
    }

    private static async Task InsertSetPanelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid instanceId, string code, int sequence, CancellationToken token)
    {
        await ExecuteAsync(connection, transaction, """
            insert into panel_placeholders (
                id, project_id, sequence_number, display_code, status,
                panel_info_completed, qr_eligible, set_instance_id, component_code, updated_at_utc
            ) values (@id,@project_id,@sequence,@display_code,'Active',false,false,@instance_id,@code,now());
            """, token, ("id", Guid.NewGuid()), ("project_id", projectId), ("sequence", sequence),
            ("display_code", ProjectInputNormalizer.FormatPanelDisplayCode(sequence)), ("instance_id", instanceId), ("code", code));
    }

    private static async Task CancelPanelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid panelId, string reason, Guid actorId, string correlationId, CancellationToken token)
    {
        await ManufacturingStore.CancelActiveExecutionAsync(connection, transaction, panelId, actorId, token);
        await QualityInspectionStore.CancelPanelInspectionsAsync(connection, transaction, panelId, actorId, token);
        await LogisticsStore.CancelPanelDraftsAsync(connection, transaction, panelId, actorId, token);
        await ExecuteAsync(connection, transaction, """
            update panel_placeholders set status='Cancelled', updated_at_utc=now(), cancelled_by_user_id=@actor_id,
                cancelled_at_utc=now(), cancellation_reason=@reason where id=@panel_id and status='Active';
            update work_items set status='Cancelled', cancelled_at_utc=coalesce(cancelled_at_utc,now())
            where target_type='Panel' and target_id=@panel_id and status in ('Requested','InProgress');
            """, token, ("actor_id", actorId), ("reason", reason), ("panel_id", panelId));
        await InsertAuditAsync(connection, transaction, projectId, "PanelPlaceholder", panelId, "PanelCancelled", "PanelStatus", "Active", "Cancelled", reason, actorId, correlationId, token);
    }

    private static async Task<bool> PanelHasStartedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid panelId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists(select 1 from panel_manufacturing_executions where panel_id=@panel_id and status in ('InProgress','Blocked','Completed'))
                or exists(select 1 from panel_kitting_completions where panel_id=@panel_id)
                or exists(select 1 from panel_quality_inspection_attempts where panel_id=@panel_id)
                or exists(select 1 from logistics_packing_unit_panels where panel_id=@panel_id and active);
            """;
        command.Parameters.AddWithValue("panel_id", panelId);
        return (bool)(await command.ExecuteScalarAsync(token) ?? false);
    }

    private static async Task<ProjectSnapshot?> LockProjectAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select status, item, structure_mode, packaging_method, sales_amount, currency_code, coalesce(project_title,name) from projects where id=@project_id and deleted_at_utc is null for update;";
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        var snapshot = new ProjectSnapshot(reader.GetString(0), reader.GetString(1), GetString(reader, 2), GetString(reader, 3), GetDecimal(reader, 4), GetString(reader, 5), reader.GetString(6));
        return snapshot.StructureMode == "Ul891Set" ? snapshot : null;
    }

    private static async Task<SpecVersionSnapshot?> LockSpecVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid specId, Guid versionId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select spec.row_version, version.version_number, version.status
            from ul891_set_specs spec join ul891_set_spec_versions version on version.spec_id=spec.id
            where spec.id=@spec_id and spec.project_id=@project_id and version.id=@version_id
            for update of spec, version;
            """;
        command.Parameters.AddWithValue("spec_id", specId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("version_id", versionId);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2)) : null;
    }

    private static async Task<IReadOnlyList<string>> ReadComponentCodesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid versionId, CancellationToken token)
    {
        var codes = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select component_code from ul891_set_spec_components where spec_version_id=@version_id order by sort_order;";
        command.Parameters.AddWithValue("version_id", versionId);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) codes.Add(reader.GetString(0));
        return codes;
    }

    private static async Task<int> ReadMaxPanelSequenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(max(sequence_number),0)::integer from panel_placeholders where project_id=@project_id;";
        command.Parameters.AddWithValue("project_id", projectId);
        return (int)(await command.ExecuteScalarAsync(token) ?? 0);
    }

    private static async Task<ProjectMutationResult<Ul891MutationResponse>?> CheckReplayAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, Guid projectId, Guid actorId, string action, string fingerprint, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select project_id, actor_user_id, action, payload_fingerprint from ul891_set_operations where operation_id=@operation_id;";
        command.Parameters.AddWithValue("operation_id", operationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return reader.GetGuid(0) == projectId && reader.GetGuid(1) == actorId && reader.GetString(2) == action && reader.GetString(3) == fingerprint
            ? ProjectMutationResult<Ul891MutationResponse>.Success(new(operationId, projectId, action, true))
            : ProjectMutationResult<Ul891MutationResponse>.Conflict("같은 operationId에 다른 요청 내용이 사용되었습니다.");
    }

    private static async Task InsertOperationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operationId, Guid projectId, Guid actorId, string action, string fingerprint, Ul891MutationResponse response, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "insert into ul891_set_operations (operation_id,project_id,actor_user_id,action,payload_fingerprint,result_projection) values (@operation_id,@project_id,@actor_id,@action,@fingerprint,@result::jsonb);";
        command.Parameters.AddWithValue("operation_id", operationId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("fingerprint", fingerprint);
        command.Parameters.AddWithValue("result", JsonSerializer.Serialize(response));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string entityType, Guid entityId, string action, string? fieldName, string? oldValue, string? newValue, string? reason, Guid actorId, string correlationId, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_audit_events (project_id,entity_type,entity_id,action,field_name,old_value,new_value,reason,changed_by_user_id,correlation_id,is_sensitive)
            values (@project_id,@entity_type,@entity_id,@action,@field_name,@old_value,@new_value,@reason,@actor_id,@correlation_id,false);
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("action", action);
        AddNullableText(command, "field_name", fieldName);
        AddNullableText(command, "old_value", oldValue);
        AddNullableText(command, "new_value", newValue);
        AddNullableText(command, "reason", NormalizeText(reason));
        command.Parameters.AddWithValue("actor_id", actorId);
        command.Parameters.AddWithValue("correlation_id", correlationId.Length <= 200 ? correlationId : correlationId[..200]);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static void AddNullableText(NpgsqlCommand command, string name, string? value) => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;
    private static void AddNullableDecimal(NpgsqlCommand command, string name, decimal? value) => command.Parameters.Add(name, NpgsqlDbType.Numeric).Value = value ?? (object)DBNull.Value;
    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Fingerprint(params object?[] values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", values.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""))))).ToLowerInvariant();
    private static string? GetString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static decimal? GetDecimal(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    private static DateTimeOffset? GetDateTimeOffset(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    private static DateOnly? GetDateOnly(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal);

    private static async Task<ProjectMutationResult<Ul891MutationResponse>> RollbackNotFound(NpgsqlTransaction transaction, CancellationToken token) { await transaction.RollbackAsync(token); return ProjectMutationResult<Ul891MutationResponse>.NotFound(); }
    private static async Task<ProjectMutationResult<Ul891MutationResponse>> RollbackConflict(NpgsqlTransaction transaction, string message, CancellationToken token) { await transaction.RollbackAsync(token); return ProjectMutationResult<Ul891MutationResponse>.Conflict(message); }
    private static async Task<ProjectMutationResult<Ul891MutationResponse>> RollbackValidation(NpgsqlTransaction transaction, string field, string message, CancellationToken token) { await transaction.RollbackAsync(token); return ProjectMutationResult<Ul891MutationResponse>.Validation(new Dictionary<string, string[]> { [field] = [message] }); }

    private sealed record ProjectSnapshot(string Status, string Item, string? StructureMode, string? PackagingMethod, decimal? SalesAmount, string? CurrencyCode, string ProjectTitle);
    private sealed record SpecVersionSnapshot(int SpecRowVersion, int VersionNumber, string VersionStatus);
    private sealed record InstancePanelSnapshot(Guid PanelId, string Code, string Status);

    private sealed class SpecBuilder(Guid specId, int specNo, string name, int rowVersion, int activeCount)
    {
        public Guid SpecId { get; } = specId;
        public int SpecNo { get; } = specNo;
        public string Name { get; } = name;
        public int RowVersion { get; } = rowVersion;
        public int ActiveCount { get; } = activeCount;
        public List<VersionBuilder> Versions { get; } = [];
        public List<InstanceBuilder> Instances { get; } = [];
        public Ul891SetSpecResponse Build() => new(SpecId, SpecNo, Name, RowVersion, ActiveCount, Versions.Select(item => item.Build()).ToList(), Instances.Select(item => item.Build()).ToList());
    }
    private sealed class VersionBuilder(Guid id, int number, string status, string? reason, DateTimeOffset? publishedAt)
    {
        public Guid Id { get; } = id; public int Number { get; } = number; public string Status { get; } = status; public string? Reason { get; } = reason; public DateTimeOffset? PublishedAt { get; } = publishedAt;
        public List<Ul891SetComponentResponse> Components { get; } = [];
        public Ul891SetVersionResponse Build() => new(Id, Number, Status, Reason, PublishedAt, Components);
    }
    private sealed class InstanceBuilder(Guid id, int number, Guid versionId, int versionNumber, string status, int rowVersion, bool started, bool delivered)
    {
        public Guid Id { get; } = id; public int Number { get; } = number; public Guid VersionId { get; } = versionId; public int VersionNumber { get; } = versionNumber; public string Status { get; } = status; public int RowVersion { get; } = rowVersion; public bool Started { get; } = started; public bool Delivered { get; } = delivered;
        public List<Ul891SetPanelResponse> Panels { get; } = [];
        public Ul891SetInstanceResponse Build() => new(Id, Number, VersionId, VersionNumber, Status, RowVersion, Started, Delivered, Panels);
    }
}
