using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.ProductionPlanning;

public sealed class ProductionControlTemplateStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<ProductionControlTemplateCatalogResponse> GetCatalogAsync(
        Guid userId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var domains = isSystemAdministrator
            ? new HashSet<string>(["Manufacturing", "ProductionPlanning"], StringComparer.Ordinal)
            : await ReadDomainsAsync(connection, null, userId, cancellationToken);
        var productTypes = new List<(Guid Id, string Code, string Name)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select id, code, name
                from production_product_types
                where is_active
                order by code;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                productTypes.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        var items = new List<ProductionControlItemTemplateResponse>();
        foreach (var productType in productTypes)
        {
            items.Add(new(
                productType.Id,
                productType.Code,
                productType.Name,
                await ReadManufacturingVersionsAsync(connection, null, productType.Id, cancellationToken),
                await ReadPlanVersionsAsync(connection, null, productType.Id, cancellationToken)));
        }
        return new(
            domains.Contains("Manufacturing"),
            domains.Contains("ProductionPlanning"),
            ProductionControlSourceCodes.Catalog,
            items);
    }

    public Task<ProductionControlTemplateCatalogResponse> CreateManufacturingDraftAsync(
        Guid productTypeId,
        CreateProductionControlDraftRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
        => CreateDraftAsync("Manufacturing", productTypeId, request, actorUserId, isSystemAdministrator, cancellationToken);

    public Task<ProductionControlTemplateCatalogResponse> CreatePlanDraftAsync(
        Guid productTypeId,
        CreateProductionControlDraftRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
        => CreateDraftAsync("ProductionPlanning", productTypeId, request, actorUserId, isSystemAdministrator, cancellationToken);

    public async Task<ProductionControlTemplateCatalogResponse> SaveManufacturingAsync(
        Guid productTypeId,
        Guid versionId,
        SaveProductionControlManufacturingVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        ValidateManufacturingItems(request.Items);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, "Manufacturing", cancellationToken);
        var templateId = await EnsureTemplateAsync(connection, transaction, "Manufacturing", productTypeId, cancellationToken);
        await LockDraftAsync(connection, transaction, "Manufacturing", templateId, versionId, request.ExpectedRowVersion, cancellationToken);
        var existingKeys = await ReadDefinitionKeysAsync(connection, transaction, "Manufacturing", versionId, cancellationToken);
        await ExecuteAsync(connection, transaction,
            "delete from production_control_manufacturing_items where template_version_id=@version_id;",
            [new("version_id", versionId)], cancellationToken);
        foreach (var item in request.Items.OrderBy(item => item.DisplayOrder))
        {
            var definitionKey = ResolveDefinitionKey(item.DefinitionKey, existingKeys);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into production_control_manufacturing_items (
                    template_version_id, definition_key, display_order, label, step_role
                )
                values (@version_id, @definition_key, @display_order, @label, @step_role);
                """;
            command.Parameters.AddWithValue("version_id", versionId);
            command.Parameters.AddWithValue("definition_key", definitionKey);
            command.Parameters.AddWithValue("display_order", item.DisplayOrder);
            command.Parameters.AddWithValue("label", item.Label.Trim());
            command.Parameters.AddWithValue("step_role", NormalizeStepRole(item.StepRole));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await IncrementDraftAsync(connection, transaction, "Manufacturing", versionId, request.ExpectedRowVersion, actorUserId, cancellationToken);
        await AppendAuditAsync(connection, transaction, "DraftSaved", "Manufacturing", "ProductionControlManufacturing", productTypeId, versionId, actorUserId, new { itemCount = request.Items.Count }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetCatalogAsync(actorUserId, isSystemAdministrator, cancellationToken);
    }

    public async Task<ProductionControlTemplateCatalogResponse> SavePlanAsync(
        Guid productTypeId,
        Guid versionId,
        SaveProductionControlPlanVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        ValidatePlanItems(request.Items);
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, "ProductionPlanning", cancellationToken);
        var templateId = await EnsureTemplateAsync(connection, transaction, "ProductionPlanning", productTypeId, cancellationToken);
        await LockDraftAsync(connection, transaction, "ProductionPlanning", templateId, versionId, request.ExpectedRowVersion, cancellationToken);
        var existingKeys = await ReadDefinitionKeysAsync(connection, transaction, "ProductionPlanning", versionId, cancellationToken);
        await ExecuteAsync(connection, transaction,
            "delete from production_control_plan_items where template_version_id=@version_id;",
            [new("version_id", versionId)], cancellationToken);
        foreach (var item in request.Items.OrderBy(item => item.DisplayOrder))
        {
            var definitionKey = ResolveDefinitionKey(item.DefinitionKey, existingKeys);
            var planItemId = Guid.NewGuid();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into production_control_plan_items (
                        id, template_version_id, definition_key, display_order, label, is_required
                    )
                    values (@id, @version_id, @definition_key, @display_order, @label, @is_required);
                    """;
                command.Parameters.AddWithValue("id", planItemId);
                command.Parameters.AddWithValue("version_id", versionId);
                command.Parameters.AddWithValue("definition_key", definitionKey);
                command.Parameters.AddWithValue("display_order", item.DisplayOrder);
                command.Parameters.AddWithValue("label", item.Label.Trim());
                command.Parameters.AddWithValue("is_required", item.IsRequired);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var connectionItem in item.Connections.DistinctBy(value => (value.SourceCode, value.SourceDefinitionKey)))
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    insert into production_control_plan_connections (
                        plan_item_id, source_code, source_definition_key
                    )
                    values (@plan_item_id, @source_code, @source_definition_key);
                    """;
                command.Parameters.AddWithValue("plan_item_id", planItemId);
                command.Parameters.AddWithValue("source_code", NormalizeSourceCode(connectionItem.SourceCode));
                command.Parameters.Add("source_definition_key", NpgsqlDbType.Uuid).Value =
                    connectionItem.SourceDefinitionKey ?? (object)DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await IncrementDraftAsync(connection, transaction, "ProductionPlanning", versionId, request.ExpectedRowVersion, actorUserId, cancellationToken);
        await AppendAuditAsync(connection, transaction, "DraftSaved", "ProductionPlanning", "ProductionControlPlan", productTypeId, versionId, actorUserId, new { itemCount = request.Items.Count }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetCatalogAsync(actorUserId, isSystemAdministrator, cancellationToken);
    }

    public Task<ProductionControlTemplateCatalogResponse> ActivateManufacturingAsync(
        Guid productTypeId,
        Guid versionId,
        TransitionProductionControlVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
        => TransitionAsync("Manufacturing", productTypeId, versionId, request, actorUserId, isSystemAdministrator, true, cancellationToken);

    public Task<ProductionControlTemplateCatalogResponse> ActivatePlanAsync(
        Guid productTypeId,
        Guid versionId,
        TransitionProductionControlVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
        => TransitionAsync("ProductionPlanning", productTypeId, versionId, request, actorUserId, isSystemAdministrator, true, cancellationToken);

    public Task<ProductionControlTemplateCatalogResponse> ArchiveManufacturingDraftAsync(
        Guid productTypeId,
        Guid versionId,
        TransitionProductionControlVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
        => TransitionAsync("Manufacturing", productTypeId, versionId, request, actorUserId, isSystemAdministrator, false, cancellationToken);

    public Task<ProductionControlTemplateCatalogResponse> ArchivePlanDraftAsync(
        Guid productTypeId,
        Guid versionId,
        TransitionProductionControlVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
        => TransitionAsync("ProductionPlanning", productTypeId, versionId, request, actorUserId, isSystemAdministrator, false, cancellationToken);

    private async Task<ProductionControlTemplateCatalogResponse> CreateDraftAsync(
        string domain,
        Guid productTypeId,
        CreateProductionControlDraftRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, domain, cancellationToken);
        var templateId = await EnsureTemplateAsync(connection, transaction, domain, productTypeId, cancellationToken);
        var existingDraft = await ReadVersionAsync(connection, transaction, domain, templateId, "Draft", true, cancellationToken);
        if (existingDraft is not null)
        {
            throw new ProductionControlTemplateConflictException("이미 편집 중인 초안이 있습니다.");
        }
        var active = await ReadVersionAsync(connection, transaction, domain, templateId, "Active", true, cancellationToken);
        if (request.ExpectedActiveRowVersion is not null
            && (active is null || active.Value.RowVersion != request.ExpectedActiveRowVersion.Value))
        {
            throw new ProductionControlTemplateConflictException("사용 중 양식이 변경되었습니다. 새로고침해 주세요.");
        }
        var versionId = Guid.NewGuid();
        var nextVersion = await NextVersionAsync(connection, transaction, domain, templateId, cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = domain == "Manufacturing"
                ? """
                    insert into production_control_manufacturing_versions (
                        id, template_id, version_number, lifecycle_status, created_by_user_id, updated_by_user_id
                    )
                    values (@id, @template_id, @version_number, 'Draft', @actor_id, @actor_id);
                    """
                : """
                    insert into production_control_plan_versions (
                        id, template_id, version_number, lifecycle_status, created_by_user_id, updated_by_user_id
                    )
                    values (@id, @template_id, @version_number, 'Draft', @actor_id, @actor_id);
                    """;
            command.Parameters.AddWithValue("id", versionId);
            command.Parameters.AddWithValue("template_id", templateId);
            command.Parameters.AddWithValue("version_number", nextVersion);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        if (active is not null)
        {
            await CloneItemsAsync(connection, transaction, domain, active.Value.Id, versionId, cancellationToken);
        }
        else
        {
            await SeedFirstDraftAsync(connection, transaction, domain, productTypeId, versionId, cancellationToken);
        }
        await AppendAuditAsync(connection, transaction, "DraftCreated", domain, domain == "Manufacturing" ? "ProductionControlManufacturing" : "ProductionControlPlan", productTypeId, versionId, actorUserId, new { version = nextVersion }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetCatalogAsync(actorUserId, isSystemAdministrator, cancellationToken);
    }

    private async Task<ProductionControlTemplateCatalogResponse> TransitionAsync(
        string domain,
        Guid productTypeId,
        Guid versionId,
        TransitionProductionControlVersionRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        bool activate,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await DemandAccessAsync(connection, transaction, actorUserId, isSystemAdministrator, domain, cancellationToken);
        var templateId = await EnsureTemplateAsync(connection, transaction, domain, productTypeId, cancellationToken);
        await LockDraftAsync(connection, transaction, domain, templateId, versionId, request.ExpectedRowVersion, cancellationToken);
        var itemCount = await CountItemsAsync(connection, transaction, domain, versionId, cancellationToken);
        if (activate && itemCount == 0)
        {
            throw new ArgumentException("항목을 한 개 이상 등록해 주세요.", "items");
        }
        if (activate && domain == "ProductionPlanning")
        {
            await ValidatePlanActivationAsync(connection, transaction, productTypeId, versionId, cancellationToken);
        }
        if (activate && domain == "Manufacturing")
        {
            await ValidateManufacturingActivationAsync(connection, transaction, productTypeId, versionId, cancellationToken);
        }

        var versionTable = domain == "Manufacturing"
            ? "production_control_manufacturing_versions"
            : "production_control_plan_versions";
        if (activate)
        {
            await using (var archive = connection.CreateCommand())
            {
                archive.Transaction = transaction;
                archive.CommandText = $"""
                    update {versionTable}
                    set lifecycle_status='Archived',
                        archived_at_utc=now(),
                        row_version=row_version+1,
                        updated_by_user_id=@actor_id,
                        updated_at_utc=now()
                    where template_id=@template_id and lifecycle_status='Active';
                    """;
                archive.Parameters.AddWithValue("actor_id", actorUserId);
                archive.Parameters.AddWithValue("template_id", templateId);
                await archive.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await using (var transition = connection.CreateCommand())
        {
            transition.Transaction = transaction;
            transition.CommandText = activate
                ? $"""
                    update {versionTable}
                    set lifecycle_status='Active',
                        activated_at_utc=now(),
                        row_version=row_version+1,
                        updated_by_user_id=@actor_id,
                        updated_at_utc=now()
                    where id=@id and lifecycle_status='Draft' and row_version=@expected;
                    """
                : $"""
                    update {versionTable}
                    set lifecycle_status='Archived',
                        archived_at_utc=now(),
                        row_version=row_version+1,
                        updated_by_user_id=@actor_id,
                        updated_at_utc=now()
                    where id=@id and lifecycle_status='Draft' and row_version=@expected;
                    """;
            transition.Parameters.AddWithValue("id", versionId);
            transition.Parameters.AddWithValue("expected", request.ExpectedRowVersion);
            transition.Parameters.AddWithValue("actor_id", actorUserId);
            if (await transition.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new ProductionControlTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요.");
            }
        }
        await AppendAuditAsync(connection, transaction, activate ? "VersionActivated" : "DraftArchived", domain, domain == "Manufacturing" ? "ProductionControlManufacturing" : "ProductionControlPlan", productTypeId, versionId, actorUserId, new { }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetCatalogAsync(actorUserId, isSystemAdministrator, cancellationToken);
    }

    private static async Task ValidatePlanActivationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid productTypeId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        Guid manufacturingVersionId;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select version.id
                from production_control_manufacturing_templates template
                join production_control_manufacturing_versions version
                  on version.template_id=template.id and version.lifecycle_status='Active'
                where template.product_type_id=@product_type_id
                for share of version;
                """;
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid id)
            {
                throw new ArgumentException("같은 Item의 제조 양식을 먼저 활성화해 주세요.", "manufacturingTemplate");
            }
            manufacturingVersionId = id;
        }
        await using var invalid = connection.CreateCommand();
        invalid.Transaction = transaction;
        invalid.CommandText = """
            select count(*)::int
            from production_control_plan_items item
            left join production_control_plan_connections connection on connection.plan_item_id=item.id
            left join production_control_manufacturing_items manufacturing
              on manufacturing.template_version_id=@manufacturing_version_id
             and manufacturing.definition_key=connection.source_definition_key
            where item.template_version_id=@version_id
              and (
                (item.is_required and connection.id is null)
                or (
                    connection.source_code in ('MANUFACTURING_STEP_COMPLETED','LQC_PASSED')
                    and manufacturing.id is null
                )
              );
            """;
        invalid.Parameters.AddWithValue("version_id", versionId);
        invalid.Parameters.AddWithValue("manufacturing_version_id", manufacturingVersionId);
        if (Convert.ToInt32(await invalid.ExecuteScalarAsync(cancellationToken)) > 0)
        {
            throw new ArgumentException("필수 항목의 실적 연결 또는 제조 단계 연결을 확인해 주세요.", "connections");
        }
    }

    private static async Task ValidateManufacturingActivationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid productTypeId,
        Guid manufacturingVersionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select count(*)::int
            from production_control_plan_templates plan_template
            join production_control_plan_versions plan_version
              on plan_version.template_id=plan_template.id
             and plan_version.lifecycle_status='Active'
            join production_control_plan_items plan_item
              on plan_item.template_version_id=plan_version.id
            join production_control_plan_connections connection
              on connection.plan_item_id=plan_item.id
             and connection.source_code in ('MANUFACTURING_STEP_COMPLETED','LQC_PASSED')
            left join production_control_manufacturing_items manufacturing_item
              on manufacturing_item.template_version_id=@manufacturing_version_id
             and manufacturing_item.definition_key=connection.source_definition_key
            where plan_template.product_type_id=@product_type_id
              and manufacturing_item.id is null;
            """;
        command.Parameters.AddWithValue("product_type_id", productTypeId);
        command.Parameters.AddWithValue("manufacturing_version_id", manufacturingVersionId);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0)
        {
            throw new ArgumentException(
                "사용 중인 생산계획이 참조하는 제조 단계가 새 제조 양식에 없습니다. 생산계획 연결을 먼저 새 제조 단계에 맞게 준비해 주세요.",
                "connections");
        }
    }

    private static void ValidateManufacturingItems(IReadOnlyList<SaveProductionControlManufacturingItemRequest> items)
    {
        if (items.Count is < 1 or > 50) throw new ArgumentException("제조 단계는 1개부터 50개까지 등록해 주세요.", "items");
        if (items.Select(item => item.DisplayOrder).Distinct().Count() != items.Count) throw new ArgumentException("제조 단계 순서는 중복될 수 없습니다.", "items");
        if (items.Count(item => NormalizeStepRole(item.StepRole) == "Assembly") > 1) throw new ArgumentException("일괄 조립 단계는 한 개만 지정할 수 있습니다.", "items");
        foreach (var item in items)
        {
            if (item.DisplayOrder is < 1 or > 50) throw new ArgumentException("제조 단계 순서를 확인해 주세요.", "items");
            if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Trim().Length > 100) throw new ArgumentException("제조 단계명은 1자부터 100자까지 입력해 주세요.", "items");
        }
    }

    private static void ValidatePlanItems(IReadOnlyList<SaveProductionControlPlanItemRequest> items)
    {
        if (items.Count is < 1 or > 100) throw new ArgumentException("생산계획 항목은 1개부터 100개까지 등록해 주세요.", "items");
        if (items.Select(item => item.DisplayOrder).Distinct().Count() != items.Count) throw new ArgumentException("생산계획 항목 순서는 중복될 수 없습니다.", "items");
        foreach (var item in items)
        {
            if (item.DisplayOrder is < 1 or > 100) throw new ArgumentException("생산계획 항목 순서를 확인해 주세요.", "items");
            if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Trim().Length > 120) throw new ArgumentException("생산계획 항목명은 1자부터 120자까지 입력해 주세요.", "items");
            foreach (var connection in item.Connections)
            {
                var code = NormalizeSourceCode(connection.SourceCode);
                if (ProductionControlSourceCodes.RequiresManufacturingDefinition(code) != (connection.SourceDefinitionKey is not null))
                {
                    throw new ArgumentException("제조·LQC 연결은 제조 단계를 선택해야 합니다.", "connections");
                }
            }
        }
    }

    private static Guid ResolveDefinitionKey(Guid? requested, HashSet<Guid> existing)
    {
        if (requested is null || requested == Guid.Empty) return Guid.NewGuid();
        if (!existing.Contains(requested.Value)) throw new ArgumentException("항목 고유번호를 임의로 변경할 수 없습니다.", "definitionKey");
        return requested.Value;
    }

    private static string NormalizeStepRole(string value)
        => value is "General" or "Assembly" ? value : throw new ArgumentException("지원하지 않는 제조 단계 역할입니다.", "stepRole");

    private static string NormalizeSourceCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return ProductionControlSourceCodes.IsSupported(normalized)
            ? normalized
            : throw new ArgumentException("지원하지 않는 실적 연결입니다.", "sourceCode");
    }

    private static async Task<Guid> EnsureTemplateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid productTypeId,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_templates"
            : "production_control_plan_templates";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            insert into {table} (product_type_id)
            values (@product_type_id)
            on conflict (product_type_id) do update set product_type_id=excluded.product_type_id
            returning id;
            """;
        command.Parameters.AddWithValue("product_type_id", productTypeId);
        try
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is Guid id ? id : throw new InvalidOperationException("양식 기준을 만들지 못했습니다.");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ArgumentException("등록된 Item을 선택해 주세요.", "productTypeId");
        }
    }

    private static async Task CloneItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid activeVersionId,
        Guid draftVersionId,
        CancellationToken cancellationToken)
    {
        if (domain == "Manufacturing")
        {
            await ExecuteAsync(connection, transaction, """
                insert into production_control_manufacturing_items (
                    template_version_id, definition_key, display_order, label, step_role
                )
                select @draft_id, definition_key, display_order, label, step_role
                from production_control_manufacturing_items
                where template_version_id=@active_id;
                """, [new("draft_id", draftVersionId), new("active_id", activeVersionId)], cancellationToken);
            return;
        }
        await ExecuteAsync(connection, transaction, """
            with copied_items as (
                insert into production_control_plan_items (
                    template_version_id, definition_key, display_order, label, is_required
                )
                select @draft_id, definition_key, display_order, label, is_required
                from production_control_plan_items
                where template_version_id=@active_id
                returning id, definition_key
            )
            insert into production_control_plan_connections (
                plan_item_id, source_code, source_definition_key
            )
            select copied.id, connection.source_code, connection.source_definition_key
            from copied_items copied
            join production_control_plan_items active_item
              on active_item.template_version_id=@active_id
             and active_item.definition_key=copied.definition_key
            join production_control_plan_connections connection
              on connection.plan_item_id=active_item.id;
            """, [new("draft_id", draftVersionId), new("active_id", activeVersionId)], cancellationToken);
    }

    private static async Task SeedFirstDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid productTypeId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (domain == "Manufacturing")
        {
            await ExecuteAsync(connection, transaction, """
                insert into production_control_manufacturing_items (
                    template_version_id, definition_key, display_order, label, step_role
                )
                select @version_id,
                       item.id,
                       item.display_order,
                       item.label,
                       case when item.item_code='MANUFACTURING' then 'Assembly' else 'General' end
                from manufacturing_step_template_versions version
                join manufacturing_step_templates template on template.id=version.template_id
                join manufacturing_step_template_items item on item.template_version_id=version.id
                where template.template_code='PANEL_MANUFACTURING'
                  and version.lifecycle_status='Active'
                order by item.display_order;
                """, [new("version_id", versionId)], cancellationToken);
            return;
        }
        await ExecuteAsync(connection, transaction, """
            insert into production_control_plan_items (
                template_version_id, definition_key, display_order, label, is_required
            )
            select @version_id, step.id, step.sequence_number, step.step_name, step.is_required
            from production_plan_templates template
            join production_plan_template_steps step on step.template_id=template.id
            where template.product_type_id=@product_type_id
              and template.is_active
              and step.is_active
            order by step.sequence_number;
            """, [new("version_id", versionId), new("product_type_id", productTypeId)], cancellationToken);
    }

    private static async Task<HashSet<Guid>> ReadDefinitionKeysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_items"
            : "production_control_plan_items";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select definition_key from {table} where template_version_id=@version_id;";
        command.Parameters.AddWithValue("version_id", versionId);
        var result = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0));
        return result;
    }

    private static async Task<(Guid Id, int RowVersion)?> ReadVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid templateId,
        string status,
        bool lockRow,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_versions"
            : "production_control_plan_versions";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select id,row_version from {table} where template_id=@template_id and lifecycle_status=@status{(lockRow ? " for update" : "")};";
        command.Parameters.AddWithValue("template_id", templateId);
        command.Parameters.AddWithValue("status", status);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetGuid(0), reader.GetInt32(1)) : null;
    }

    private static async Task LockDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid templateId,
        Guid versionId,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_versions"
            : "production_control_plan_versions";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select row_version from {table} where id=@id and template_id=@template_id and lifecycle_status='Draft' for update;";
        command.Parameters.AddWithValue("id", versionId);
        command.Parameters.AddWithValue("template_id", templateId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not int rowVersion) throw new ProductionControlTemplateConflictException("편집 가능한 초안을 찾을 수 없습니다.");
        if (rowVersion != expectedRowVersion) throw new ProductionControlTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요.");
    }

    private static async Task IncrementDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid versionId,
        int expectedRowVersion,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_versions"
            : "production_control_plan_versions";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            update {table}
            set row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
            where id=@id and lifecycle_status='Draft' and row_version=@expected;
            """;
        command.Parameters.AddWithValue("id", versionId);
        command.Parameters.AddWithValue("expected", expectedRowVersion);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new ProductionControlTemplateConflictException("양식이 변경되었습니다. 새로고침해 주세요.");
    }

    private static async Task<int> NextVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_versions"
            : "production_control_plan_versions";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select coalesce(max(version_number),0)::int+1 from {table} where template_id=@template_id;";
        command.Parameters.AddWithValue("template_id", templateId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> CountItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string domain,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var table = domain == "Manufacturing"
            ? "production_control_manufacturing_items"
            : "production_control_plan_items";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select count(*)::int from {table} where template_version_id=@version_id;";
        command.Parameters.AddWithValue("version_id", versionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<IReadOnlyList<ProductionControlManufacturingVersionResponse>> ReadManufacturingVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid productTypeId,
        CancellationToken cancellationToken)
    {
        var versions = new List<ProductionControlManufacturingVersionResponse>();
        var rows = new List<(Guid Id, int Number, string Status, int RowVersion, DateTimeOffset? Activated, DateTimeOffset? Archived)>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select version.id,version.version_number,version.lifecycle_status,version.row_version,
                       version.activated_at_utc,version.archived_at_utc
                from production_control_manufacturing_templates template
                join production_control_manufacturing_versions version on version.template_id=template.id
                where template.product_type_id=@product_type_id
                order by version.version_number desc;
                """;
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)));
            }
        }
        foreach (var row in rows)
        {
            var items = new List<ProductionControlManufacturingItemResponse>();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select definition_key,display_order,label,step_role
                from production_control_manufacturing_items
                where template_version_id=@version_id
                order by display_order;
                """;
            command.Parameters.AddWithValue("version_id", row.Id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                items.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));
            versions.Add(new(row.Id, row.Number, row.Status, row.RowVersion, row.Activated, row.Archived, items));
        }
        return versions;
    }

    private static async Task<IReadOnlyList<ProductionControlPlanVersionResponse>> ReadPlanVersionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid productTypeId,
        CancellationToken cancellationToken)
    {
        var versions = new List<ProductionControlPlanVersionResponse>();
        var rows = new List<(Guid Id, int Number, string Status, int RowVersion, DateTimeOffset? Activated, DateTimeOffset? Archived)>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select version.id,version.version_number,version.lifecycle_status,version.row_version,
                       version.activated_at_utc,version.archived_at_utc
                from production_control_plan_templates template
                join production_control_plan_versions version on version.template_id=template.id
                where template.product_type_id=@product_type_id
                order by version.version_number desc;
                """;
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5)));
            }
        }
        foreach (var row in rows)
        {
            var items = new List<ProductionControlPlanTemplateItemResponse>();
            var itemRows = new List<(Guid Id, Guid Definition, int Order, string Label, bool Required)>();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    select id,definition_key,display_order,label,is_required
                    from production_control_plan_items
                    where template_version_id=@version_id
                    order by display_order;
                    """;
                command.Parameters.AddWithValue("version_id", row.Id);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    itemRows.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetString(3), reader.GetBoolean(4)));
            }
            foreach (var item in itemRows)
            {
                var connections = new List<ProductionControlConnectionResponse>();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    select source_code,source_definition_key
                    from production_control_plan_connections
                    where plan_item_id=@item_id
                    order by source_code,source_definition_key;
                    """;
                command.Parameters.AddWithValue("item_id", item.Id);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    connections.Add(new(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1)));
                items.Add(new(item.Definition, item.Order, item.Label, item.Required, connections));
            }
            versions.Add(new(row.Id, row.Number, row.Status, row.RowVersion, row.Activated, row.Archived, items));
        }
        return versions;
    }

    private static async Task<HashSet<string>> ReadDomainsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select distinct binding.domain
            from form_template_manager_bindings binding
            join qms_users actor on actor.id=binding.user_id
             and actor.department_id=binding.department_id
             and actor.is_active
            where binding.user_id=@user_id and binding.revoked_at_utc is null;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task DemandAccessAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid userId,
        bool isSystemAdministrator,
        string domain,
        CancellationToken cancellationToken)
    {
        if (isSystemAdministrator) return;
        var domains = await ReadDomainsAsync(connection, transaction, userId, cancellationToken);
        if (!domains.Contains(domain)) throw new ProductionControlTemplateForbiddenException();
    }

    private static async Task AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string action,
        string domain,
        string family,
        Guid productTypeId,
        Guid versionId,
        Guid actorUserId,
        object detail,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into form_template_audit_events (
                action,domain,family,template_key,version_id,actor_user_id,detail
            )
            values (@action,@domain,@family,@template_key,@version_id,@actor_id,@detail::jsonb);
            """;
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("domain", domain);
        command.Parameters.AddWithValue("family", family);
        command.Parameters.AddWithValue("template_key", productTypeId.ToString());
        command.Parameters.AddWithValue("version_id", versionId);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("detail", JsonSerializer.Serialize(detail));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        IReadOnlyList<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var value = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(value);
    }
}
