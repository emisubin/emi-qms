using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Admin;

public sealed class MaterialCategoryIqcStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<MaterialCategoryIqcTemplatesResponse> GetAsync(
        Guid userId, bool isSystemAdministrator, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await DemandManageAsync(connection, null, userId, isSystemAdministrator, cancellationToken);
        return new(true, await ReadAsync(connection, null, cancellationToken));
    }

    public async Task<MaterialCategoryIqcTemplatesResponse> UpdateSettingAsync(
        Guid materialCategoryId,
        UpdateMaterialCategoryIqcSettingRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        var decisionMode = NormalizeDecisionMode(request.DecisionMode);
        if (materialCategoryId == Guid.Empty || request.ExpectedRowVersion < 1)
            throw new ArgumentException("구매품 구분과 최신 설정 version을 확인해 주세요.", "materialCategoryId");

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await DemandManageAsync(connection, transaction, actorUserId, isSystemAdministrator, cancellationToken);
        var current = await LockAsync(connection, transaction, materialCategoryId, cancellationToken)
            ?? throw new ArgumentException("구매품 구분 IQC 설정을 찾을 수 없습니다.", "materialCategoryId");
        if (current.SettingRowVersion != request.ExpectedRowVersion)
            throw new FormTemplateConflictException("구매품 구분 IQC 설정이 변경되었습니다. 새로고침해 주세요.");

        if (request.IsEnabled && decisionMode == "Detailed"
            && await CountItemsAsync(connection, transaction, current.TemplateVersionId, cancellationToken) < 1)
        {
            throw new ArgumentException("상세형 검사를 켜려면 검사 항목을 1개 이상 먼저 저장해 주세요.", "isEnabled");
        }

        if (current.IsEnabled != request.IsEnabled || current.DecisionMode != decisionMode)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update material_category_iqc_settings
                set is_enabled=@is_enabled,decision_mode=@decision_mode,
                    row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
                where material_category_id=@category_id and row_version=@expected_row_version;
                """;
            command.Parameters.AddWithValue("is_enabled", request.IsEnabled);
            command.Parameters.AddWithValue("decision_mode", decisionMode);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("category_id", materialCategoryId);
            command.Parameters.AddWithValue("expected_row_version", request.ExpectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new FormTemplateConflictException("구매품 구분 IQC 설정이 변경되었습니다. 새로고침해 주세요.");

            await AppendAuditAsync(connection, transaction, materialCategoryId, "SettingChanged", actorUserId,
                new { isEnabled = current.IsEnabled, decisionMode = current.DecisionMode },
                new { isEnabled = request.IsEnabled, decisionMode }, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new(true, await ReadAsync(connection, null, cancellationToken));
    }

    public async Task<MaterialCategoryIqcTemplatesResponse> SaveTemplateAsync(
        Guid materialCategoryId,
        SaveMaterialCategoryIqcTemplateRequest request,
        Guid actorUserId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        ValidateItems(request.Items);
        if (materialCategoryId == Guid.Empty || request.ExpectedTemplateRowVersion < 1)
            throw new ArgumentException("구매품 구분과 최신 양식 version을 확인해 주세요.", "materialCategoryId");

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await DemandManageAsync(connection, transaction, actorUserId, isSystemAdministrator, cancellationToken);
        var current = await LockAsync(connection, transaction, materialCategoryId, cancellationToken)
            ?? throw new ArgumentException("구매품 구분 IQC 설정을 찾을 수 없습니다.", "materialCategoryId");
        if (current.TemplateRowVersion != request.ExpectedTemplateRowVersion)
            throw new FormTemplateConflictException("구매품별 IQC 검사 양식이 변경되었습니다. 새로고침해 주세요.");
        if (current.IsEnabled && current.DecisionMode == "Detailed" && request.Items.Count < 1)
            throw new ArgumentException("운영 중인 상세형 검사는 항목을 1개 이상 유지해야 합니다.", "items");

        var existingDefinitionKeys = await ReadDefinitionKeysAsync(
            connection, transaction, current.TemplateVersionId, cancellationToken);
        ValidateDefinitionKeys(request.Items, existingDefinitionKeys);

        int nextVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select coalesce(max(version.version_number),0)+1
                from iqc_report_template_versions version
                join iqc_report_templates template on template.id=version.template_id
                where template.material_category_id=@category_id;
                """;
            command.Parameters.AddWithValue("category_id", materialCategoryId);
            nextVersion = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }

        var nextVersionId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into iqc_report_template_versions (
                    id,template_id,version_number,is_active,lifecycle_status,row_version,
                    created_by_user_id,updated_by_user_id
                )
                select @next_version_id,template_id,@next_version,false,'Draft',1,@actor_id,@actor_id
                from iqc_report_template_versions where id=@current_version_id;
                """;
            command.Parameters.AddWithValue("next_version_id", nextVersionId);
            command.Parameters.AddWithValue("next_version", nextVersion);
            command.Parameters.AddWithValue("actor_id", actorUserId);
            command.Parameters.AddWithValue("current_version_id", current.TemplateVersionId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new ArgumentException("현재 구매품별 IQC 양식을 찾을 수 없습니다.", "materialCategoryId");
        }

        foreach (var item in request.Items.OrderBy(item => item.DisplayOrder))
            await InsertItemAsync(connection, transaction, nextVersionId, item, cancellationToken);

        await using (var archive = connection.CreateCommand())
        {
            archive.Transaction = transaction;
            archive.CommandText = """
                update iqc_report_template_versions
                set lifecycle_status='Archived',is_active=false,archived_at_utc=now(),
                    row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
                where id=@current_version_id and lifecycle_status='Active'
                  and row_version=@expected_row_version;
                """;
            archive.Parameters.AddWithValue("actor_id", actorUserId);
            archive.Parameters.AddWithValue("current_version_id", current.TemplateVersionId);
            archive.Parameters.AddWithValue("expected_row_version", request.ExpectedTemplateRowVersion);
            if (await archive.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new FormTemplateConflictException("구매품별 IQC 검사 양식이 변경되었습니다. 새로고침해 주세요.");
        }

        await using (var activate = connection.CreateCommand())
        {
            activate.Transaction = transaction;
            activate.CommandText = """
                update iqc_report_template_versions
                set lifecycle_status='Active',is_active=true,activated_at_utc=now(),
                    row_version=row_version+1,updated_by_user_id=@actor_id,updated_at_utc=now()
                where id=@next_version_id and lifecycle_status='Draft' and row_version=1;
                """;
            activate.Parameters.AddWithValue("actor_id", actorUserId);
            activate.Parameters.AddWithValue("next_version_id", nextVersionId);
            if (await activate.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new FormTemplateConflictException("새 구매품별 IQC 검사 양식을 적용하지 못했습니다.");
        }

        await using (var setting = connection.CreateCommand())
        {
            setting.Transaction = transaction;
            setting.CommandText = """
                update material_category_iqc_settings
                set current_template_version_id=@next_version_id,row_version=row_version+1,
                    updated_by_user_id=@actor_id,updated_at_utc=now()
                where material_category_id=@category_id and row_version=@expected_setting_row_version;
                """;
            setting.Parameters.AddWithValue("next_version_id", nextVersionId);
            setting.Parameters.AddWithValue("actor_id", actorUserId);
            setting.Parameters.AddWithValue("category_id", materialCategoryId);
            setting.Parameters.AddWithValue("expected_setting_row_version", current.SettingRowVersion);
            if (await setting.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new FormTemplateConflictException("구매품 구분 IQC 설정이 변경되었습니다. 새로고침해 주세요.");
        }

        await AppendAuditAsync(connection, transaction, materialCategoryId, "TemplateChanged", actorUserId,
            new { templateVersionNumber = current.TemplateVersionNumber },
            new { templateVersionNumber = nextVersion, itemCount = request.Items.Count }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, await ReadAsync(connection, null, cancellationToken));
    }

    private static async Task InsertItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid versionId,
        SaveFormTemplateItemRequest item,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into iqc_report_template_items (
                id,template_version_id,item_code,display_order,label,guidance,response_type,
                is_required,requires_photo,max_text_length,definition_key
            ) values (
                uuid_generate_v4(),@version_id,@item_code,@display_order,@label,@guidance,@response_type,
                @is_required,@requires_photo,@max_text_length,@definition_key
            );
            """;
        command.Parameters.AddWithValue("version_id", versionId);
        command.Parameters.AddWithValue("item_code", item.ItemCode.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("display_order", item.DisplayOrder);
        command.Parameters.AddWithValue("label", item.Label.Trim());
        command.Parameters.Add("guidance", NpgsqlDbType.Text).Value =
            string.IsNullOrWhiteSpace(item.Guidance) ? DBNull.Value : item.Guidance.Trim();
        command.Parameters.AddWithValue("response_type", item.ResponseType);
        command.Parameters.AddWithValue("is_required", item.IsRequired);
        command.Parameters.AddWithValue("requires_photo", item.RequiresPhoto);
        command.Parameters.Add("max_text_length", NpgsqlDbType.Integer).Value =
            item.ResponseType == "Text" ? item.MaxTextLength ?? 1000 : DBNull.Value;
        command.Parameters.AddWithValue("definition_key", item.DefinitionKey ?? Guid.NewGuid());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<MaterialCategoryIqcTemplateResponse>> ReadAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        var rows = new List<SettingSnapshot>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select category.id,category.code,category.display_name,category.is_active,
                       setting.is_enabled,setting.decision_mode,setting.row_version,
                       version.id,version.version_number,version.row_version
                from material_categories category
                join material_category_iqc_settings setting on setting.material_category_id=category.id
                join iqc_report_template_versions version on version.id=setting.current_template_version_id
                order by category.is_active desc,category.display_order,category.display_name,category.id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) rows.Add(MapSetting(reader));
        }

        var result = new List<MaterialCategoryIqcTemplateResponse>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new(
                row.CategoryId, row.Code, row.Name, row.IsCategoryActive, row.IsEnabled,
                row.DecisionMode, row.SettingRowVersion, row.TemplateVersionId,
                row.TemplateVersionNumber, row.TemplateRowVersion,
                await ReadItemsAsync(connection, transaction, row.TemplateVersionId, cancellationToken)));
        }
        return result;
    }

    private static async Task<SettingSnapshot?> LockAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid categoryId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select category.id,category.code,category.display_name,category.is_active,
                   setting.is_enabled,setting.decision_mode,setting.row_version,
                   version.id,version.version_number,version.row_version
            from material_categories category
            join material_category_iqc_settings setting on setting.material_category_id=category.id
            join iqc_report_template_versions version on version.id=setting.current_template_version_id
            where category.id=@category_id
            for update of setting,version;
            """;
        command.Parameters.AddWithValue("category_id", categoryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapSetting(reader) : null;
    }

    private static SettingSnapshot MapSetting(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3),
        reader.GetBoolean(4), reader.GetString(5), reader.GetInt32(6), reader.GetGuid(7),
        reader.GetInt32(8), reader.GetInt32(9));

    private static async Task<IReadOnlyList<FormTemplateItemResponse>> ReadItemsAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid versionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,item_code,display_order,label,guidance,response_type,is_required,
                   requires_photo,max_text_length,definition_key
            from iqc_report_template_items where template_version_id=@version_id order by display_order;
            """;
        command.Parameters.AddWithValue("version_id", versionId);
        var result = new List<FormTemplateItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new(
                reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetBoolean(6),
                reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.GetGuid(9)));
        }
        return result;
    }

    private static async Task<HashSet<Guid>> ReadDefinitionKeysAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid versionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select definition_key from iqc_report_template_items where template_version_id=@version_id;";
        command.Parameters.AddWithValue("version_id", versionId);
        var result = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0));
        return result;
    }

    private static void ValidateDefinitionKeys(
        IReadOnlyList<SaveFormTemplateItemRequest> items, IReadOnlySet<Guid> existingKeys)
    {
        var submitted = items.Where(item => item.DefinitionKey is not null)
            .Select(item => item.DefinitionKey!.Value).ToArray();
        if (submitted.Distinct().Count() != submitted.Length)
            throw new ArgumentException("검사 항목 고유번호는 중복될 수 없습니다.", "items");
        if (submitted.Any(key => !existingKeys.Contains(key)))
            throw new ArgumentException("검사 항목 고유번호를 임의로 변경할 수 없습니다.", "items");
    }

    private static void ValidateItems(IReadOnlyList<SaveFormTemplateItemRequest> items)
    {
        if (items.Count > 50) throw new ArgumentException("항목은 최대 50개까지 등록할 수 있습니다.", "items");
        if (items.Select(item => item.ItemCode.Trim().ToUpperInvariant()).Distinct().Count() != items.Count
            || items.Select(item => item.DisplayOrder).Distinct().Count() != items.Count)
            throw new ArgumentException("항목 코드와 순서는 중복될 수 없습니다.", "items");
        foreach (var item in items)
        {
            if (item.DisplayOrder is < 1 or > 50)
                throw new ArgumentException("항목 순서를 확인해 주세요.", "items");
            if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Trim().Length > 200)
                throw new ArgumentException("항목명은 1자부터 200자까지 입력해 주세요.", "items");
            if (item.ResponseType is not ("Check" or "Text"))
                throw new ArgumentException("지원하지 않는 응답 형식입니다.", "items");
            if (item.RequiresPhoto && item.ResponseType != "Check")
                throw new ArgumentException("사진 필수는 확인형 항목에만 설정할 수 있습니다.", "items");
        }
    }

    private static string NormalizeDecisionMode(string? value) => value switch
    {
        "ScanBased" => "ScanBased",
        "Detailed" => "Detailed",
        _ => throw new ArgumentException("검사 방식은 스캔형 또는 상세형이어야 합니다.", "decisionMode")
    };

    private static async Task<int> CountItemsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid versionId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select count(*)::int from iqc_report_template_items where template_version_id=@version_id;";
        command.Parameters.AddWithValue("version_id", versionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task DemandManageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid userId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        if (isSystemAdministrator) return;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from form_template_manager_bindings binding
                join qms_users actor on actor.id=binding.user_id
                    and actor.department_id=binding.department_id and actor.is_active
                join departments department on department.id=binding.department_id
                    and department.code='quality'
                where binding.user_id=@user_id and binding.domain='Quality'
                  and binding.revoked_at_utc is null
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
            throw new FormTemplateForbiddenException();
    }

    private static async Task AppendAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid categoryId,
        string action,
        Guid actorUserId,
        object oldValue,
        object newValue,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into material_category_iqc_setting_audit_events (
                material_category_id,action,actor_user_id,old_value,new_value
            ) values (@category_id,@action,@actor_id,@old_value::jsonb,@new_value::jsonb);
            """;
        command.Parameters.AddWithValue("category_id", categoryId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        command.Parameters.AddWithValue("old_value", JsonSerializer.Serialize(oldValue));
        command.Parameters.AddWithValue("new_value", JsonSerializer.Serialize(newValue));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }

    private sealed record SettingSnapshot(
        Guid CategoryId, string Code, string Name, bool IsCategoryActive, bool IsEnabled,
        string DecisionMode, int SettingRowVersion, Guid TemplateVersionId,
        int TemplateVersionNumber, int TemplateRowVersion);
}
