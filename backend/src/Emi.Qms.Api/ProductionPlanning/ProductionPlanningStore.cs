using ClosedXML.Excel;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.ProductionPlanning;

public sealed class ProductionPlanningStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<ProductionPlanningSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            with active_projects as (
                select p.id
                from projects p
                where p.deleted_at_utc is null
                  and p.status = 'Active'
            ),
            project_steps as (
                select ap.id as project_id,
                       pp.product_type_id,
                       coalesce(count(pi.id) filter (where pi.is_required), 0)::int as required_count,
                       coalesce(count(pi.id) filter (where pi.is_required and pi.planned_date is not null), 0)::int as planned_required_count
                from active_projects ap
                left join project_production_plans pp on pp.project_id = ap.id
                left join project_production_plan_items pi on pi.production_plan_id = pp.id
                group by ap.id, pp.product_type_id
            ),
            assignee_summary as (
                select ap.id as project_id,
                       coalesce(count(pa.id) filter (where pa.assigned_user_id is not null), 0)::int as assigned_count
                from active_projects ap
                left join project_assignees pa on pa.project_id = ap.id
                group by ap.id
            )
            select
                count(*) filter (where ps.product_type_id is null)::int,
                count(*) filter (where ps.product_type_id is not null and (ps.required_count = 0 or ps.planned_required_count < ps.required_count))::int,
                count(*) filter (where ps.product_type_id is not null and ps.required_count > 0 and ps.planned_required_count = ps.required_count)::int,
                count(*) filter (where coalesce(a.assigned_count, 0) < 5)::int
            from project_steps ps
            left join assignee_summary a on a.project_id = ps.project_id;
            """);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProductionPlanningSummaryResponse(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3))
            : new ProductionPlanningSummaryResponse(0, 0, 0, 0);
    }

    public async Task<ProductionPlanningProjectListResponse> ListProjectsAsync(string? search, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand($"""
            with plan_summary as (
                select pp.project_id,
                       pp.product_type_id,
                       pt.code as product_type_code,
                       pt.name as product_type_name,
                       coalesce(count(pi.id) filter (where pi.is_required), 0)::int as required_count,
                       coalesce(count(pi.id) filter (where pi.is_required and pi.planned_date is not null), 0)::int as planned_required_count
                from project_production_plans pp
                left join production_product_types pt on pt.id = pp.product_type_id
                left join project_production_plan_items pi on pi.production_plan_id = pp.id
                group by pp.project_id, pp.product_type_id, pt.code, pt.name
            ),
            assignee_summary as (
                select project_id,
                       count(*) filter (where assigned_user_id is not null)::int as assignee_count
                from project_assignees
                group by project_id
            ),
            panels as (
                select project_id, count(*)::int as active_panel_count
                from panel_placeholders
                where status = 'Active'
                group by project_id
            )
            select p.id,
                   coalesce(p.project_title, p.name, ''),
                   coalesce(p.customer_name, ''),
                   coalesce(p.project_code, p.project_number, ''),
                   coalesce(p.item, ''),
                   coalesce(panels.active_panel_count, 0),
                   p.delivery_date,
                   p.status,
                   ps.product_type_id,
                   ps.product_type_code,
                   ps.product_type_name,
                   coalesce(ps.required_count, 0),
                   coalesce(ps.planned_required_count, 0),
                   coalesce(a.assignee_count, 0)
            from projects p
            left join panels on panels.project_id = p.id
            left join plan_summary ps on ps.project_id = p.id
            left join assignee_summary a on a.project_id = p.id
            where p.deleted_at_utc is null
              and (@search = '' or p.project_title ilike @search_like or p.project_code ilike @search_like or p.customer_name ilike @search_like or p.item ilike @search_like)
            order by p.delivery_date asc nulls last, p.created_at_utc desc;
            """);
        var searchValue = search?.Trim() ?? "";
        command.Parameters.AddWithValue("search", searchValue);
        command.Parameters.AddWithValue("search_like", $"%{searchValue}%");

        var projects = new List<ProductionPlanningProjectSummaryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var productTypeId = reader.IsDBNull(8) ? (Guid?)null : reader.GetGuid(8);
            var requiredCount = reader.GetInt32(11);
            var plannedRequiredCount = reader.GetInt32(12);
            var status = CalculateStatus(productTypeId, requiredCount, plannedRequiredCount);
            projects.Add(new ProductionPlanningProjectSummaryResponse
            {
                ProjectId = reader.GetGuid(0),
                ProjectTitle = reader.GetString(1),
                CustomerName = reader.GetString(2),
                ProjectCode = reader.GetString(3),
                Item = reader.GetString(4),
                ActivePanelCount = reader.GetInt32(5),
                DeliveryDate = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                ProjectStatus = reader.GetString(7),
                PlanStatus = status,
                PlanStatusLabel = ProductionPlanningDomain.StatusLabel(status),
                ProductTypeCode = reader.IsDBNull(9) ? null : reader.GetString(9),
                ProductTypeName = reader.IsDBNull(10) ? null : reader.GetString(10),
                RequiredStepCount = requiredCount,
                PlannedRequiredStepCount = plannedRequiredCount,
                AssigneeCount = reader.GetInt32(13)
            });
        }

        return new ProductionPlanningProjectListResponse(projects);
    }

    public async Task<ProductionPlanningResponse?> GetProjectPlanAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await ReadProjectAsync(connection, null, projectId, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var plan = await ReadPlanHeaderAsync(connection, null, projectId, cancellationToken);
        var items = plan is null ? [] : await ReadPlanItemsAsync(connection, null, plan.PlanId, cancellationToken);
        var assignees = await ReadAssigneesAsync(connection, null, projectId, cancellationToken);
        var candidates = await ReadAssigneeCandidatesAsync(connection, null, cancellationToken);
        var fallbacks = await BuildFallbacksAsync(connection, null, project, assignees, cancellationToken);
        return BuildResponse(project, plan, items, assignees, candidates, fallbacks);
    }

    public async Task<IReadOnlyList<ProductionProductTypeResponse>> ListProductTypesAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadProductTypesAsync(connection, null, cancellationToken);
    }

    public async Task<ProductionPlanningMutationResult<IReadOnlyList<ProductionProductTypeResponse>>> CreateProductTypeAsync(
        UpsertProductionProductTypeRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateProductType(request);
        if (errors.Count > 0)
        {
            return ProductionPlanningMutationResult<IReadOnlyList<ProductionProductTypeResponse>>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var productTypeId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into production_product_types (id, code, name)
                values (@id, @code, @name);

                insert into production_plan_templates (id, product_type_id, version, is_active)
                values (@template_id, @id, 1, true);
                """;
            command.Parameters.AddWithValue("id", productTypeId);
            command.Parameters.AddWithValue("code", request.Code!.Trim());
            command.Parameters.AddWithValue("name", request.Name!.Trim());
            command.Parameters.AddWithValue("template_id", templateId);
            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ProductionPlanningMutationResult<IReadOnlyList<ProductionProductTypeResponse>>.Conflict("이미 사용 중인 Item 코드입니다.");
            }
        }

        foreach (var step in request.Steps!.OrderBy(step => step.SequenceNumber!.Value))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into production_plan_template_steps (template_id, sequence_number, step_name, is_required)
                values (@template_id, @sequence_number, @step_name, @is_required);
                """;
            command.Parameters.AddWithValue("template_id", templateId);
            command.Parameters.AddWithValue("sequence_number", step.SequenceNumber!.Value);
            command.Parameters.AddWithValue("step_name", step.StepName!.Trim());
            command.Parameters.AddWithValue("is_required", step.IsRequired ?? true);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<IReadOnlyList<ProductionProductTypeResponse>>.Success(await ReadProductTypesAsync(connection, null, cancellationToken));
    }

    public async Task<IReadOnlyList<ProductionTemplateSettingsResponse>> ListTemplateSettingsAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadTemplateSettingsAsync(connection, null, cancellationToken);
    }

    public async Task<ProductionPlanningMutationResult<IReadOnlyList<ProductionTemplateSettingsResponse>>> UpdateTemplateSettingsAsync(
        Guid productTypeId,
        UpdateProductionTemplateSettingsRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateTemplateSettings(request);
        if (errors.Count > 0)
        {
            return ProductionPlanningMutationResult<IReadOnlyList<ProductionTemplateSettingsResponse>>.Validation(errors);
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var current = await ReadTemplateSettingsAsync(connection, transaction, cancellationToken);
        var productType = current.FirstOrDefault(item => item.ProductTypeId == productTypeId);
        if (productType is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProductionPlanningMutationResult<IReadOnlyList<ProductionTemplateSettingsResponse>>.NotFound();
        }

        var nextVersion = productType.ActiveTemplateVersion + 1;
        var newTemplateId = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update production_plan_templates
                set is_active = false
                where product_type_id = @product_type_id
                  and is_active = true;

                insert into production_plan_templates (id, product_type_id, version, is_active)
                values (@template_id, @product_type_id, @version, true);
                """;
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            command.Parameters.AddWithValue("template_id", newTemplateId);
            command.Parameters.AddWithValue("version", nextVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var step in request.Steps!.OrderBy(item => item.SequenceNumber!.Value))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into production_plan_template_steps (template_id, sequence_number, step_name, is_required, is_active)
                values (@template_id, @sequence_number, @step_name, @is_required, @is_active);
                """;
            command.Parameters.AddWithValue("template_id", newTemplateId);
            command.Parameters.AddWithValue("sequence_number", step.SequenceNumber!.Value);
            command.Parameters.AddWithValue("step_name", step.StepName!.Trim());
            command.Parameters.AddWithValue("is_required", step.IsRequired ?? true);
            command.Parameters.AddWithValue("is_active", step.IsActive ?? true);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertTemplateSettingsAuditAsync(connection, transaction, productType, request, changedByUserId, correlationId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<IReadOnlyList<ProductionTemplateSettingsResponse>>.Success(await ReadTemplateSettingsAsync(connection, null, cancellationToken));
    }

    public async Task<ProductionPlanningMutationResult<ProductionPlanningResponse>> UpdateProjectPlanAsync(
        Guid projectId,
        UpdateProductionPlanningRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var project = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
        if (project is null)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.NotFound();
        }

        if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("현재 프로젝트 상태에서는 생산계획을 수정할 수 없습니다.");
        }

        var currentPlan = await ReadPlanHeaderAsync(connection, transaction, projectId, cancellationToken);
        if (currentPlan is not null && request.ExpectedRowVersion is not null && currentPlan.RowVersion != request.ExpectedRowVersion)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.");
        }

        var planId = currentPlan?.PlanId ?? Guid.NewGuid();
        ProductTypeSnapshot? productType = null;
        IReadOnlyList<ProductionPlanItemResponse> existing = [];
        IReadOnlyList<ProductionTemplateStepResponse> templateSteps;

        if (currentPlan is null)
        {
            productType = await ReadActiveProductTypeByCodeAsync(connection, transaction, project.Item, cancellationToken);
            if (productType is null)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.ProductTypeId)] = ["현재 프로젝트의 Item이 등록된 Item 기준값과 일치하지 않습니다. 프로젝트 정보를 수정한 후 생산계획을 입력해 주세요."] });
            }

            if (request.ProductTypeId is not null && request.ProductTypeId.Value != productType.ProductTypeId)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.ProductTypeId)] = ["프로젝트 Item과 선택 기준값이 일치하지 않습니다. 프로젝트 정보를 다시 확인해 주세요."] });
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into project_production_plans (
                    id, project_id, product_type_id, template_id, notes, created_by_user_id, updated_by_user_id
                )
                values (@id, @project_id, @product_type_id, @template_id, @notes, @user_id, @user_id);
                """;
            command.Parameters.AddWithValue("id", planId);
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.Add("product_type_id", NpgsqlDbType.Uuid).Value = productType.ProductTypeId;
            command.Parameters.Add("template_id", NpgsqlDbType.Uuid).Value = productType.TemplateId;
            command.Parameters.Add("notes", NpgsqlDbType.Text).Value = TrimToNull(request.Notes) ?? (object)DBNull.Value;
            command.Parameters.AddWithValue("user_id", changedByUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await InsertAuditAsync(connection, transaction, projectId, planId, "ProductionPlan", "ProductTypeId", null, productType.ProductTypeCode, request.Reason, changedByUserId, correlationId, cancellationToken);
            templateSteps = await ReadTemplateStepsAsync(connection, transaction, productType.TemplateId, cancellationToken);
        }
        else
        {
            if (currentPlan.ProductTypeId is null || currentPlan.TemplateId is null)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.ProductTypeId)] = ["기존 생산계획의 Item snapshot을 확인할 수 없습니다. 관리자에게 문의해 주세요."] });
            }

            if (request.ProductTypeId is not null && request.ProductTypeId.Value != currentPlan.ProductTypeId.Value)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.ProductTypeId)] = ["기존 생산계획의 Item snapshot과 일치하지 않습니다. 기존 생산계획은 자동으로 최신 template으로 변경되지 않습니다."] });
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update project_production_plans
                set notes = @notes,
                    row_version = row_version + 1,
                    updated_at_utc = now(),
                    updated_by_user_id = @user_id
                where id = @id;
                """;
            command.Parameters.AddWithValue("id", planId);
            command.Parameters.Add("notes", NpgsqlDbType.Text).Value = TrimToNull(request.Notes) ?? (object)DBNull.Value;
            command.Parameters.AddWithValue("user_id", changedByUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            existing = await ReadPlanItemsAsync(connection, transaction, planId, cancellationToken);
            templateSteps = existing
                .Where(item => item.TemplateStepId is not null)
                .OrderBy(item => item.SequenceNumber)
                .Select(item => new ProductionTemplateStepResponse(
                    item.TemplateStepId!.Value,
                    item.SequenceNumber,
                    item.StepName,
                    item.IsRequired))
                .ToList();
            if (templateSteps.Count == 0)
            {
                templateSteps = await ReadTemplateStepsAsync(connection, transaction, currentPlan.TemplateId.Value, cancellationToken);
            }
        }

        {
            var validationErrors = ValidatePlanItemUpdates(request.Items ?? [], templateSteps);
            if (validationErrors.Count > 0)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(validationErrors);
            }

            var requestedTemplateItems = (request.Items ?? [])
                .Where(item => item.TemplateStepId is not null)
                .ToDictionary(item => item.TemplateStepId!.Value, item => item);
            var requestedCustomItems = (request.Items ?? [])
                .Where(item => item.TemplateStepId is null)
                .ToList();
            existing = currentPlan is null ? [] : existing;

            foreach (var step in templateSteps)
            {
                requestedTemplateItems.TryGetValue(step.TemplateStepId, out var requestedItem);
                var current = existing.FirstOrDefault(item => item.TemplateStepId == step.TemplateStepId);
                var plannedDate = requestedItem?.PlannedDate;
                var note = TrimToNull(requestedItem?.Note);
                if (current is null)
                {
                    if (currentPlan is not null)
                    {
                        continue;
                    }

                    var itemId = Guid.NewGuid();
                    await InsertTemplatePlanItemAsync(connection, transaction, itemId, planId, step, plannedDate, note, cancellationToken);
                    if (plannedDate is not null)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem", step.StepName, null, plannedDate.Value.ToString("yyyy-MM-dd"), request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                }
                else
                {
                    if (requestedItem?.ExpectedRowVersion is not null && current.RowVersion != requestedItem.ExpectedRowVersion)
                    {
                        return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.");
                    }

                    if (current.PlannedDate != plannedDate || current.Note != note)
                    {
                        await UpdatePlanItemAsync(connection, transaction, current.ItemId!.Value, plannedDate, note, cancellationToken);
                        if (current.PlannedDate != plannedDate)
                        {
                            await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", step.StepName, FormatDate(current.PlannedDate), FormatDate(plannedDate), request.Reason, changedByUserId, correlationId, cancellationToken);
                        }
                        if (current.Note != note)
                        {
                            await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", $"{step.StepName} 비고", current.Note, note, request.Reason, changedByUserId, correlationId, cancellationToken);
                        }
                    }
                }
            }

            var customSequence = existing.Count == 0 ? templateSteps.Count + 1 : existing.Max(item => item.SequenceNumber) + 1;
            foreach (var requestedItem in requestedCustomItems)
            {
                var stepName = TrimToNull(requestedItem.StepName)!;
                var current = requestedItem.ItemId is null
                    ? null
                    : existing.FirstOrDefault(item => item.ItemId == requestedItem.ItemId && item.TemplateStepId is null);
                if (current is not null && requestedItem.ExpectedRowVersion is not null && current.RowVersion != requestedItem.ExpectedRowVersion)
                {
                    return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.");
                }

                if (requestedItem.IsDeleted == true)
                {
                    if (current is null)
                    {
                        continue;
                    }

                    await DeactivateCustomPlanItemAsync(connection, transaction, current.ItemId!.Value, cancellationToken);
                    await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", "계획 항목 삭제", current.StepName, null, request.Reason, changedByUserId, correlationId, cancellationToken);
                    continue;
                }

                var plannedDate = requestedItem.PlannedDate;
                var note = TrimToNull(requestedItem.Note);
                if (current is null)
                {
                    var itemId = Guid.NewGuid();
                    await InsertCustomPlanItemAsync(connection, transaction, itemId, planId, customSequence++, stepName, plannedDate, note, cancellationToken);
                    await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem", "사용자 추가 항목", null, stepName, request.Reason, changedByUserId, correlationId, cancellationToken);
                    if (plannedDate is not null)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem", stepName, null, plannedDate.Value.ToString("yyyy-MM-dd"), request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                }
                else if (current.StepName != stepName || current.PlannedDate != plannedDate || current.Note != note || current.SequenceNumber != requestedItem.SequenceNumber)
                {
                    await UpdateCustomPlanItemAsync(connection, transaction, current.ItemId!.Value, requestedItem.SequenceNumber ?? customSequence++, stepName, plannedDate, note, cancellationToken);
                    if (current.StepName != stepName)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", "계획 항목명", current.StepName, stepName, request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                    if (current.PlannedDate != plannedDate)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", stepName, FormatDate(current.PlannedDate), FormatDate(plannedDate), request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                    if (current.Note != note)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", $"{stepName} 비고", current.Note, note, request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                }
            }
        }

        var assigneeResult = await UpdateAssigneesAsync(connection, transaction, projectId, request, changedByUserId, correlationId, cancellationToken);
        if (assigneeResult is not null)
        {
            return assigneeResult;
        }

        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<ProductionPlanningResponse>.Success((await GetProjectPlanAsync(projectId, cancellationToken))!);
    }

    public Task<ProductionPlanningTemplateDownload> CreateBulkTemplateAsync(CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Production Planning");
        var headers = BulkExcelHeaders;
        var requiredColumns = new HashSet<int> { 1, 2, 3, 4 };
        for (var index = 0; index < headers.Length; index++)
        {
            var column = index + 1;
            worksheet.Cell(1, column).Value = requiredColumns.Contains(column) ? $"{headers[index]} *" : headers[index];
            worksheet.Cell(1, column).Style.Font.Bold = true;
            if (requiredColumns.Contains(column))
            {
                worksheet.Cell(1, column).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }
        worksheet.Cell(1, headers.Length + 1).Value = "* 표시 항목은 필수 입력값입니다. 프로젝트명 또는 PJT Code 중 하나는 필수입니다.";
        worksheet.Cell(1, headers.Length + 1).Style.Font.Italic = true;
        worksheet.Cell(1, headers.Length + 1).Style.Alignment.WrapText = true;
        worksheet.Column(headers.Length + 1).Width = 42;

        var example = new[] { "UAT-PLAN", "PLAN-CODE", "UL67", "자재 입고", "2026-07-01", "예시", "dev-procurement", "dev-production", "dev-manufacturing", "dev-quality", "dev-logistics" };
        for (var index = 0; index < example.Length; index++)
        {
            worksheet.Cell(2, index + 1).Value = example[index];
        }
        ApplyExcelTemplateLayout(worksheet, headers.Length, headerRow: 1, wideColumns: [4, 6]);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var download = new ProductionPlanningTemplateDownload(
            stream.ToArray(),
            "Production_Planning_Bulk_Template.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return Task.FromResult(download);
    }

    public async Task<ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>> PreviewBulkExcelAsync(
        string fileName,
        byte[] bytes,
        string fileSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dataSource = CreateDataSource();
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var rows = await ParseBulkExcelRowsAsync(connection, null, bytes, cancellationToken);
            return ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>.Success(BuildExcelPreview(fileSha256, rows));
        }
        catch (InvalidDataException ex)
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>.Validation(new Dictionary<string, string[]> { ["file"] = [ex.Message] });
        }
    }

    public async Task<ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>> ApplyBulkExcelAsync(
        string fileName,
        byte[] bytes,
        string fileSha256,
        string expectedFileSha256,
        string? reason,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(fileSha256, expectedFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                new Dictionary<string, string[]> { ["file"] = ["파일이 변경되었습니다. 다시 미리보기를 실행해 주세요."] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var parsedRows = await ParseBulkExcelRowsAsync(connection, null, bytes, cancellationToken);
        var saveable = parsedRows.Where(row => row.IsSaveable).ToList();
        if (saveable.Count == 0)
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                new Dictionary<string, string[]> { ["rows"] = ["저장 가능한 생산계획 항목이 없습니다."] });
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var appliedProjectIds = new HashSet<Guid>();
        foreach (var row in saveable)
        {
            var project = await LockProjectAsync(connection, transaction, row.ProjectId!.Value, cancellationToken);
            if (project is null || !string.Equals(project.Status, "Active", StringComparison.Ordinal))
            {
                continue;
            }

            if (!ItemCodesEqual(project.Item, row.ProductTypeCode))
            {
                return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                    new Dictionary<string, string[]> { ["rows"] = ["Excel의 Item이 프로젝트 Item과 일치하지 않습니다."] });
            }

            var productType = await ReadActiveProductTypeByCodeAsync(connection, transaction, project.Item, cancellationToken);
            if (productType is null)
            {
                return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                    new Dictionary<string, string[]> { ["rows"] = ["현재 프로젝트의 Item이 등록된 Item 기준값과 일치하지 않습니다. 프로젝트 정보를 수정한 후 생산계획을 입력해 주세요."] });
            }

            var (planId, createdPlan) = await EnsurePlanForExcelAsync(connection, transaction, project.ProjectId, productType, changedByUserId, cancellationToken);
            var existing = await ReadPlanItemsAsync(connection, transaction, planId, cancellationToken);
            var current = createdPlan && row.TemplateStepId is not null
                ? existing.FirstOrDefault(item => item.TemplateStepId == row.TemplateStepId)
                : existing.FirstOrDefault(item => string.Equals(Normalize(row.StepName), Normalize(item.StepName), StringComparison.Ordinal));

            if (current is null)
            {
                var nextSequence = existing.Count == 0 ? 1 : existing.Max(item => item.SequenceNumber) + 1;
                var itemId = Guid.NewGuid();
                if (createdPlan && row.TemplateStepId is not null)
                {
                    await InsertTemplatePlanItemAsync(
                        connection,
                        transaction,
                        itemId,
                        planId,
                        new ProductionTemplateStepResponse(row.TemplateStepId.Value, nextSequence, row.StepName!, true),
                        row.PlannedDate,
                        row.Note,
                        cancellationToken);
                }
                else
                {
                    await InsertCustomPlanItemAsync(connection, transaction, itemId, planId, nextSequence, row.StepName!, row.PlannedDate, row.Note, cancellationToken);
                }
                await InsertAuditAsync(connection, transaction, project.ProjectId, itemId, "ProductionPlanItem", row.StepName!, null, FormatDate(row.PlannedDate), reason, changedByUserId, correlationId, cancellationToken, "Excel");
            }
            else if (current.PlannedDate != row.PlannedDate || current.Note != row.Note)
            {
                await UpdatePlanItemAsync(connection, transaction, current.ItemId!.Value, row.PlannedDate, row.Note, cancellationToken);
                await InsertAuditAsync(connection, transaction, project.ProjectId, current.ItemId.Value, "ProductionPlanItem", row.StepName!, FormatDate(current.PlannedDate), FormatDate(row.PlannedDate), reason, changedByUserId, correlationId, cancellationToken, "Excel");
            }

            await ApplyAssigneesFromExcelAsync(connection, transaction, project.ProjectId, row, changedByUserId, reason, correlationId, cancellationToken);
            appliedProjectIds.Add(project.ProjectId);
        }

        await InsertProductionPlanningImportBatchAsync(connection, transaction, fileName, bytes.Length, fileSha256, parsedRows.Count, saveable.Count, parsedRows.Count - saveable.Count, changedByUserId, reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Success(
            new ProductionPlanningExcelApplyResponse(saveable.Count, parsedRows.Count - saveable.Count, appliedProjectIds.ToList()));
    }

    public async Task<ProductionPlanningTemplateDownload?> CreateTemplateAsync(Guid projectId, Guid productTypeId, CancellationToken cancellationToken)
    {
        var plan = await GetProjectPlanAsync(projectId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        var type = (await ListProductTypesAsync(cancellationToken)).FirstOrDefault(item => item.ProductTypeId == productTypeId);
        if (type is null)
        {
            return null;
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Production Plan");
        worksheet.Cell(1, 1).Value = "생산계획 입력 양식";
        worksheet.Cell(2, 1).Value = plan.ProjectTitle;
        worksheet.Cell(2, 2).Value = "* 표시 항목은 필수 입력값입니다.";
        worksheet.Cell(2, 2).Style.Font.Italic = true;
        worksheet.Cell(3, 1).Value = "계획 항목 *";
        worksheet.Cell(3, 2).Value = "예정일";
        worksheet.Cell(3, 3).Value = "비고";
        worksheet.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
        var row = 4;
        foreach (var step in type.Steps.OrderBy(step => step.SequenceNumber))
        {
            worksheet.Cell(row, 1).Value = step.StepName;
            worksheet.Cell(row, 2).Style.DateFormat.Format = "yyyy-mm-dd";
            worksheet.Cell(row, 3).Value = "";
            row++;
        }

        ApplyExcelTemplateLayout(worksheet, 3, headerRow: 3, wideColumns: [1, 3]);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ProductionPlanningTemplateDownload(
            stream.ToArray(),
            "Production_Plan_Template.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static void ApplyExcelTemplateLayout(IXLWorksheet worksheet, int columnCount, int headerRow, IReadOnlyCollection<int> wideColumns)
    {
        worksheet.Row(headerRow).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(headerRow);
        var lastRow = Math.Max(worksheet.LastRowUsed()?.RowNumber() ?? headerRow, headerRow);
        worksheet.Range(headerRow, 1, lastRow, columnCount).SetAutoFilter();
        worksheet.Columns(1, columnCount).AdjustToContents();
        for (var column = 1; column <= columnCount; column++)
        {
            var min = wideColumns.Contains(column) ? 18 : column == 5 || column == 2 ? 13 : 14;
            var max = wideColumns.Contains(column) ? 36 : 24;
            worksheet.Column(column).Width = Math.Clamp(worksheet.Column(column).Width + 2, min, max);
        }
        worksheet.Columns(1, columnCount).Style.Alignment.WrapText = true;
    }

    public async Task<ProductionPlanningHistoryResponse?> GetHistoryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select a.id,
                   a.entity_type,
                   a.entity_id,
                   a.field_name,
                   a.old_value,
                   a.new_value,
                   a.reason,
                   a.changed_by_user_id,
                   u.display_name,
                   a.changed_at_utc,
                   a.correlation_id
            from project_audit_events a
            left join qms_users u on u.id = a.changed_by_user_id
            where a.project_id = @project_id
              and a.entity_type in ('ProductionPlan', 'ProductionPlanItem', 'ProjectAssignee')
            order by a.changed_at_utc desc, a.id desc;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var rows = new List<HistoryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new HistoryRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetFieldValue<DateTimeOffset>(9),
                reader.GetString(10)));
        }

        return new ProductionPlanningHistoryResponse(rows
            .GroupBy(row => string.IsNullOrWhiteSpace(row.CorrelationId) ? row.AuditId.ToString("D") : row.CorrelationId)
            .Select(group =>
            {
                var first = group.OrderByDescending(item => item.ChangedAtUtc).ThenByDescending(item => item.AuditId).First();
                return new ProductionPlanningHistoryGroupResponse
                {
                    GroupId = group.Key,
                    ChangedByUserId = first.ChangedByUserId,
                    ChangedByName = first.ChangedByName,
                    ChangedAtUtc = first.ChangedAtUtc,
                    Reason = first.Reason,
                    AffectedItemCount = group.Select(item => item.EntityId).Distinct().Count(),
                    ChangeCount = group.Count(),
                    Changes = group.Select(item => new ProductionPlanningHistoryChangeResponse
                    {
                        EntityId = item.EntityId,
                        EntityType = item.EntityType,
                        FieldName = item.FieldName,
                        OldValue = item.OldValue,
                        NewValue = item.NewValue
                    }).ToList()
                };
            })
            .ToList());
    }

    private async Task<ProductionPlanningMutationResult<ProductionPlanningResponse>?> UpdateAssigneesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        UpdateProductionPlanningRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var requested = (request.Assignees ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.ResponsibilityType))
            .ToDictionary(item => item.ResponsibilityType!, StringComparer.Ordinal);
        var current = await ReadAssigneesAsync(connection, transaction, projectId, cancellationToken);

        foreach (var responsibility in ProductionPlanningDomain.Responsibilities)
        {
            requested.TryGetValue(responsibility, out var update);
            if (update is null)
            {
                continue;
            }

            var currentAssignee = current.FirstOrDefault(item => item.ResponsibilityType == responsibility);
            if (currentAssignee is not null && update.ExpectedRowVersion is not null && currentAssignee.RowVersion != update.ExpectedRowVersion)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.");
            }

            if (update.AssignedUserId is not null && !await IsActiveRoleUserAsync(connection, transaction, update.AssignedUserId.Value, ProductionPlanningDomain.RoleForResponsibility(responsibility), cancellationToken))
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                    new Dictionary<string, string[]> { [responsibility] = [$"{ProductionPlanningDomain.ResponsibilityLabel(responsibility)} 후보에서 활성 사용자를 선택해 주세요."] });
            }

            var changed = currentAssignee?.AssignedUserId != update.AssignedUserId || currentAssignee?.Note != TrimToNull(update.Note);
            var changingExisting = currentAssignee?.AssignedUserId is not null && changed;
            if (changingExisting && string.IsNullOrWhiteSpace(request.Reason))
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                    new Dictionary<string, string[]> { [nameof(request.Reason)] = ["기존 담당자 변경 또는 해제 시 수정사유가 필요합니다."] });
            }

            if (!changed)
            {
                continue;
            }

            var userName = update.AssignedUserId is null ? null : await ReadUserDisplayNameAsync(connection, transaction, update.AssignedUserId.Value, cancellationToken);
            if (currentAssignee is null)
            {
                var assigneeId = Guid.NewGuid();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    insert into project_assignees (
                        id, project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc, note
                    )
                    values (@id, @project_id, @responsibility_type, @assigned_user_id, @assigned_by_user_id, now(), @note);
                    """;
                command.Parameters.AddWithValue("id", assigneeId);
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("responsibility_type", responsibility);
                command.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = update.AssignedUserId ?? (object)DBNull.Value;
                command.Parameters.AddWithValue("assigned_by_user_id", changedByUserId);
                command.Parameters.Add("note", NpgsqlDbType.Text).Value = TrimToNull(update.Note) ?? (object)DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
                await InsertAuditAsync(connection, transaction, projectId, assigneeId, "ProjectAssignee", ProductionPlanningDomain.ResponsibilityLabel(responsibility), null, userName, request.Reason, changedByUserId, correlationId, cancellationToken);
            }
            else
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    update project_assignees
                    set assigned_user_id = @assigned_user_id,
                        assigned_by_user_id = @assigned_by_user_id,
                        assigned_at_utc = now(),
                        note = @note,
                        row_version = row_version + 1
                    where id = @id;
                    """;
                command.Parameters.AddWithValue("id", currentAssignee.AssigneeId!.Value);
                command.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = update.AssignedUserId ?? (object)DBNull.Value;
                command.Parameters.AddWithValue("assigned_by_user_id", changedByUserId);
                command.Parameters.Add("note", NpgsqlDbType.Text).Value = TrimToNull(update.Note) ?? (object)DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
                await InsertAuditAsync(connection, transaction, projectId, currentAssignee.AssigneeId.Value, "ProjectAssignee", ProductionPlanningDomain.ResponsibilityLabel(responsibility), currentAssignee.AssignedUserName, userName, request.Reason, changedByUserId, correlationId, cancellationToken);
            }
        }

        return null;
    }

    private static async Task InsertTemplatePlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, Guid planId, ProductionTemplateStepResponse step, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_production_plan_items (
                id, production_plan_id, template_step_id, sequence_number, step_name_snapshot, is_required, planned_date, note
            )
            values (@id, @plan_id, @template_step_id, @sequence_number, @step_name, @is_required, @planned_date, @note);
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("plan_id", planId);
        command.Parameters.AddWithValue("template_step_id", step.TemplateStepId);
        command.Parameters.AddWithValue("sequence_number", step.SequenceNumber);
        command.Parameters.AddWithValue("step_name", step.StepName);
        command.Parameters.AddWithValue("is_required", step.IsRequired);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCustomPlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, Guid planId, int sequenceNumber, string stepName, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_production_plan_items (
                id, production_plan_id, template_step_id, sequence_number, step_name_snapshot, is_required, planned_date, note
            )
            values (@id, @plan_id, null, @sequence_number, @step_name, false, @planned_date, @note);
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("plan_id", planId);
        command.Parameters.AddWithValue("sequence_number", sequenceNumber);
        command.Parameters.AddWithValue("step_name", stepName);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdatePlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_production_plan_items
            set planned_date = @planned_date,
                note = @note,
                row_version = row_version + 1,
                updated_at_utc = now()
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateCustomPlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, int sequenceNumber, string stepName, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_production_plan_items
            set sequence_number = @sequence_number,
                step_name_snapshot = @step_name,
                planned_date = @planned_date,
                note = @note,
                row_version = row_version + 1,
                updated_at_utc = now()
            where id = @id
              and template_step_id is null;
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("sequence_number", sequenceNumber);
        command.Parameters.AddWithValue("step_name", stepName);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeactivateCustomPlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_production_plan_items
            set is_active = false,
                row_version = row_version + 1,
                updated_at_utc = now()
            where id = @id
              and template_step_id is null;
            """;
        command.Parameters.AddWithValue("id", itemId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, string[]> ValidateProductType(UpsertProductionProductTypeRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            errors[nameof(request.Code)] = ["Item 코드는 필수입니다."];
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(request.Name)] = ["Item 이름은 필수입니다."];
        }
        if (request.Steps is null || request.Steps.Count == 0)
        {
            errors[nameof(request.Steps)] = ["계획 항목은 1개 이상 필요합니다."];
        }
        else if (request.Steps.Any(step => step.SequenceNumber is null or < 1 || string.IsNullOrWhiteSpace(step.StepName)))
        {
            errors[nameof(request.Steps)] = ["계획 항목의 순서와 이름을 확인해 주세요."];
        }

        return errors;
    }

    private static IReadOnlyDictionary<string, string[]> ValidateTemplateSettings(UpdateProductionTemplateSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var rows = request.Steps ?? [];
        if (rows.Count == 0)
        {
            errors[nameof(request.Steps)] = ["생산계획 단계는 최소 1개 이상 필요합니다."];
            return errors;
        }

        var activeNames = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<int>();
        var activeCount = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var prefix = $"steps[{index}]";
            if (row.SequenceNumber is null || row.SequenceNumber < 1)
            {
                errors[$"{prefix}.sequenceNumber"] = [$"{index + 1}행 순서는 1 이상의 정수여야 합니다."];
            }
            else if (!sequences.Add(row.SequenceNumber.Value))
            {
                errors[$"{prefix}.sequenceNumber"] = [$"{index + 1}행 순서가 중복되었습니다."];
            }

            var stepName = TrimToNull(row.StepName);
            if (stepName is null)
            {
                errors[$"{prefix}.stepName"] = [$"{index + 1}행 생산계획 단계명을 입력해 주세요."];
                continue;
            }

            if (stepName.Length > 120)
            {
                errors[$"{prefix}.stepName"] = [$"{index + 1}행 생산계획 단계명은 120자 이하로 입력해 주세요."];
            }

            if (row.IsActive != false)
            {
                activeCount++;
                if (!activeNames.Add(Normalize(stepName)))
                {
                    errors[$"{prefix}.stepName"] = [$"{index + 1}행 활성 생산계획 단계명이 중복되었습니다."];
                }
            }
        }

        if (activeCount == 0)
        {
            errors[nameof(request.Steps)] = ["사용 중인 생산계획 단계는 최소 1개 이상 필요합니다."];
        }

        return errors;
    }

    private static IReadOnlyDictionary<string, string[]> ValidatePlanItemUpdates(
        IReadOnlyList<ProductionPlanItemUpdateRequest> items,
        IReadOnlyList<ProductionTemplateStepResponse> templateSteps)
    {
        var errors = new Dictionary<string, string[]>();
        var templateStepIds = templateSteps.Select(step => step.TemplateStepId).ToHashSet();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in templateSteps)
        {
            names.Add(Normalize(step.StepName));
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.TemplateStepId is not null && !templateStepIds.Contains(item.TemplateStepId.Value))
            {
                errors[$"items[{index}].templateStepId"] = ["현재 Item의 계획 항목이 아닙니다."];
                continue;
            }

            if (item.TemplateStepId is not null)
            {
                continue;
            }

            if (item.IsDeleted == true && item.ItemId is not null)
            {
                continue;
            }

            var stepName = TrimToNull(item.StepName);
            if (stepName is null)
            {
                errors[$"items[{index}].stepName"] = ["추가 계획 항목명을 입력해 주세요."];
                continue;
            }

            if (stepName.Length > 120)
            {
                errors[$"items[{index}].stepName"] = ["추가 계획 항목명은 120자 이하로 입력해 주세요."];
                continue;
            }

            var normalized = Normalize(stepName);
            if (!names.Add(normalized))
            {
                errors[$"items[{index}].stepName"] = ["같은 생산계획 안에서 동일한 계획 항목명을 중복 사용할 수 없습니다."];
            }
        }

        return errors;
    }

    private static ProductionPlanningResponse BuildResponse(
        ProjectSnapshot project,
        PlanHeader? plan,
        IReadOnlyList<ProductionPlanItemResponse> items,
        IReadOnlyList<ProjectAssigneeResponse> assignees,
        IReadOnlyList<AssigneeCandidateResponse> candidates,
        IReadOnlyList<NotificationFallbackResponse> fallbacks)
    {
        var allAssignees = ProductionPlanningDomain.Responsibilities
            .Select(responsibility => assignees.FirstOrDefault(item => item.ResponsibilityType == responsibility) ?? new ProjectAssigneeResponse
            {
                ResponsibilityType = responsibility,
                ResponsibilityLabel = ProductionPlanningDomain.ResponsibilityLabel(responsibility)
            })
            .ToList();
        var status = ProductionPlanningDomain.CalculateStatus(plan?.ProductTypeId, items);
        return new ProductionPlanningResponse(
            project.ProjectId,
            project.ProjectTitle,
            project.ProjectCode,
            project.DeliveryDate,
            plan?.PlanId,
            plan?.RowVersion ?? 0,
            status,
            ProductionPlanningDomain.StatusLabel(status),
            plan?.ProductTypeId,
            plan?.TemplateId,
            plan?.ProductTypeCode,
            plan?.ProductTypeName,
            plan?.Notes,
            SortPlanItems(items),
            allAssignees,
            candidates,
            fallbacks);
    }

    private static IReadOnlyList<ProductionPlanItemResponse> SortPlanItems(IReadOnlyList<ProductionPlanItemResponse> items)
    {
        return items
            .OrderBy(item => item.PlannedDate is null ? 1 : 0)
            .ThenBy(item => item.PlannedDate)
            .ThenBy(item => item.SequenceNumber)
            .ToList();
    }

    private static string CalculateStatus(Guid? productTypeId, int requiredCount, int plannedRequiredCount)
    {
        if (productTypeId is null)
        {
            return ProductionPlanningDomain.NotPlanned;
        }

        return requiredCount > 0 && requiredCount == plannedRequiredCount
            ? ProductionPlanningDomain.Planned
            : ProductionPlanningDomain.Planning;
    }

    private static async Task<ProjectSnapshot?> ReadProjectAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,
                   coalesce(project_title, name, ''),
                   coalesce(project_code, project_number, ''),
                   coalesce(item, ''),
                   delivery_date,
                   status,
                   sales_owner_user_id
            from projects
            where id = @project_id
              and deleted_at_utc is null;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6))
            : null;
    }

    private static async Task<ProjectSnapshot?> LockProjectAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,
                   coalesce(project_title, name, ''),
                   coalesce(project_code, project_number, ''),
                   coalesce(item, ''),
                   delivery_date,
                   status,
                   sales_owner_user_id
            from projects
            where id = @project_id
              and deleted_at_utc is null
            for update;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6))
            : null;
    }

    private static async Task<PlanHeader?> ReadPlanHeaderAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pp.id,
                   pp.product_type_id,
                   pp.template_id,
                   pt.code,
                   pt.name,
                   pp.notes,
                   pp.row_version
            from project_production_plans pp
            left join production_product_types pt on pt.id = pp.product_type_id
            where pp.project_id = @project_id;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PlanHeader(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt32(6))
            : null;
    }

    private static async Task<IReadOnlyList<ProductionPlanItemResponse>> ReadPlanItemsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid planId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, template_step_id, sequence_number, step_name_snapshot, is_required, planned_date, note, row_version
            from project_production_plan_items
            where production_plan_id = @plan_id
              and is_active = true
            order by sequence_number;
            """;
        command.Parameters.AddWithValue("plan_id", planId);
        var items = new List<ProductionPlanItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ProductionPlanItemResponse
            {
                ItemId = reader.GetGuid(0),
                TemplateStepId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                SequenceNumber = reader.GetInt32(2),
                StepName = reader.GetString(3),
                IsRequired = reader.GetBoolean(4),
                IsCustom = reader.IsDBNull(1),
                PlannedDate = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5),
                Note = reader.IsDBNull(6) ? null : reader.GetString(6),
                RowVersion = reader.GetInt32(7)
            });
        }
        return items;
    }

    private static async Task<ProductTypeSnapshot?> ReadActiveProductTypeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid productTypeId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pt.id, pt.code, pt.name, t.id
            from production_product_types pt
            join production_plan_templates t on t.product_type_id = pt.id and t.is_active = true
            where pt.id = @id
              and pt.is_active = true;
            """;
        command.Parameters.AddWithValue("id", productTypeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProductTypeSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3))
            : null;
    }

    private static async Task<ProductTypeSnapshot?> ReadActiveProductTypeByCodeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string productTypeCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pt.id, pt.code, pt.name, t.id
            from production_product_types pt
            join production_plan_templates t on t.product_type_id = pt.id and t.is_active = true
            where pt.code = @code
              and pt.is_active = true;
            """;
        command.Parameters.AddWithValue("code", productTypeCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProductTypeSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3))
            : null;
    }

    private static async Task<IReadOnlyList<ProductionTemplateStepResponse>> ReadTemplateStepsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid templateId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, sequence_number, step_name, is_required
            from production_plan_template_steps
            where template_id = @template_id
              and is_active = true
            order by sequence_number;
            """;
        command.Parameters.AddWithValue("template_id", templateId);
        var steps = new List<ProductionTemplateStepResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            steps.Add(new ProductionTemplateStepResponse(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetBoolean(3)));
        }
        return steps;
    }

    private static async Task<IReadOnlyList<ProductionProductTypeResponse>> ReadProductTypesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pt.id, pt.code, pt.name, pt.is_active, t.id, t.version
            from production_product_types pt
            left join production_plan_templates t on t.product_type_id = pt.id and t.is_active = true
            order by case pt.code
                when 'UL67' then 1
                when 'UL891' then 2
                when 'UL508A' then 3
                when 'IEC' then 4
                when 'LLP' then 5
                when 'RRP' then 6
                else 100
            end, pt.code;
            """;
        var rows = new List<(Guid Id, string Code, string Name, bool Active, Guid? TemplateId, int? Version)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetInt32(5)));
            }
        }

        var result = new List<ProductionProductTypeResponse>();
        foreach (var row in rows)
        {
            result.Add(new ProductionProductTypeResponse(
                row.Id,
                row.Code,
                row.Name,
                row.Active,
                row.TemplateId,
                row.Version,
                row.TemplateId is null ? [] : await ReadTemplateStepsAsync(connection, transaction, row.TemplateId.Value, cancellationToken)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<ProductionTemplateSettingsResponse>> ReadTemplateSettingsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pt.id, pt.code, pt.name, t.id, t.version
            from production_product_types pt
            join production_plan_templates t on t.product_type_id = pt.id and t.is_active = true
            where pt.is_active = true
            order by case pt.code
                when 'UL67' then 1
                when 'UL891' then 2
                when 'UL508A' then 3
                when 'IEC' then 4
                when 'LLP' then 5
                when 'RRP' then 6
                else 100
            end, pt.code;
            """;
        var rows = new List<(Guid ProductTypeId, string Code, string Name, Guid TemplateId, int Version)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3), reader.GetInt32(4)));
            }
        }

        var result = new List<ProductionTemplateSettingsResponse>();
        foreach (var row in rows)
        {
            result.Add(new ProductionTemplateSettingsResponse(
                row.ProductTypeId,
                row.Code,
                row.Name,
                row.TemplateId,
                row.Version,
                await ReadTemplateSettingsStepsAsync(connection, transaction, row.TemplateId, cancellationToken)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ProductionTemplateSettingsStepResponse>> ReadTemplateSettingsStepsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid templateId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, sequence_number, step_name, is_required, is_active
            from production_plan_template_steps
            where template_id = @template_id
            order by sequence_number;
            """;
        command.Parameters.AddWithValue("template_id", templateId);
        var steps = new List<ProductionTemplateSettingsStepResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            steps.Add(new ProductionTemplateSettingsStepResponse(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4)));
        }

        return steps;
    }

    private static async Task<IReadOnlyList<ProjectAssigneeResponse>> ReadAssigneesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid projectId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select pa.id, pa.responsibility_type, pa.assigned_user_id, u.display_name, pa.note, pa.row_version
            from project_assignees pa
            left join qms_users u on u.id = pa.assigned_user_id
            where pa.project_id = @project_id
            order by pa.responsibility_type;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var assignees = new List<ProjectAssigneeResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var responsibility = reader.GetString(1);
            assignees.Add(new ProjectAssigneeResponse
            {
                AssigneeId = reader.GetGuid(0),
                ResponsibilityType = responsibility,
                ResponsibilityLabel = ProductionPlanningDomain.ResponsibilityLabel(responsibility),
                AssignedUserId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                AssignedUserName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Note = reader.IsDBNull(4) ? null : reader.GetString(4),
                RowVersion = reader.GetInt32(5)
            });
        }
        return assignees;
    }

    private static async Task<IReadOnlyList<AssigneeCandidateResponse>> ReadAssigneeCandidatesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        var result = new List<AssigneeCandidateResponse>();
        foreach (var responsibility in ProductionPlanningDomain.Responsibilities)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                select u.id, u.display_name
                from qms_users u
                join user_roles ur on ur.user_id = u.id
                join roles r on r.id = ur.role_id
                where u.is_active = true
                  and r.code = @role
                order by u.display_name;
                """;
            command.Parameters.AddWithValue("role", ProductionPlanningDomain.RoleForResponsibility(responsibility));
            var users = new List<UserOptionResponse>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new UserOptionResponse(reader.GetGuid(0), reader.GetString(1)));
            }
            result.Add(new AssigneeCandidateResponse(responsibility, users));
        }
        return result;
    }

    private static async Task<IReadOnlyList<NotificationFallbackResponse>> BuildFallbacksAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, ProjectSnapshot project, IReadOnlyList<ProjectAssigneeResponse> assignees, CancellationToken cancellationToken)
    {
        var salesOwnerName = project.SalesOwnerUserId is null ? null : await ReadUserDisplayNameAsync(connection, transaction, project.SalesOwnerUserId.Value, cancellationToken);
        var admin = await ReadFirstActiveRoleUserAsync(connection, transaction, "system-administrator", cancellationToken);
        return ProductionPlanningDomain.Responsibilities.Select(responsibility =>
        {
            var assigned = assignees.FirstOrDefault(item => item.ResponsibilityType == responsibility && item.AssignedUserId is not null);
            if (assigned is not null)
            {
                return new NotificationFallbackResponse(responsibility, ProductionPlanningDomain.ResponsibilityLabel(responsibility), assigned.AssignedUserId, assigned.AssignedUserName, "담당자");
            }
            if (project.SalesOwnerUserId is not null && !string.IsNullOrWhiteSpace(salesOwnerName))
            {
                return new NotificationFallbackResponse(responsibility, ProductionPlanningDomain.ResponsibilityLabel(responsibility), project.SalesOwnerUserId, salesOwnerName, "영업담당자");
            }
            return new NotificationFallbackResponse(responsibility, ProductionPlanningDomain.ResponsibilityLabel(responsibility), admin?.UserId, admin?.DisplayName, "관리자");
        }).ToList();
    }

    private static async Task<bool> IsActiveRoleUserAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid userId, string roleCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select exists (
                select 1
                from qms_users u
                join user_roles ur on ur.user_id = u.id
                join roles r on r.id = ur.role_id
                where u.id = @user_id
                  and u.is_active = true
                  and r.code = @role
            );
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("role", roleCode);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<string?> ReadUserDisplayNameAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select display_name from qms_users where id = @user_id and is_active = true;";
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private static async Task<UserOptionResponse?> ReadFirstActiveRoleUserAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string roleCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select u.id, u.display_name
            from qms_users u
            join user_roles ur on ur.user_id = u.id
            join roles r on r.id = ur.role_id
            where u.is_active = true
              and r.code = @role
            order by u.display_name
            limit 1;
            """;
        command.Parameters.AddWithValue("role", roleCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new UserOptionResponse(reader.GetGuid(0), reader.GetString(1))
            : null;
    }

    private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid entityId, string entityType, string fieldName, string? oldValue, string? newValue, string? reason, Guid userId, string correlationId, CancellationToken cancellationToken, string inputSource = "Direct")
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_audit_events (
                project_id, entity_type, entity_id, action, field_name, old_value, new_value,
                reason, changed_by_user_id, correlation_id, input_source, is_sensitive
            )
            values (
                @project_id, @entity_type, @entity_id, 'ProductionPlanningUpdated', @field_name,
                @old_value, @new_value, @reason, @user_id, @correlation_id, @input_source, false
            );
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("entity_type", entityType);
        command.Parameters.AddWithValue("entity_id", entityId);
        command.Parameters.AddWithValue("field_name", fieldName);
        command.Parameters.Add("old_value", NpgsqlDbType.Text).Value = oldValue ?? (object)DBNull.Value;
        command.Parameters.Add("new_value", NpgsqlDbType.Text).Value = newValue ?? (object)DBNull.Value;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = TrimToNull(reason) ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue("input_source", inputSource);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertTemplateSettingsAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProductionTemplateSettingsResponse previous,
        UpdateProductionTemplateSettingsRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var oldValue = string.Join(" | ", previous.Steps.Select(step => $"{step.SequenceNumber}:{step.StepName}:{(step.IsRequired ? "필수" : "선택")}:{(step.IsActive ? "사용" : "미사용")}"));
        var newValue = string.Join(" | ", request.Steps!.OrderBy(step => step.SequenceNumber!.Value).Select(step => $"{step.SequenceNumber}:{step.StepName!.Trim()}:{((step.IsRequired ?? true) ? "필수" : "선택")}:{((step.IsActive ?? true) ? "사용" : "미사용")}"));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into production_plan_template_audit_events (
                product_type_id, template_id, action, old_value, new_value,
                reason, changed_by_user_id, correlation_id
            )
            values (
                @product_type_id, @template_id, 'TemplateSettingsUpdated', @old_value, @new_value,
                @reason, @changed_by_user_id, @correlation_id
            );
            """;
        command.Parameters.AddWithValue("product_type_id", previous.ProductTypeId);
        command.Parameters.AddWithValue("template_id", previous.ActiveTemplateId);
        command.Parameters.Add("old_value", NpgsqlDbType.Text).Value = oldValue;
        command.Parameters.Add("new_value", NpgsqlDbType.Text).Value = newValue;
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = TrimToNull(request.Reason) ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("changed_by_user_id", changedByUserId);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("QMS database connection string is not configured.");
        }

        return NpgsqlDataSource.Create(connectionString);
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FormatDate(DateOnly? value)
    {
        return value?.ToString("yyyy-MM-dd");
    }

    private static string Normalize(string? value)
    {
        return string.Join(' ', (value ?? "").Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ItemCodesEqual(string? left, string? right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    private static string NormalizeExcelHeader(string value)
    {
        return string.Join(' ', value.Trim().TrimEnd('*').Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool HeaderMatches(string actual, string expected)
    {
        if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(expected, "Item", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual, "제품 구분", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] BulkExcelHeaders =
    [
        "프로젝트명",
        "PJT Code",
        "Item",
        "계획 항목",
        "예정일",
        "비고",
        "구매 담당자",
        "생산관리 담당자",
        "제조 담당자",
        "품질 담당자",
        "물류 담당자"
    ];

    private async Task<IReadOnlyList<ProductionPlanningExcelRow>> ParseBulkExcelRowsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, byte[] bytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        if (workbook.Worksheets.Count != 1)
        {
            throw new InvalidDataException("생산계획 Excel은 하나의 시트만 포함해야 합니다.");
        }

        var worksheet = workbook.Worksheet(1);
        var headerRow = 1;
        var headerMap = BulkExcelHeaders
            .Select((header, index) => (header, index: index + 1))
            .ToDictionary(item => item.header, item => item.index, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < BulkExcelHeaders.Length; index++)
        {
            var actual = NormalizeExcelHeader(worksheet.Cell(headerRow, index + 1).GetString());
            if (!HeaderMatches(actual, BulkExcelHeaders[index]))
            {
                throw new InvalidDataException("생산계획 Excel 양식의 Header를 확인해 주세요.");
            }
        }

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
        if (lastRow - headerRow > 500)
        {
            throw new InvalidDataException("생산계획 Excel은 최대 500행까지 업로드할 수 있습니다.");
        }

        var productTypes = await ReadProductTypesAsync(connection, transaction, cancellationToken);
        var rows = new List<ProductionPlanningExcelRow>();
        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var projectTitle = CellText(worksheet.Cell(rowNumber, headerMap["프로젝트명"]));
            var projectCode = CellText(worksheet.Cell(rowNumber, headerMap["PJT Code"]));
            var productTypeCode = CellText(worksheet.Cell(rowNumber, headerMap["Item"]));
            var stepName = CellText(worksheet.Cell(rowNumber, headerMap["계획 항목"]));
            var plannedDateText = CellText(worksheet.Cell(rowNumber, headerMap["예정일"]));
            var note = CellText(worksheet.Cell(rowNumber, headerMap["비고"]));
            var procurement = CellText(worksheet.Cell(rowNumber, headerMap["구매 담당자"]));
            var production = CellText(worksheet.Cell(rowNumber, headerMap["생산관리 담당자"]));
            var manufacturing = CellText(worksheet.Cell(rowNumber, headerMap["제조 담당자"]));
            var quality = CellText(worksheet.Cell(rowNumber, headerMap["품질 담당자"]));
            var logistics = CellText(worksheet.Cell(rowNumber, headerMap["물류 담당자"]));

            if (new[] { projectTitle, projectCode, productTypeCode, stepName, plannedDateText, note, procurement, production, manufacturing, quality, logistics }.All(string.IsNullOrWhiteSpace))
            {
                rows.Add(new ProductionPlanningExcelRow(rowNumber, "Skipped", null, projectTitle, projectCode, null, productTypeCode, null, stepName, false, null, note, procurement, production, manufacturing, quality, logistics, ["빈 행입니다."]));
                continue;
            }

            var errors = new List<string>();
            var project = await MatchProjectForExcelAsync(connection, transaction, projectCode, projectTitle, cancellationToken);
            if (project is null)
            {
                errors.Add("등록되지 않은 프로젝트입니다.");
            }

            var productType = productTypes.FirstOrDefault(item => string.Equals(item.Code, productTypeCode, StringComparison.OrdinalIgnoreCase));
            if (productType is null)
            {
                errors.Add("Item은 UL67, UL891, UL508A, IEC, LLP, RRP 중 하나여야 합니다.");
            }

            if (project is not null
                && !string.IsNullOrWhiteSpace(productTypeCode)
                && !ItemCodesEqual(project.Item, productTypeCode))
            {
                errors.Add($"Excel의 Item이 프로젝트 Item과 일치하지 않습니다. 프로젝트 Item: {project.Item}, Excel Item: {productTypeCode}");
            }

            if (string.IsNullOrWhiteSpace(stepName))
            {
                errors.Add("계획 항목은 필수입니다.");
            }

            DateOnly? plannedDate = null;
            if (!string.IsNullOrWhiteSpace(plannedDateText))
            {
                if (DateOnly.TryParse(plannedDateText, out var parsedDate))
                {
                    plannedDate = parsedDate;
                }
                else
                {
                    errors.Add("예정일은 yyyy-mm-dd 형식으로 입력해 주세요.");
                }
            }

            foreach (var assignee in new[]
            {
                ("구매 담당자", procurement, "Procurement"),
                ("생산관리 담당자", production, "ProductionPlanning"),
                ("제조 담당자", manufacturing, "Manufacturing"),
                ("품질 담당자", quality, "Quality"),
                ("물류 담당자", logistics, "Logistics")
            })
            {
                if (!string.IsNullOrWhiteSpace(assignee.Item2)
                    && await MatchUserForExcelAsync(connection, transaction, assignee.Item2, assignee.Item3, cancellationToken) is null)
                {
                    errors.Add($"{assignee.Item1} 후보에서 활성 사용자를 찾을 수 없습니다.");
                }
            }

            ProductionTemplateStepResponse? templateStep = null;
            if (errors.Count == 0 && project is not null && productType is not null)
            {
                var existingPlan = await ReadPlanHeaderAsync(connection, transaction, project.ProjectId, cancellationToken);
                if (existingPlan is not null)
                {
                    var existingItems = await ReadPlanItemsAsync(connection, transaction, existingPlan.PlanId, cancellationToken);
                    var existingItem = existingItems.FirstOrDefault(item => string.Equals(Normalize(item.StepName), Normalize(stepName), StringComparison.Ordinal));
                    if (existingItem?.TemplateStepId is not null)
                    {
                        templateStep = new ProductionTemplateStepResponse(
                            existingItem.TemplateStepId.Value,
                            existingItem.SequenceNumber,
                            existingItem.StepName,
                            existingItem.IsRequired);
                    }
                }
                else
                {
                    templateStep = productType.Steps.FirstOrDefault(step => string.Equals(Normalize(step.StepName), Normalize(stepName), StringComparison.Ordinal));
                }
            }
            var resultType = errors.Count > 0 ? "Error" : templateStep is null ? "CustomStep" : "New";
            rows.Add(new ProductionPlanningExcelRow(
                rowNumber,
                resultType,
                project?.ProjectId,
                projectTitle,
                projectCode,
                productType?.ProductTypeId,
                productTypeCode,
                templateStep?.TemplateStepId,
                stepName,
                errors.Count == 0 && templateStep is null,
                plannedDate,
                TrimToNull(note),
                procurement,
                production,
                manufacturing,
                quality,
                logistics,
                errors));
        }

        return rows;
    }

    private static ProductionPlanningExcelPreviewResponse BuildExcelPreview(string fileSha256, IReadOnlyList<ProductionPlanningExcelRow> rows)
    {
        var responseRows = rows.Select(row => new ProductionPlanningExcelPreviewRowResponse
        {
            ExcelRowNumber = row.ExcelRowNumber,
            ResultType = row.ResultType,
            ProjectId = row.ProjectId,
            ProjectTitle = row.ProjectTitle,
            ProjectCode = row.ProjectCode,
            ProductTypeId = row.ProductTypeId,
            ProductTypeCode = row.ProductTypeCode,
            TemplateStepId = row.TemplateStepId,
            StepName = row.StepName,
            IsCustomStep = row.IsCustomStep,
            PlannedDate = row.PlannedDate,
            Note = row.Note,
            ProcurementAssigneeText = row.ProcurementAssigneeText,
            ProductionPlanningAssigneeText = row.ProductionPlanningAssigneeText,
            ManufacturingAssigneeText = row.ManufacturingAssigneeText,
            QualityAssigneeText = row.QualityAssigneeText,
            LogisticsAssigneeText = row.LogisticsAssigneeText,
            ErrorMessages = row.ErrorMessages
        }).ToList();
        return new ProductionPlanningExcelPreviewResponse(
            fileSha256,
            rows.Count,
            rows.Count(row => row.IsSaveable),
            rows.Count(row => !row.IsSaveable && row.ResultType != "Skipped"),
            responseRows);
    }

    private static string? CellText(IXLCell cell)
    {
        if (cell.HasFormula)
        {
            throw new InvalidDataException("수식이 포함된 Excel 파일은 업로드할 수 없습니다.");
        }

        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime).ToString("yyyy-MM-dd");
        }

        return TrimToNull(cell.GetFormattedString());
    }

    private static async Task<ProjectSnapshot?> MatchProjectForExcelAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string? projectCode, string? projectTitle, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var byCode = await FindProjectsByCodeAsync(connection, transaction, projectCode, cancellationToken);
            if (byCode.Count == 1)
            {
                return byCode[0];
            }

            if (byCode.Count > 1)
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(projectTitle))
        {
            var byTitle = await FindProjectByNormalizedTitleAsync(connection, transaction, projectTitle, cancellationToken);
            if (byTitle is not null)
            {
                return byTitle;
            }
        }

        return null;
    }

    private static async Task<IReadOnlyList<ProjectSnapshot>> FindProjectsByCodeAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string projectCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, coalesce(project_title, name, ''), coalesce(project_code, project_number, ''), coalesce(item, ''), delivery_date, status, sales_owner_user_id
            from projects
            where deleted_at_utc is null
              and upper(btrim(coalesce(project_code, project_number, ''))) = upper(btrim(@project_code));
            """;
        command.Parameters.AddWithValue("project_code", projectCode);
        var rows = new List<ProjectSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6)));
        }
        return rows;
    }

    private static async Task<ProjectSnapshot?> FindProjectByNormalizedTitleAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string projectTitle, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, coalesce(project_title, name, ''), coalesce(project_code, project_number, ''), coalesce(item, ''), delivery_date, status, sales_owner_user_id
            from projects
            where deleted_at_utc is null
              and upper(btrim(coalesce(project_title, name, ''))) = upper(btrim(@project_title))
            limit 1;
            """;
        command.Parameters.AddWithValue("project_title", projectTitle);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6))
            : null;
    }

    private static async Task<Guid?> MatchUserForExcelAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string text, string responsibilityType, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select u.id
            from qms_users u
            join user_roles ur on ur.user_id = u.id
            join roles r on r.id = ur.role_id
            where u.is_active = true
              and r.code = @role
              and (upper(btrim(u.display_name)) = upper(btrim(@text)) or upper(btrim(u.development_user_key)) = upper(btrim(@text)))
            limit 1;
            """;
        command.Parameters.AddWithValue("role", ProductionPlanningDomain.RoleForResponsibility(responsibilityType));
        command.Parameters.AddWithValue("text", text);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid userId ? userId : null;
    }

    private static async Task<(Guid PlanId, bool Created)> EnsurePlanForExcelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, ProductTypeSnapshot productType, Guid changedByUserId, CancellationToken cancellationToken)
    {
        var plan = await ReadPlanHeaderAsync(connection, transaction, projectId, cancellationToken);
        if (plan is not null)
        {
            return (plan.PlanId, false);
        }

        var planId = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_production_plans (
                id, project_id, product_type_id, template_id, created_by_user_id, updated_by_user_id
            )
            values (@id, @project_id, @product_type_id, @template_id, @user_id, @user_id);
            """;
        command.Parameters.AddWithValue("id", planId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("product_type_id", productType.ProductTypeId);
        command.Parameters.AddWithValue("template_id", productType.TemplateId);
        command.Parameters.AddWithValue("user_id", changedByUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (planId, true);
    }

    private static async Task ApplyAssigneesFromExcelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, ProductionPlanningExcelRow row, Guid changedByUserId, string? reason, string correlationId, CancellationToken cancellationToken)
    {
        foreach (var item in new[]
        {
            ("Procurement", row.ProcurementAssigneeText),
            ("ProductionPlanning", row.ProductionPlanningAssigneeText),
            ("Manufacturing", row.ManufacturingAssigneeText),
            ("Quality", row.QualityAssigneeText),
            ("Logistics", row.LogisticsAssigneeText)
        })
        {
            if (string.IsNullOrWhiteSpace(item.Item2))
            {
                continue;
            }

            var userId = await MatchUserForExcelAsync(connection, transaction, item.Item2, item.Item1, cancellationToken);
            if (userId is null)
            {
                continue;
            }

            await UpsertAssigneeFromExcelAsync(connection, transaction, projectId, item.Item1, userId.Value, changedByUserId, reason, correlationId, cancellationToken);
        }
    }

    private static async Task UpsertAssigneeFromExcelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string responsibilityType, Guid assignedUserId, Guid changedByUserId, string? reason, string correlationId, CancellationToken cancellationToken)
    {
        var current = (await ReadAssigneesAsync(connection, transaction, projectId, cancellationToken)).FirstOrDefault(item => item.ResponsibilityType == responsibilityType);
        if (current?.AssignedUserId == assignedUserId)
        {
            return;
        }

        var assigneeId = current?.AssigneeId ?? Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = current is null
            ? """
              insert into project_assignees (id, project_id, responsibility_type, assigned_user_id, assigned_by_user_id, assigned_at_utc)
              values (@id, @project_id, @responsibility_type, @assigned_user_id, @assigned_by_user_id, now());
              """
            : """
              update project_assignees
              set assigned_user_id = @assigned_user_id,
                  assigned_by_user_id = @assigned_by_user_id,
                  assigned_at_utc = now(),
                  row_version = row_version + 1
              where id = @id;
              """;
        command.Parameters.AddWithValue("id", assigneeId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("responsibility_type", responsibilityType);
        command.Parameters.AddWithValue("assigned_user_id", assignedUserId);
        command.Parameters.AddWithValue("assigned_by_user_id", changedByUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        var newName = await ReadUserDisplayNameAsync(connection, transaction, assignedUserId, cancellationToken);
        await InsertAuditAsync(connection, transaction, projectId, assigneeId, "ProjectAssignee", ProductionPlanningDomain.ResponsibilityLabel(responsibilityType), current?.AssignedUserName, newName, reason, changedByUserId, correlationId, cancellationToken, "Excel");
    }

    private static async Task InsertProductionPlanningImportBatchAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string fileName, long fileSizeBytes, string fileSha256, int totalRowCount, int appliedRowCount, int errorRowCount, Guid userId, string? reason, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into production_planning_excel_import_batches (
                original_file_name, file_size_bytes, file_sha256, total_row_count, applied_row_count, error_row_count, uploaded_by_user_id, reason
            )
            values (@file_name, @file_size, @file_sha, @total_count, @applied_count, @error_count, @user_id, @reason);
            """;
        command.Parameters.AddWithValue("file_name", fileName);
        command.Parameters.AddWithValue("file_size", fileSizeBytes);
        command.Parameters.AddWithValue("file_sha", fileSha256);
        command.Parameters.AddWithValue("total_count", totalRowCount);
        command.Parameters.AddWithValue("applied_count", appliedRowCount);
        command.Parameters.AddWithValue("error_count", errorRowCount);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.Add("reason", NpgsqlDbType.Text).Value = TrimToNull(reason) ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ProjectSnapshot(Guid ProjectId, string ProjectTitle, string ProjectCode, string Item, DateOnly? DeliveryDate, string Status, Guid? SalesOwnerUserId);
    private sealed record PlanHeader(Guid PlanId, Guid? ProductTypeId, Guid? TemplateId, string? ProductTypeCode, string? ProductTypeName, string? Notes, int RowVersion);
    private sealed record ProductTypeSnapshot(Guid ProductTypeId, string ProductTypeCode, string ProductTypeName, Guid TemplateId);
    private sealed record HistoryRow(Guid AuditId, string EntityType, Guid EntityId, string? FieldName, string? OldValue, string? NewValue, string? Reason, Guid? ChangedByUserId, string? ChangedByName, DateTimeOffset ChangedAtUtc, string CorrelationId);
    private sealed record ProductionPlanningExcelRow(
        int ExcelRowNumber,
        string ResultType,
        Guid? ProjectId,
        string? ProjectTitle,
        string? ProjectCode,
        Guid? ProductTypeId,
        string? ProductTypeCode,
        Guid? TemplateStepId,
        string? StepName,
        bool IsCustomStep,
        DateOnly? PlannedDate,
        string? Note,
        string? ProcurementAssigneeText,
        string? ProductionPlanningAssigneeText,
        string? ManufacturingAssigneeText,
        string? QualityAssigneeText,
        string? LogisticsAssigneeText,
        IReadOnlyList<string> ErrorMessages)
    {
        public bool IsSaveable => ResultType is "New" or "Changed" or "CustomStep";
    }
}
