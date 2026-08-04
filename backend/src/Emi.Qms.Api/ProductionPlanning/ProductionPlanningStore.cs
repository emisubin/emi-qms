using ClosedXML.Excel;
using Npgsql;
using NpgsqlTypes;
using System.Globalization;

namespace Emi.Qms.Api.ProductionPlanning;

public sealed class ProductionPlanningStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    internal static async Task EnsureSetPlanScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid? setInstanceId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into project_production_plan_set_defaults (
                    id, production_plan_id, row_version, created_by_user_id, updated_by_user_id
                )
                select uuid_generate_v4(), plan.id, 1, @actor_id, @actor_id
                from project_production_plans plan
                join projects project on project.id=plan.project_id
                where plan.project_id=@project_id
                  and plan.model_version='LINKED_V1'
                  and project.structure_mode='Ul891Set'
                on conflict (production_plan_id) do nothing;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into project_production_plan_set_scopes (
                    id, production_plan_id, set_instance_id,
                    row_version, created_by_user_id, updated_by_user_id
                )
                select uuid_generate_v4(), plan.id, instance.id, 1, @actor_id, @actor_id
                from projects project
                join project_production_plans plan
                  on plan.project_id = project.id
                 and plan.model_version = 'LINKED_V1'
                join ul891_set_specs spec on spec.project_id = project.id
                join ul891_set_instances instance on instance.spec_id = spec.id
                where project.id = @project_id
                  and project.structure_mode = 'Ul891Set'
                  and (@set_instance_id is null or instance.id = @set_instance_id)
                on conflict (production_plan_id, set_instance_id) do nothing;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.Add("set_instance_id", NpgsqlDbType.Uuid).Value = setInstanceId ?? (object)DBNull.Value;
            command.Parameters.AddWithValue("actor_id", actorId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                insert into project_production_plan_set_item_values (
                    id, set_scope_id, production_plan_item_id,
                    planned_start_date, planned_end_date, assigned_user_id,
                    required_headcount, note, row_version
                )
                select uuid_generate_v4(), scope.id, item.id,
                       default_value.planned_start_date, default_value.planned_end_date,
                       default_value.assigned_user_id, default_value.required_headcount,
                       default_value.note, 1
                from project_production_plan_set_scopes scope
                join project_production_plans plan on plan.id = scope.production_plan_id
                join project_production_plan_items item
                  on item.production_plan_id = plan.id
                left join project_production_plan_set_defaults defaults
                  on defaults.production_plan_id = plan.id
                left join project_production_plan_set_default_values default_value
                  on default_value.set_default_id = defaults.id
                 and default_value.production_plan_item_id = item.id
                where plan.project_id = @project_id
                  and (@set_instance_id is null or scope.set_instance_id = @set_instance_id)
                on conflict (set_scope_id, production_plan_item_id) do nothing;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.Add("set_instance_id", NpgsqlDbType.Uuid).Value = setInstanceId ?? (object)DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<ProductionPlanningSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            with active_projects as (
                select p.id, p.structure_mode
                from projects p
                where p.deleted_at_utc is null
                  and p.status = 'Active'
            ),
            base_steps as (
                select pp.id as plan_id,
                       count(pi.id) filter (where pi.is_active and pi.is_required)::int as required_count,
                       count(pi.id) filter (
                           where pi.is_active
                             and pi.is_required
                             and (
                               (pp.model_version = 'LEGACY' and pi.planned_date is not null)
                               or (pp.model_version = 'LINKED_V1' and pi.planned_start_date is not null and pi.planned_end_date is not null)
                             )
                       )::int as planned_required_count
                from project_production_plans pp
                left join project_production_plan_items pi on pi.production_plan_id = pp.id
                group by pp.id
            ),
            set_steps as (
                select scope.production_plan_id as plan_id,
                       count(pi.id) filter (where pi.is_active and pi.is_required)::int as required_count,
                       count(pi.id) filter (
                           where pi.is_active and pi.is_required
                             and value.planned_start_date is not null
                             and value.planned_end_date is not null
                       )::int as planned_required_count
                from project_production_plan_set_scopes scope
                join ul891_set_instances instance on instance.id = scope.set_instance_id and instance.status = 'Active'
                join project_production_plan_items pi on pi.production_plan_id = scope.production_plan_id
                left join project_production_plan_set_item_values value
                  on value.set_scope_id = scope.id and value.production_plan_item_id = pi.id
                group by scope.production_plan_id
            ),
            project_steps as (
                select ap.id as project_id,
                       pp.product_type_id,
                       case when ap.structure_mode = 'Ul891Set' and pp.model_version = 'LINKED_V1'
                            then coalesce(set_steps.required_count, 0)
                            else coalesce(base_steps.required_count, 0) end as required_count,
                       case when ap.structure_mode = 'Ul891Set' and pp.model_version = 'LINKED_V1'
                            then coalesce(set_steps.planned_required_count, 0)
                            else coalesce(base_steps.planned_required_count, 0) end as planned_required_count
                from active_projects ap
                left join project_production_plans pp on pp.project_id = ap.id
                left join base_steps on base_steps.plan_id = pp.id
                left join set_steps on set_steps.plan_id = pp.id
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
            with base_steps as (
                select pp.id as plan_id,
                       count(pi.id) filter (where pi.is_active and pi.is_required)::int as required_count,
                       count(pi.id) filter (
                           where pi.is_active and pi.is_required
                             and (
                               (pp.model_version = 'LEGACY' and pi.planned_date is not null)
                               or (pp.model_version = 'LINKED_V1' and pi.planned_start_date is not null and pi.planned_end_date is not null)
                             )
                       )::int as planned_required_count
                from project_production_plans pp
                left join project_production_plan_items pi on pi.production_plan_id = pp.id
                group by pp.id
            ),
            set_steps as (
                select scope.production_plan_id as plan_id,
                       count(pi.id) filter (where pi.is_active and pi.is_required)::int as required_count,
                       count(pi.id) filter (
                           where pi.is_active and pi.is_required
                             and value.planned_start_date is not null
                             and value.planned_end_date is not null
                       )::int as planned_required_count
                from project_production_plan_set_scopes scope
                join ul891_set_instances instance on instance.id = scope.set_instance_id and instance.status = 'Active'
                join project_production_plan_items pi on pi.production_plan_id = scope.production_plan_id
                left join project_production_plan_set_item_values value
                  on value.set_scope_id = scope.id and value.production_plan_item_id = pi.id
                group by scope.production_plan_id
            ),
            plan_summary as (
                select pp.project_id,
                       pp.product_type_id,
                       pt.code as product_type_code,
                       pt.name as product_type_name,
                       case when project.structure_mode = 'Ul891Set' and pp.model_version = 'LINKED_V1'
                            then coalesce(set_steps.required_count, 0)
                            else coalesce(base_steps.required_count, 0) end as required_count,
                       case when project.structure_mode = 'Ul891Set' and pp.model_version = 'LINKED_V1'
                            then coalesce(set_steps.planned_required_count, 0)
                            else coalesce(base_steps.planned_required_count, 0) end as planned_required_count
                from project_production_plans pp
                join projects project on project.id = pp.project_id
                left join production_product_types pt on pt.id = pp.product_type_id
                left join base_steps on base_steps.plan_id = pp.id
                left join set_steps on set_steps.plan_id = pp.id
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
        var result = await GetProjectPlanAsync(projectId, null, cancellationToken);
        return result.Status == ProductionPlanningReadStatus.Success ? result.Value : null;
    }

    public async Task<ProductionPlanningReadResult> GetProjectPlanAsync(
        Guid projectId,
        Guid? setInstanceId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await ReadProjectAsync(connection, null, projectId, cancellationToken);
        if (project is null)
        {
            return ProductionPlanningReadResult.NotFound();
        }

        var plan = await ReadPlanHeaderAsync(connection, null, projectId, cancellationToken);
        var items = plan is null ? [] : await ReadPlanItemsAsync(connection, null, plan.PlanId, cancellationToken);
        IReadOnlyList<ProductionPlanSetScopeResponse> scopes = [];
        ProductionPlanSetScopeResponse? selectedScope = null;
        ProductionPlanSetDefaultResponse? setDefault = null;
        var isSetScoped = plan?.ModelVersion == ProductionControlModelVersions.LinkedV1
            && string.Equals(project.StructureMode, "Ul891Set", StringComparison.Ordinal);
        if (setInstanceId is not null && !isSetScoped)
        {
            return ProductionPlanningReadResult.Validation(
                new Dictionary<string, string[]> { ["setInstanceId"] = ["세트형 UL891 프로젝트에서만 세트 생산계획을 조회할 수 있습니다."] });
        }
        if (isSetScoped && plan is not null)
        {
            setDefault = await ReadSetDefaultAsync(connection, null, plan.PlanId, items, cancellationToken);
            scopes = await ReadSetScopesAsync(connection, null, plan.PlanId, cancellationToken);
            if (setInstanceId is not null)
            {
                selectedScope = scopes.FirstOrDefault(scope => scope.SetInstanceId == setInstanceId.Value);
                if (selectedScope is null)
                {
                    return ProductionPlanningReadResult.Validation(
                        new Dictionary<string, string[]> { ["setInstanceId"] = ["현재 프로젝트에 속한 세트를 다시 선택해 주세요."] });
                }
                var values = await ReadSetItemValuesAsync(connection, null, selectedScope.ScopeId, cancellationToken);
                items = ApplySetItemValues(items, values);
            }
            else
            {
                var activeScopeIds = scopes
                    .Where(scope => string.Equals(scope.Status, "Active", StringComparison.Ordinal))
                    .Select(scope => scope.ScopeId)
                    .ToArray();
                var values = await ReadSetItemValuesAsync(connection, null, activeScopeIds, cancellationToken);
                items = AggregateSetItemValues(items, scopes, values);
            }
        }
        if (plan?.ModelVersion == ProductionControlModelVersions.LinkedV1)
        {
            items = await EnrichLinkedPlanItemsAsync(connection, null, project, items, setInstanceId, cancellationToken);
        }
        var manufacturingSteps = plan?.ModelVersion == ProductionControlModelVersions.LinkedV1
            ? await ReadProjectManufacturingStepsAsync(connection, null, projectId, cancellationToken)
            : [];
        var availableSources = plan?.ModelVersion == ProductionControlModelVersions.LinkedV1
            ? await ProductionControlTemplateStore.ReadSourceCatalogAsync(connection, null, cancellationToken)
            : [];
        var assignees = await ReadAssigneesAsync(connection, null, projectId, cancellationToken);
        var candidates = await ReadAssigneeCandidatesAsync(connection, null, cancellationToken);
        var fallbacks = await BuildFallbacksAsync(connection, null, project, assignees, cancellationToken);
        return ProductionPlanningReadResult.Success(
            BuildResponse(project, plan, items, manufacturingSteps, availableSources, assignees, candidates, fallbacks, isSetScoped, selectedScope, scopes, setDefault));
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

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update production_plan_templates
                set is_active = true
                where id = @template_id;

                update production_plan_template_steps
                set is_active = false
                where template_id = @template_id;
                """;
            command.Parameters.AddWithValue("product_type_id", productTypeId);
            command.Parameters.AddWithValue("template_id", productType.ActiveTemplateId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var step in request.Steps!.OrderBy(item => item.SequenceNumber!.Value))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                insert into production_plan_template_steps (template_id, sequence_number, step_name, is_required, is_active)
                values (@template_id, @sequence_number, @step_name, @is_required, @is_active)
                on conflict (template_id, sequence_number) do update
                set step_name = excluded.step_name,
                    is_required = excluded.is_required,
                    is_active = excluded.is_active;
                """;
            command.Parameters.AddWithValue("template_id", productType.ActiveTemplateId);
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

        if (currentPlan?.ModelVersion == ProductionControlModelVersions.LinkedV1)
        {
            return await UpdateLinkedProjectPlanAsync(
                connection,
                transaction,
                project,
                currentPlan,
                request,
                changedByUserId,
                correlationId,
                cancellationToken);
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
            var validationErrors = ValidatePlanItemUpdates(request.Items ?? [], templateSteps)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            await AppendPlanItemStaffingErrorsAsync(
                connection,
                transaction,
                request.Items ?? [],
                validationErrors,
                cancellationToken);
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
                var stepName = TrimToNull(requestedItem?.StepName) ?? current?.StepName ?? step.StepName;
                var isRequired = requestedItem?.IsRequired ?? current?.IsRequired ?? step.IsRequired;
                var plannedDate = requestedItem?.PlannedDate;
                var note = TrimToNull(requestedItem?.Note);
                var assignedUserId = requestedItem is null ? current?.AssignedUserId : requestedItem.AssignedUserId;
                var requiredHeadcount = requestedItem is null ? current?.RequiredHeadcount : requestedItem.RequiredHeadcount;
                if (current is null)
                {
                    if (currentPlan is not null)
                    {
                        continue;
                    }

                    var itemId = Guid.NewGuid();
                    await InsertTemplatePlanItemAsync(connection, transaction, itemId, planId, step, stepName, isRequired, plannedDate, note, cancellationToken);
                    if (assignedUserId is not null || requiredHeadcount is not null)
                    {
                        await UpdatePlanItemStaffingAsync(connection, transaction, itemId, assignedUserId, requiredHeadcount, cancellationToken);
                    }
                    if (plannedDate is not null)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem", stepName, null, plannedDate.Value.ToString("yyyy-MM-dd"), request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                }
                else
                {
                    if (requestedItem?.ExpectedRowVersion is not null && current.RowVersion != requestedItem.ExpectedRowVersion)
                    {
                        return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.");
                    }

                    if (current.StepName != stepName || current.IsRequired != isRequired || current.PlannedDate != plannedDate || current.Note != note)
                    {
                        await UpdatePlanItemAsync(connection, transaction, current.ItemId!.Value, stepName, isRequired, plannedDate, note, cancellationToken);
                        if (current.StepName != stepName)
                        {
                            await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", "계획 항목명", current.StepName, stepName, request.Reason, changedByUserId, correlationId, cancellationToken);
                        }
                        if (current.IsRequired != isRequired)
                        {
                            await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", $"{stepName} 필수 여부", current.IsRequired ? "예" : "아니오", isRequired ? "예" : "아니오", request.Reason, changedByUserId, correlationId, cancellationToken);
                        }
                        if (current.PlannedDate != plannedDate)
                        {
                            await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", stepName, FormatDate(current.PlannedDate), FormatDate(plannedDate), request.Reason, changedByUserId, correlationId, cancellationToken);
                        }
                        if (current.Note != note)
                        {
                            await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", $"{stepName} 생산관리 코멘트", current.Note, note, request.Reason, changedByUserId, correlationId, cancellationToken);
                        }
                    }
                    if (current.AssignedUserId != assignedUserId || current.RequiredHeadcount != requiredHeadcount)
                    {
                        await UpdatePlanItemStaffingAsync(connection, transaction, current.ItemId!.Value, assignedUserId, requiredHeadcount, cancellationToken);
                        if (current.AssignedUserId != assignedUserId)
                        {
                            await InsertAuditAsync(
                                connection,
                                transaction,
                                projectId,
                                current.ItemId.Value,
                                "ProductionPlanItem",
                                $"{stepName} 담당자",
                                current.AssignedUserName,
                                assignedUserId is null ? null : await ReadUserDisplayNameAsync(connection, transaction, assignedUserId.Value, cancellationToken),
                                request.Reason,
                                changedByUserId,
                                correlationId,
                                cancellationToken);
                        }
                        if (current.RequiredHeadcount != requiredHeadcount)
                        {
                            await InsertAuditAsync(
                                connection,
                                transaction,
                                projectId,
                                current.ItemId.Value,
                                "ProductionPlanItem",
                                $"{stepName} 필요 인원",
                                current.RequiredHeadcount?.ToString(),
                                requiredHeadcount?.ToString(),
                                request.Reason,
                                changedByUserId,
                                correlationId,
                                cancellationToken);
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
                var isRequired = requestedItem.IsRequired ?? current?.IsRequired ?? false;
                var assignedUserId = requestedItem.AssignedUserId;
                var requiredHeadcount = requestedItem.RequiredHeadcount;
                if (current is null)
                {
                    var itemId = Guid.NewGuid();
                    await InsertCustomPlanItemAsync(connection, transaction, itemId, planId, customSequence++, stepName, isRequired, plannedDate, note, cancellationToken);
                    if (assignedUserId is not null || requiredHeadcount is not null)
                    {
                        await UpdatePlanItemStaffingAsync(connection, transaction, itemId, assignedUserId, requiredHeadcount, cancellationToken);
                    }
                    await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem", "사용자 추가 항목", null, stepName, request.Reason, changedByUserId, correlationId, cancellationToken);
                    if (plannedDate is not null)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem", stepName, null, plannedDate.Value.ToString("yyyy-MM-dd"), request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                }
                else if (current.StepName != stepName || current.IsRequired != isRequired || current.PlannedDate != plannedDate || current.Note != note || current.SequenceNumber != requestedItem.SequenceNumber)
                {
                    await UpdateCustomPlanItemAsync(connection, transaction, current.ItemId!.Value, requestedItem.SequenceNumber ?? customSequence++, stepName, isRequired, plannedDate, note, cancellationToken);
                    if (current.StepName != stepName)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", "계획 항목명", current.StepName, stepName, request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                    if (current.IsRequired != isRequired)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", $"{stepName} 필수 여부", current.IsRequired ? "예" : "아니오", isRequired ? "예" : "아니오", request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                    if (current.PlannedDate != plannedDate)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", stepName, FormatDate(current.PlannedDate), FormatDate(plannedDate), request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                    if (current.Note != note)
                    {
                        await InsertAuditAsync(connection, transaction, projectId, current.ItemId.Value, "ProductionPlanItem", $"{stepName} 생산관리 코멘트", current.Note, note, request.Reason, changedByUserId, correlationId, cancellationToken);
                    }
                }
                if (current is not null && (current.AssignedUserId != assignedUserId || current.RequiredHeadcount != requiredHeadcount))
                {
                    await UpdatePlanItemStaffingAsync(connection, transaction, current.ItemId!.Value, assignedUserId, requiredHeadcount, cancellationToken);
                    if (current.AssignedUserId != assignedUserId)
                    {
                        await InsertAuditAsync(
                            connection,
                            transaction,
                            projectId,
                            current.ItemId.Value,
                            "ProductionPlanItem",
                            $"{stepName} 담당자",
                            current.AssignedUserName,
                            assignedUserId is null ? null : await ReadUserDisplayNameAsync(connection, transaction, assignedUserId.Value, cancellationToken),
                            request.Reason,
                            changedByUserId,
                            correlationId,
                            cancellationToken);
                    }
                    if (current.RequiredHeadcount != requiredHeadcount)
                    {
                        await InsertAuditAsync(
                            connection,
                            transaction,
                            projectId,
                            current.ItemId.Value,
                            "ProductionPlanItem",
                            $"{stepName} 필요 인원",
                            current.RequiredHeadcount?.ToString(),
                            requiredHeadcount?.ToString(),
                            request.Reason,
                            changedByUserId,
                            correlationId,
                            cancellationToken);
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

    public async Task<ProductionPlanningMutationResult<ProductionPlanningResponse>> UpdateSetDefaultAsync(
        Guid projectId,
        UpdateProductionPlanSetDefaultRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var project = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
        if (project is null) return ProductionPlanningMutationResult<ProductionPlanningResponse>.NotFound();
        if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("현재 프로젝트 상태에서는 전체 세트 기본계획을 수정할 수 없습니다.");
        var plan = await ReadPlanHeaderAsync(connection, transaction, projectId, cancellationToken);
        if (plan is null || plan.ModelVersion != ProductionControlModelVersions.LinkedV1 || project.StructureMode != "Ul891Set")
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                new Dictionary<string, string[]> { ["setDefault"] = ["세트형 UL891 연결 생산계획에서만 전체 기본계획을 저장할 수 있습니다."] });

        await EnsureSetPlanScopeAsync(connection, transaction, projectId, null, changedByUserId, cancellationToken);
        Guid defaultId;
        int defaultRowVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select id,row_version from project_production_plan_set_defaults where production_plan_id=@plan_id for update;";
            command.Parameters.AddWithValue("plan_id", plan.PlanId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("전체 세트 기본계획을 준비하지 못했습니다. 다시 시도해 주세요.");
            defaultId = reader.GetGuid(0);
            defaultRowVersion = reader.GetInt32(1);
        }
        if (request.ExpectedRowVersion is null || request.ExpectedRowVersion.Value != defaultRowVersion)
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 전체 세트 기본계획을 먼저 수정했습니다. 최신 내용을 다시 불러와 주세요.");

        var requestedItems = request.Items ?? [];
        var existingItems = await ReadPlanItemsAsync(connection, transaction, plan.PlanId, cancellationToken);
        var existingById = existingItems.Where(item => item.ItemId is not null).ToDictionary(item => item.ItemId!.Value);
        var currentDefault = await ReadSetDefaultAsync(connection, transaction, plan.PlanId, existingItems, cancellationToken);
        var currentById = currentDefault?.Items.Where(item => item.ItemId is not null).ToDictionary(item => item.ItemId!.Value)
            ?? new Dictionary<Guid, ProductionPlanItemResponse>();
        var errors = new Dictionary<string, string[]>();
        for (var index = 0; index < requestedItems.Count; index++)
        {
            var requested = requestedItems[index];
            if (requested.ItemId is null || !existingById.ContainsKey(requested.ItemId.Value))
            {
                errors[$"items[{index}].itemId"] = ["현재 프로젝트의 생산계획 항목을 다시 선택해 주세요."];
                continue;
            }
            if (requested.PlannedStartDate is not null && requested.PlannedEndDate is not null && requested.PlannedStartDate > requested.PlannedEndDate)
                errors[$"items[{index}].plannedEndDate"] = ["계획 종료일은 시작일보다 빠를 수 없습니다."];
            if (requested.RequiredHeadcount is not null && requested.RequiredHeadcount is (< 1 or > 999))
                errors[$"items[{index}].requiredHeadcount"] = ["필요 인원은 1명부터 999명까지 입력할 수 있습니다."];
            if (requested.AssignedUserId is not null
                && await ReadUserDisplayNameAsync(connection, transaction, requested.AssignedUserId.Value, cancellationToken) is null)
                errors[$"items[{index}].assignedUserId"] = ["현재 사용 가능한 담당자를 다시 선택해 주세요."];
            if (currentById.TryGetValue(requested.ItemId.Value, out var current)
                && requested.ExpectedRowVersion is not null
                && current.RowVersion != requested.ExpectedRowVersion.Value)
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 전체 세트 기본계획 항목을 먼저 수정했습니다. 최신 내용을 다시 불러와 주세요.");
        }
        if (errors.Count > 0) return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(errors);

        var affectedScopes = new HashSet<Guid>();
        foreach (var requested in requestedItems)
        {
            var itemId = requested.ItemId!.Value;
            var note = TrimToNull(requested.Note);
            currentById.TryGetValue(itemId, out var current);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into project_production_plan_set_default_values (
                        id,set_default_id,production_plan_item_id,
                        planned_start_date,planned_end_date,assigned_user_id,
                        required_headcount,note,row_version,updated_at_utc
                    ) values (
                        @id,@default_id,@item_id,
                        @planned_start_date,@planned_end_date,@assigned_user_id,
                        @required_headcount,@note,1,now()
                    )
                    on conflict (set_default_id,production_plan_item_id) do update
                    set planned_start_date=excluded.planned_start_date,
                        planned_end_date=excluded.planned_end_date,
                        assigned_user_id=excluded.assigned_user_id,
                        required_headcount=excluded.required_headcount,
                        note=excluded.note,
                        row_version=project_production_plan_set_default_values.row_version+1,
                        updated_at_utc=now();
                    """;
                command.Parameters.AddWithValue("id", Guid.NewGuid());
                command.Parameters.AddWithValue("default_id", defaultId);
                command.Parameters.AddWithValue("item_id", itemId);
                command.Parameters.Add("planned_start_date", NpgsqlDbType.Date).Value = requested.PlannedStartDate ?? (object)DBNull.Value;
                command.Parameters.Add("planned_end_date", NpgsqlDbType.Date).Value = requested.PlannedEndDate ?? (object)DBNull.Value;
                command.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = requested.AssignedUserId ?? (object)DBNull.Value;
                command.Parameters.Add("required_headcount", NpgsqlDbType.Integer).Value = requested.RequiredHeadcount ?? (object)DBNull.Value;
                command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update project_production_plan_set_item_values value
                    set planned_start_date=@planned_start_date,
                        planned_end_date=@planned_end_date,
                        assigned_user_id=@assigned_user_id,
                        required_headcount=@required_headcount,
                        note=@note,
                        row_version=value.row_version+1,
                        updated_at_utc=now()
                    from project_production_plan_set_scopes scope
                    join ul891_set_instances instance on instance.id=scope.set_instance_id
                    where value.set_scope_id=scope.id
                      and scope.production_plan_id=@plan_id
                      and instance.status='Active'
                      and value.production_plan_item_id=@item_id
                      and (
                          @overwrite
                          or (
                              value.planned_start_date is null and value.planned_end_date is null
                              and value.assigned_user_id is null and value.required_headcount is null
                              and value.note is null
                          )
                      )
                    returning value.set_scope_id;
                    """;
                command.Parameters.AddWithValue("plan_id", plan.PlanId);
                command.Parameters.AddWithValue("item_id", itemId);
                command.Parameters.AddWithValue("overwrite", request.OverwriteExisting == true);
                command.Parameters.Add("planned_start_date", NpgsqlDbType.Date).Value = requested.PlannedStartDate ?? (object)DBNull.Value;
                command.Parameters.Add("planned_end_date", NpgsqlDbType.Date).Value = requested.PlannedEndDate ?? (object)DBNull.Value;
                command.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = requested.AssignedUserId ?? (object)DBNull.Value;
                command.Parameters.Add("required_headcount", NpgsqlDbType.Integer).Value = requested.RequiredHeadcount ?? (object)DBNull.Value;
                command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) affectedScopes.Add(reader.GetGuid(0));
            }

            var oldPeriod = FormatPeriod(current?.PlannedStartDate, current?.PlannedEndDate);
            var newPeriod = FormatPeriod(requested.PlannedStartDate, requested.PlannedEndDate);
            if (oldPeriod != newPeriod)
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"전체 세트 기본계획 · {existingById[itemId].StepName} 계획 기간", oldPeriod, newPeriod,
                    request.Reason, changedByUserId, correlationId, cancellationToken);
            if (current?.AssignedUserId != requested.AssignedUserId)
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"전체 세트 기본계획 · {existingById[itemId].StepName} 담당자", current?.AssignedUserName,
                    requested.AssignedUserId is null ? null : await ReadUserDisplayNameAsync(connection, transaction, requested.AssignedUserId.Value, cancellationToken),
                    request.Reason, changedByUserId, correlationId, cancellationToken);
            if (current?.RequiredHeadcount != requested.RequiredHeadcount)
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"전체 세트 기본계획 · {existingById[itemId].StepName} 필요 인원",
                    current?.RequiredHeadcount?.ToString(CultureInfo.InvariantCulture),
                    requested.RequiredHeadcount?.ToString(CultureInfo.InvariantCulture),
                    request.Reason, changedByUserId, correlationId, cancellationToken);
            if (current?.Note != note)
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"전체 세트 기본계획 · {existingById[itemId].StepName} 생산관리 코멘트", current?.Note, note,
                    request.Reason, changedByUserId, correlationId, cancellationToken);
        }

        if (affectedScopes.Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update project_production_plan_set_scopes
                set row_version=row_version+1,updated_at_utc=now(),updated_by_user_id=@user_id
                where id=any(@scope_ids);
                """;
            command.Parameters.AddWithValue("user_id", changedByUserId);
            command.Parameters.AddWithValue("scope_ids", affectedScopes.ToArray());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update project_production_plan_set_defaults
                set row_version=row_version+1,updated_at_utc=now(),updated_by_user_id=@user_id
                where id=@default_id and row_version=@expected_row_version;
                """;
            command.Parameters.AddWithValue("user_id", changedByUserId);
            command.Parameters.AddWithValue("default_id", defaultId);
            command.Parameters.AddWithValue("expected_row_version", defaultRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 전체 세트 기본계획을 먼저 수정했습니다. 최신 내용을 다시 불러와 주세요.");
        }
        await transaction.CommitAsync(cancellationToken);
        var updated = await GetProjectPlanAsync(projectId, null, cancellationToken);
        return updated.Status == ProductionPlanningReadStatus.Success && updated.Value is not null
            ? ProductionPlanningMutationResult<ProductionPlanningResponse>.Success(updated.Value)
            : ProductionPlanningMutationResult<ProductionPlanningResponse>.NotFound();
    }

    public async Task<ProductionPlanningMutationResult<ProductionPlanningResponse>> UpdateSetScopeAsync(
        Guid projectId,
        Guid setInstanceId,
        UpdateProductionPlanSetScopeRequest request,
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
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("현재 프로젝트 상태에서는 세트 생산계획을 수정할 수 없습니다.");
        }
        var plan = await ReadPlanHeaderAsync(connection, transaction, projectId, cancellationToken);
        if (plan is null
            || plan.ModelVersion != ProductionControlModelVersions.LinkedV1
            || project.StructureMode != "Ul891Set")
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                new Dictionary<string, string[]> { ["setInstanceId"] = ["세트형 UL891 연결 생산계획에서만 세트 일정을 수정할 수 있습니다."] });
        }

        SetScopeSnapshot? scope;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select scope.id,
                       scope.row_version,
                       instance.status,
                       spec.name,
                       spec.spec_no,
                       instance.instance_number
                from project_production_plan_set_scopes scope
                join ul891_set_instances instance on instance.id = scope.set_instance_id
                join ul891_set_specs spec on spec.id = instance.spec_id
                where scope.production_plan_id = @plan_id
                  and scope.set_instance_id = @set_instance_id
                  and spec.project_id = @project_id
                for update of scope;
                """;
            command.Parameters.AddWithValue("plan_id", plan.PlanId);
            command.Parameters.AddWithValue("set_instance_id", setInstanceId);
            command.Parameters.AddWithValue("project_id", projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            scope = await reader.ReadAsync(cancellationToken)
                ? new SetScopeSnapshot(
                    reader.GetGuid(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5))
                : null;
        }
        if (scope is null)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(
                new Dictionary<string, string[]> { ["setInstanceId"] = ["현재 프로젝트에 속한 세트를 다시 선택해 주세요."] });
        }
        if (scope.Status != "Active")
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("취소된 세트의 생산계획은 이력 조회만 가능합니다.");
        }
        if (request.ExpectedRowVersion is null || request.ExpectedRowVersion.Value != scope.RowVersion)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 이 세트 계획을 먼저 수정했습니다. 최신 내용을 다시 불러와 주세요.");
        }

        var requestedItems = request.Items ?? [];
        var existingItems = await ReadPlanItemsAsync(connection, transaction, plan.PlanId, cancellationToken);
        var existingById = existingItems
            .Where(item => item.ItemId is not null)
            .ToDictionary(item => item.ItemId!.Value);
        var currentValues = await ReadSetItemValuesAsync(connection, transaction, scope.ScopeId, cancellationToken);
        var errors = new Dictionary<string, string[]>();
        for (var index = 0; index < requestedItems.Count; index++)
        {
            var requested = requestedItems[index];
            if (requested.ItemId is null || !existingById.ContainsKey(requested.ItemId.Value))
            {
                errors[$"items[{index}].itemId"] = ["현재 프로젝트의 생산계획 항목을 다시 선택해 주세요."];
                continue;
            }
            if (requested.PlannedStartDate is not null
                && requested.PlannedEndDate is not null
                && requested.PlannedStartDate > requested.PlannedEndDate)
            {
                errors[$"items[{index}].plannedEndDate"] = ["계획 종료일은 시작일보다 빠를 수 없습니다."];
            }
            if (requested.RequiredHeadcount is not null && requested.RequiredHeadcount is (< 1 or > 999))
            {
                errors[$"items[{index}].requiredHeadcount"] = ["필요 인원은 1명부터 999명까지 입력할 수 있습니다."];
            }
            if (requested.AssignedUserId is not null
                && await ReadUserDisplayNameAsync(connection, transaction, requested.AssignedUserId.Value, cancellationToken) is null)
            {
                errors[$"items[{index}].assignedUserId"] = ["현재 사용 가능한 담당자를 다시 선택해 주세요."];
            }
            if (currentValues.TryGetValue(requested.ItemId.Value, out var current)
                && requested.ExpectedRowVersion is not null
                && requested.ExpectedRowVersion.Value != current.RowVersion)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 이 세트 계획 항목을 먼저 수정했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }
        if (errors.Count > 0)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(errors);
        }

        var scopeLabel = $"{scope.SpecName} · {scope.InstanceNumber}번 세트";
        foreach (var requested in requestedItems)
        {
            var itemId = requested.ItemId!.Value;
            var item = existingById[itemId];
            currentValues.TryGetValue(itemId, out var current);
            var note = TrimToNull(requested.Note);
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into project_production_plan_set_item_values (
                        id, set_scope_id, production_plan_item_id,
                        planned_start_date, planned_end_date, assigned_user_id,
                        required_headcount, note, row_version, updated_at_utc
                    )
                    values (
                        @id, @scope_id, @item_id,
                        @planned_start_date, @planned_end_date, @assigned_user_id,
                        @required_headcount, @note, 1, now()
                    )
                    on conflict (set_scope_id, production_plan_item_id) do update
                    set planned_start_date = excluded.planned_start_date,
                        planned_end_date = excluded.planned_end_date,
                        assigned_user_id = excluded.assigned_user_id,
                        required_headcount = excluded.required_headcount,
                        note = excluded.note,
                        row_version = project_production_plan_set_item_values.row_version + 1,
                        updated_at_utc = now();
                    """;
                command.Parameters.AddWithValue("id", current?.ValueId ?? Guid.NewGuid());
                command.Parameters.AddWithValue("scope_id", scope.ScopeId);
                command.Parameters.AddWithValue("item_id", itemId);
                command.Parameters.Add("planned_start_date", NpgsqlDbType.Date).Value = requested.PlannedStartDate ?? (object)DBNull.Value;
                command.Parameters.Add("planned_end_date", NpgsqlDbType.Date).Value = requested.PlannedEndDate ?? (object)DBNull.Value;
                command.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = requested.AssignedUserId ?? (object)DBNull.Value;
                command.Parameters.Add("required_headcount", NpgsqlDbType.Integer).Value = requested.RequiredHeadcount ?? (object)DBNull.Value;
                command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var oldPeriod = FormatPeriod(current?.PlannedStartDate, current?.PlannedEndDate);
            var newPeriod = FormatPeriod(requested.PlannedStartDate, requested.PlannedEndDate);
            if (oldPeriod != newPeriod)
            {
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"{scopeLabel} · {item.StepName} 계획 기간", oldPeriod, newPeriod, request.Reason,
                    changedByUserId, correlationId, cancellationToken);
            }
            if (current?.AssignedUserId != requested.AssignedUserId)
            {
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"{scopeLabel} · {item.StepName} 담당자", current?.AssignedUserName,
                    requested.AssignedUserId is null ? null : await ReadUserDisplayNameAsync(connection, transaction, requested.AssignedUserId.Value, cancellationToken),
                    request.Reason, changedByUserId, correlationId, cancellationToken);
            }
            if (current?.RequiredHeadcount != requested.RequiredHeadcount)
            {
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"{scopeLabel} · {item.StepName} 필요 인원", current?.RequiredHeadcount?.ToString(CultureInfo.InvariantCulture),
                    requested.RequiredHeadcount?.ToString(CultureInfo.InvariantCulture), request.Reason,
                    changedByUserId, correlationId, cancellationToken);
            }
            if (current?.Note != note)
            {
                await InsertAuditAsync(connection, transaction, projectId, itemId, "ProductionPlanItem",
                    $"{scopeLabel} · {item.StepName} 생산관리 코멘트", current?.Note, note, request.Reason,
                    changedByUserId, correlationId, cancellationToken);
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update project_production_plan_set_scopes
                set row_version = row_version + 1,
                    updated_at_utc = now(),
                    updated_by_user_id = @user_id
                where id = @scope_id and row_version = @expected_row_version;
                """;
            command.Parameters.AddWithValue("scope_id", scope.ScopeId);
            command.Parameters.AddWithValue("expected_row_version", scope.RowVersion);
            command.Parameters.AddWithValue("user_id", changedByUserId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 이 세트 계획을 먼저 수정했습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        var updated = await GetProjectPlanAsync(projectId, setInstanceId, cancellationToken);
        return updated.Status == ProductionPlanningReadStatus.Success && updated.Value is not null
            ? ProductionPlanningMutationResult<ProductionPlanningResponse>.Success(updated.Value)
            : ProductionPlanningMutationResult<ProductionPlanningResponse>.NotFound();
    }

    private async Task<ProductionPlanningMutationResult<ProductionPlanningResponse>> UpdateLinkedProjectPlanAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ProjectSnapshot project,
        PlanHeader currentPlan,
        UpdateProductionPlanningRequest request,
        Guid changedByUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var requestedItems = request.Items ?? [];
        var errors = ValidateLinkedPlanItemUpdates(requestedItems);
        if (project.StructureMode == "Ul891Set")
        {
            for (var index = 0; index < requestedItems.Count; index++)
            {
                var requested = requestedItems[index];
                const string message = "UL891 세트형 프로젝트의 계획 일정은 세트 일정에서 수정해 주세요.";
                if (requested.PlannedDate is not null)
                {
                    errors[$"items[{index}].plannedDate"] = [message];
                }
                if (requested.PlannedStartDate is not null)
                {
                    errors[$"items[{index}].plannedStartDate"] = [message];
                }
                if (requested.PlannedEndDate is not null)
                {
                    errors[$"items[{index}].plannedEndDate"] = [message];
                }
                if (requested.AssignedUserId is not null)
                {
                    errors[$"items[{index}].assignedUserId"] = [message];
                }
                if (requested.RequiredHeadcount is not null)
                {
                    errors[$"items[{index}].requiredHeadcount"] = [message];
                }
                if (!string.IsNullOrWhiteSpace(requested.Note))
                {
                    errors[$"items[{index}].note"] = [message];
                }
            }
        }
        await AppendPlanItemStaffingErrorsAsync(
            connection,
            transaction,
            requestedItems,
            errors,
            cancellationToken);
        if (errors.Count > 0)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(errors);
        }

        var existing = await ReadPlanItemsAsync(connection, transaction, currentPlan.PlanId, cancellationToken);
        var existingById = existing
            .Where(item => item.ItemId is not null)
            .ToDictionary(item => item.ItemId!.Value);
        for (var index = 0; index < requestedItems.Count; index++)
        {
            var requested = requestedItems[index];
            if (requested.ItemId is not null && !existingById.ContainsKey(requested.ItemId.Value))
            {
                errors[$"items[{index}].itemId"] = ["현재 프로젝트에 속한 생산계획 항목이 아닙니다."];
                continue;
            }
            if (requested.ItemId is not null
                && requested.ExpectedRowVersion is not null
                && existingById[requested.ItemId.Value].RowVersion != requested.ExpectedRowVersion)
            {
                return ProductionPlanningMutationResult<ProductionPlanningResponse>.Conflict("다른 사용자가 먼저 수정했습니다. 새로고침 후 다시 시도해 주세요.");
            }
        }
        if (errors.Count > 0)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(errors);
        }

        var manufacturingDefinitionKeys = await ReadProjectManufacturingDefinitionKeysAsync(
            connection,
            transaction,
            project.ProjectId,
            cancellationToken);
        var qualityDefinitionKeys = await ReadQualityDefinitionKeysAsync(
            connection,
            transaction,
            cancellationToken);
        for (var itemIndex = 0; itemIndex < requestedItems.Count; itemIndex++)
        {
            var item = requestedItems[itemIndex];
            var itemConnections = item.Connections ?? [];
            for (var connectionIndex = 0; connectionIndex < itemConnections.Count; connectionIndex++)
            {
                var source = itemConnections[connectionIndex];
                var field = $"items[{itemIndex}].connections[{connectionIndex}]";
                if (!ProductionControlSourceCodes.IsSupported(source.SourceCode))
                {
                    errors[field] = ["지원하지 않는 실적 연결값입니다."];
                    continue;
                }
                if (ProductionControlSourceCodes.RequiresManufacturingDefinition(source.SourceCode))
                {
                    if (source.SourceDefinitionKey is null || !manufacturingDefinitionKeys.Contains(source.SourceDefinitionKey.Value))
                    {
                        errors[field] = ["현재 프로젝트의 제조 항목을 다시 선택해 주세요."];
                    }
                }
                else if (ProductionControlSourceCodes.RequiresQualityDefinition(source.SourceCode))
                {
                    if (source.SourceDefinitionKey is null
                        || !qualityDefinitionKeys.TryGetValue(source.SourceCode, out var keys)
                        || !keys.Contains(source.SourceDefinitionKey.Value))
                    {
                        errors[field] = ["현재 품질 양식의 검사 항목을 다시 선택해 주세요."];
                    }
                }
                else if (source.SourceDefinitionKey is not null)
                {
                    errors[field] = ["이 실적 연결값에는 세부 항목을 지정할 수 없습니다."];
                }
            }
        }
        if (errors.Count > 0)
        {
            return ProductionPlanningMutationResult<ProductionPlanningResponse>.Validation(errors);
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                update project_production_plans
                set notes = @notes,
                    row_version = row_version + 1,
                    updated_at_utc = now(),
                    updated_by_user_id = @user_id
                where id = @plan_id;
                """;
            command.Parameters.AddWithValue("plan_id", currentPlan.PlanId);
            command.Parameters.Add("notes", NpgsqlDbType.Text).Value = TrimToNull(request.Notes) ?? (object)DBNull.Value;
            command.Parameters.AddWithValue("user_id", changedByUserId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        if (request.Items is not null)
        {
            await using var shiftCommand = connection.CreateCommand();
            shiftCommand.Transaction = transaction;
            shiftCommand.CommandText = """
                update project_production_plan_items
                set sequence_number = sequence_number + 1000000
                where production_plan_id = @plan_id;
                """;
            shiftCommand.Parameters.AddWithValue("plan_id", currentPlan.PlanId);
            await shiftCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var nextSequence = 1;
        foreach (var requested in requestedItems)
        {
            var existingItem = requested.ItemId is not null ? existingById[requested.ItemId.Value] : null;
            var sequence = requested.SequenceNumber is > 0 ? requested.SequenceNumber.Value : nextSequence;
            nextSequence = Math.Max(nextSequence + 1, sequence + 1);
            if (requested.IsDeleted == true)
            {
                if (existingItem is not null)
                {
                    await using var deleteCommand = connection.CreateCommand();
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = """
                        update project_production_plan_items
                        set is_active = false,
                            sequence_number = @sequence_number,
                            row_version = row_version + 1,
                            updated_at_utc = now()
                        where id = @item_id;
                        """;
                    deleteCommand.Parameters.AddWithValue("item_id", existingItem.ItemId!.Value);
                    deleteCommand.Parameters.AddWithValue("sequence_number", 2000000000 - sequence);
                    await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                continue;
            }

            var itemId = existingItem?.ItemId ?? Guid.NewGuid();
            var definitionKey = existingItem?.DefinitionKey ?? requested.DefinitionKey ?? Guid.NewGuid();
            var stepName = TrimToNull(requested.StepName)!;
            var start = requested.PlannedStartDate;
            var end = requested.PlannedEndDate;
            var note = TrimToNull(requested.Note);
            var assignedUserId = requested.AssignedUserId;
            var requiredHeadcount = requested.RequiredHeadcount;
            var isRequired = requested.IsRequired ?? existingItem?.IsRequired ?? false;
            if (existingItem is null)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = """
                    insert into project_production_plan_items (
                        id, production_plan_id, template_step_id, sequence_number, step_name_snapshot,
                        is_required, is_active, planned_date, planned_start_date, planned_end_date,
                        definition_key, assigned_user_id, required_headcount, note
                    )
                    values (
                        @id, @plan_id, null, @sequence_number, @step_name,
                        @is_required, true, null, @planned_start_date, @planned_end_date,
                        @definition_key, @assigned_user_id, @required_headcount, @note
                    );
                    """;
                insertCommand.Parameters.AddWithValue("id", itemId);
                insertCommand.Parameters.AddWithValue("plan_id", currentPlan.PlanId);
                insertCommand.Parameters.AddWithValue("sequence_number", sequence);
                insertCommand.Parameters.AddWithValue("step_name", stepName);
                insertCommand.Parameters.AddWithValue("is_required", isRequired);
                insertCommand.Parameters.Add("planned_start_date", NpgsqlDbType.Date).Value = start ?? (object)DBNull.Value;
                insertCommand.Parameters.Add("planned_end_date", NpgsqlDbType.Date).Value = end ?? (object)DBNull.Value;
                insertCommand.Parameters.AddWithValue("definition_key", definitionKey);
                insertCommand.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = assignedUserId ?? (object)DBNull.Value;
                insertCommand.Parameters.Add("required_headcount", NpgsqlDbType.Integer).Value = requiredHeadcount ?? (object)DBNull.Value;
                insertCommand.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = """
                    update project_production_plan_items
                    set sequence_number = @sequence_number,
                        step_name_snapshot = @step_name,
                        is_required = @is_required,
                        planned_date = null,
                        planned_start_date = @planned_start_date,
                        planned_end_date = @planned_end_date,
                        assigned_user_id = @assigned_user_id,
                        required_headcount = @required_headcount,
                        note = @note,
                        row_version = row_version + 1,
                        updated_at_utc = now()
                    where id = @item_id;
                    """;
                updateCommand.Parameters.AddWithValue("item_id", itemId);
                updateCommand.Parameters.AddWithValue("sequence_number", sequence);
                updateCommand.Parameters.AddWithValue("step_name", stepName);
                updateCommand.Parameters.AddWithValue("is_required", isRequired);
                updateCommand.Parameters.Add("planned_start_date", NpgsqlDbType.Date).Value = start ?? (object)DBNull.Value;
                updateCommand.Parameters.Add("planned_end_date", NpgsqlDbType.Date).Value = end ?? (object)DBNull.Value;
                updateCommand.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = assignedUserId ?? (object)DBNull.Value;
                updateCommand.Parameters.Add("required_headcount", NpgsqlDbType.Integer).Value = requiredHeadcount ?? (object)DBNull.Value;
                updateCommand.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var clearCommand = connection.CreateCommand())
            {
                clearCommand.Transaction = transaction;
                clearCommand.CommandText = "delete from project_production_plan_connections where production_plan_item_id = @item_id;";
                clearCommand.Parameters.AddWithValue("item_id", itemId);
                await clearCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var source in requested.Connections ?? [])
            {
                await using var connectionCommand = connection.CreateCommand();
                connectionCommand.Transaction = transaction;
                connectionCommand.CommandText = """
                    insert into project_production_plan_connections (
                        production_plan_item_id, source_code, source_definition_key
                    )
                    values (@item_id, @source_code, @source_definition_key);
                    """;
                connectionCommand.Parameters.AddWithValue("item_id", itemId);
                connectionCommand.Parameters.AddWithValue("source_code", source.SourceCode);
                connectionCommand.Parameters.Add("source_definition_key", NpgsqlDbType.Uuid).Value = source.SourceDefinitionKey ?? (object)DBNull.Value;
                await connectionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            if (existingItem?.Note != note)
            {
                await InsertAuditAsync(
                    connection,
                    transaction,
                    project.ProjectId,
                    itemId,
                    "ProductionPlanItem",
                    $"{stepName} 생산관리 코멘트",
                    existingItem?.Note,
                    note,
                    request.Reason,
                    changedByUserId,
                    correlationId,
                    cancellationToken);
            }
            if (existingItem?.AssignedUserId != assignedUserId)
            {
                await InsertAuditAsync(
                    connection,
                    transaction,
                    project.ProjectId,
                    itemId,
                    "ProductionPlanItem",
                    $"{stepName} 담당자",
                    existingItem?.AssignedUserName,
                    assignedUserId is null
                        ? null
                        : await ReadUserDisplayNameAsync(connection, transaction, assignedUserId.Value, cancellationToken),
                    request.Reason,
                    changedByUserId,
                    correlationId,
                    cancellationToken);
            }
            if (existingItem?.RequiredHeadcount != requiredHeadcount)
            {
                await InsertAuditAsync(
                    connection,
                    transaction,
                    project.ProjectId,
                    itemId,
                    "ProductionPlanItem",
                    $"{stepName} 필요 인원",
                    existingItem?.RequiredHeadcount?.ToString(),
                    requiredHeadcount?.ToString(),
                    request.Reason,
                    changedByUserId,
                    correlationId,
                    cancellationToken);
            }
        }

        if (project.StructureMode == "Ul891Set")
        {
            await EnsureSetPlanScopeAsync(
                connection,
                transaction,
                project.ProjectId,
                null,
                changedByUserId,
                cancellationToken);
        }

        var assigneeResult = await UpdateAssigneesAsync(
            connection,
            transaction,
            project.ProjectId,
            request,
            changedByUserId,
            correlationId,
            cancellationToken);
        if (assigneeResult is not null)
        {
            return assigneeResult;
        }

        await InsertAuditAsync(
            connection,
            transaction,
            project.ProjectId,
            currentPlan.PlanId,
            "ProductionPlan",
            "연결형 생산계획",
            currentPlan.Notes,
            TrimToNull(request.Notes),
            request.Reason,
            changedByUserId,
            correlationId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<ProductionPlanningResponse>.Success(
            (await GetProjectPlanAsync(project.ProjectId, cancellationToken))!);
    }

    private static Dictionary<string, string[]> ValidateLinkedPlanItemUpdates(
        IReadOnlyList<ProductionPlanItemUpdateRequest> items)
    {
        var errors = new Dictionary<string, string[]>();
        if (items.Count == 0)
        {
            return errors;
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sequences = new HashSet<int>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.IsDeleted == true)
            {
                continue;
            }
            var prefix = $"items[{index}]";
            var name = TrimToNull(item.StepName);
            if (name is null)
            {
                errors[$"{prefix}.stepName"] = ["계획 항목명을 입력해 주세요."];
            }
            else if (name.Length > 120)
            {
                errors[$"{prefix}.stepName"] = ["계획 항목명은 120자 이하로 입력해 주세요."];
            }
            else if (!names.Add(Normalize(name)))
            {
                errors[$"{prefix}.stepName"] = ["같은 생산계획 안에서 동일한 항목명을 중복 사용할 수 없습니다."];
            }

            if (item.SequenceNumber is not null && (item.SequenceNumber < 1 || !sequences.Add(item.SequenceNumber.Value)))
            {
                errors[$"{prefix}.sequenceNumber"] = ["계획 항목 순서는 1 이상의 중복되지 않는 숫자여야 합니다."];
            }
            if ((item.PlannedStartDate is null) != (item.PlannedEndDate is null))
            {
                errors[$"{prefix}.plannedStartDate"] = ["계획 시작일과 종료일을 함께 입력해 주세요."];
            }
            else if (item.PlannedStartDate is not null && item.PlannedEndDate < item.PlannedStartDate)
            {
                errors[$"{prefix}.plannedEndDate"] = ["계획 종료일은 시작일보다 빠를 수 없습니다."];
            }
            if ((item.Connections ?? []).Count != 1)
            {
                errors[$"{prefix}.connections"] = ["실적 데이터 하나를 선택해 주세요."];
            }
        }
        return errors;
    }

    private static async Task AppendPlanItemStaffingErrorsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<ProductionPlanItemUpdateRequest> items,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        var requestedUserIds = new HashSet<Guid>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.IsDeleted == true)
            {
                continue;
            }

            if (item.RequiredHeadcount is < 1 or > 999)
            {
                errors[$"items[{index}].requiredHeadcount"] = ["필요 인원은 1명부터 999명까지 입력해 주세요."];
            }

            if (item.AssignedUserId is not null)
            {
                requestedUserIds.Add(item.AssignedUserId.Value);
            }
        }

        if (requestedUserIds.Count == 0)
        {
            return;
        }

        var allowedUserIds = (await ReadAssigneeCandidatesAsync(connection, transaction, cancellationToken))
            .SelectMany(group => group.Users)
            .Select(user => user.UserId)
            .ToHashSet();
        for (var index = 0; index < items.Count; index++)
        {
            var assignedUserId = items[index].AssignedUserId;
            if (items[index].IsDeleted != true
                && assignedUserId is not null
                && !allowedUserIds.Contains(assignedUserId.Value))
            {
                errors[$"items[{index}].assignedUserId"] = ["활성 담당자 후보에서 선택해 주세요."];
            }
        }
    }

    private static async Task<HashSet<Guid>> ReadProjectManufacturingDefinitionKeysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select definition_key
            from project_manufacturing_step_snapshots
            where project_id = @project_id and is_active;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var result = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetGuid(0));
        }
        return result;
    }

    private static async Task<Dictionary<string, HashSet<Guid>>> ReadQualityDefinitionKeysAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal)
        {
            [ProductionControlSourceCodes.IqcPassed] = []
        };
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select 'IQC_PASSED', definition_key
            from iqc_report_template_items;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)].Add(reader.GetGuid(1));
        }
        return result;
    }

    private static async Task<IReadOnlyList<ProjectManufacturingStepResponse>> ReadProjectManufacturingStepsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select definition_key, sequence_number, step_name_snapshot
            from project_manufacturing_step_snapshots
            where project_id = @project_id and is_active
            order by sequence_number;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var result = new List<ProjectManufacturingStepResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProjectManufacturingStepResponse(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetString(2)));
        }
        return result;
    }

    public Task<ProductionPlanningTemplateDownload> CreateBulkTemplateAsync(CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Production Planning");
        var headers = BulkExcelHeaders;
        var requiredColumns = new HashSet<int> { 3, 4 };
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
        worksheet.Cell(1, headers.Length + 1).Value = "* 표시 항목은 필수 입력값입니다. 프로젝트명 또는 PJT Code 중 하나는 필요합니다. 필수 여부는 예/아니오로 입력합니다.";
        worksheet.Cell(1, headers.Length + 1).Style.Font.Italic = true;
        worksheet.Cell(1, headers.Length + 1).Style.Alignment.WrapText = true;
        worksheet.Column(headers.Length + 1).Width = 54;

        var example = new[] { "UAT-PLAN", "PLAN-CODE", "UL67", "자재 도착", "예", "2026-07-01", "예시" };
        for (var index = 0; index < example.Length; index++)
        {
            worksheet.Cell(2, index + 1).Value = example[index];
        }
        ApplyExcelTemplateLayout(worksheet, headers.Length, headerRow: 1, wideColumns: [4, 7]);

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
            var rows = await ParseBulkExcelRowsAsync(connection, null, bytes, null, cancellationToken);
            return ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>.Success(BuildExcelPreview(fileSha256, rows));
        }
        catch (InvalidDataException ex)
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>.Validation(new Dictionary<string, string[]> { ["file"] = [ex.Message] });
        }
    }

    public async Task<ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>> PreviewProjectExcelAsync(
        Guid projectId,
        string fileName,
        byte[] bytes,
        string fileSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dataSource = CreateDataSource();
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var project = await ReadProjectAsync(connection, null, projectId, cancellationToken);
            if (project is null)
            {
                return ProductionPlanningMutationResult<ProductionPlanningExcelPreviewResponse>.NotFound();
            }

            var rows = await ParseBulkExcelRowsAsync(connection, null, bytes, project, cancellationToken);
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
        var parsedRows = await ParseBulkExcelRowsAsync(connection, null, bytes, null, cancellationToken);
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
                var required = row.IsRequired ?? current?.IsRequired ?? false;
                if (createdPlan && row.TemplateStepId is not null)
                {
                    await InsertTemplatePlanItemAsync(
                        connection,
                        transaction,
                        itemId,
                        planId,
                        new ProductionTemplateStepResponse(row.TemplateStepId.Value, nextSequence, row.StepName!, required),
                        row.StepName!,
                        required,
                        row.PlannedDate,
                        row.Note,
                        cancellationToken);
                }
                else
                {
                    await InsertCustomPlanItemAsync(connection, transaction, itemId, planId, nextSequence, row.StepName!, required, row.PlannedDate, row.Note, cancellationToken);
                }
                await InsertAuditAsync(connection, transaction, project.ProjectId, itemId, "ProductionPlanItem", row.StepName!, null, FormatDate(row.PlannedDate), reason, changedByUserId, correlationId, cancellationToken, "Excel");
            }
            else
            {
                var required = row.IsRequired ?? current.IsRequired;
                if (current.PlannedDate != row.PlannedDate || current.Note != row.Note || current.IsRequired != required)
                {
                    await UpdatePlanItemAsync(connection, transaction, current.ItemId!.Value, current.StepName, required, row.PlannedDate, row.Note, cancellationToken);
                    await InsertAuditAsync(connection, transaction, project.ProjectId, current.ItemId.Value, "ProductionPlanItem", row.StepName!, FormatDate(current.PlannedDate), FormatDate(row.PlannedDate), reason, changedByUserId, correlationId, cancellationToken, "Excel");
                    if (current.IsRequired != required)
                    {
                        await InsertAuditAsync(connection, transaction, project.ProjectId, current.ItemId.Value, "ProductionPlanItem", $"{row.StepName} 필수 여부", current.IsRequired ? "예" : "아니오", required ? "예" : "아니오", reason, changedByUserId, correlationId, cancellationToken, "Excel");
                    }
                }
            }

            await ApplyAssigneesFromExcelAsync(connection, transaction, project.ProjectId, row, changedByUserId, reason, correlationId, cancellationToken);
            appliedProjectIds.Add(project.ProjectId);
        }

        await InsertProductionPlanningImportBatchAsync(connection, transaction, fileName, bytes.Length, fileSha256, parsedRows.Count, saveable.Count, parsedRows.Count - saveable.Count, changedByUserId, reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Success(
            new ProductionPlanningExcelApplyResponse(saveable.Count, parsedRows.Count - saveable.Count, appliedProjectIds.ToList()));
    }

    public async Task<ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>> ApplyProjectExcelAsync(
        Guid projectId,
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
        var projectContext = await ReadProjectAsync(connection, null, projectId, cancellationToken);
        if (projectContext is null)
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.NotFound();
        }

        var parsedRows = await ParseBulkExcelRowsAsync(connection, null, bytes, projectContext, cancellationToken);
        var saveable = parsedRows.Where(row => row.IsSaveable).ToList();
        if (saveable.Count == 0)
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                new Dictionary<string, string[]> { ["rows"] = ["저장 가능한 생산계획 항목이 없습니다."] });
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var project = await LockProjectAsync(connection, transaction, projectId, cancellationToken);
        if (project is null)
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.NotFound();
        }

        if (!string.Equals(project.Status, "Active", StringComparison.Ordinal))
        {
            return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                new Dictionary<string, string[]> { ["project"] = ["진행 중 프로젝트만 생산계획 Excel을 적용할 수 있습니다."] });
        }

        foreach (var row in saveable)
        {
            if (row.ProjectId != projectId)
            {
                return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Validation(
                    new Dictionary<string, string[]> { ["rows"] = ["현재 프로젝트의 생산계획 Excel만 적용할 수 있습니다."] });
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
                var required = row.IsRequired ?? false;
                if (createdPlan && row.TemplateStepId is not null)
                {
                    await InsertTemplatePlanItemAsync(
                        connection,
                        transaction,
                        itemId,
                        planId,
                        new ProductionTemplateStepResponse(row.TemplateStepId.Value, nextSequence, row.StepName!, required),
                        row.StepName!,
                        required,
                        row.PlannedDate,
                        row.Note,
                        cancellationToken);
                }
                else
                {
                    await InsertCustomPlanItemAsync(connection, transaction, itemId, planId, nextSequence, row.StepName!, required, row.PlannedDate, row.Note, cancellationToken);
                }
                await InsertAuditAsync(connection, transaction, project.ProjectId, itemId, "ProductionPlanItem", row.StepName!, null, FormatDate(row.PlannedDate), reason, changedByUserId, correlationId, cancellationToken, "Excel");
            }
            else
            {
                var required = row.IsRequired ?? current.IsRequired;
                if (current.PlannedDate != row.PlannedDate || current.Note != row.Note || current.IsRequired != required)
                {
                    await UpdatePlanItemAsync(connection, transaction, current.ItemId!.Value, current.StepName, required, row.PlannedDate, row.Note, cancellationToken);
                    await InsertAuditAsync(connection, transaction, project.ProjectId, current.ItemId.Value, "ProductionPlanItem", row.StepName!, FormatDate(current.PlannedDate), FormatDate(row.PlannedDate), reason, changedByUserId, correlationId, cancellationToken, "Excel");
                    if (current.IsRequired != required)
                    {
                        await InsertAuditAsync(connection, transaction, project.ProjectId, current.ItemId.Value, "ProductionPlanItem", $"{row.StepName} 필수 여부", current.IsRequired ? "예" : "아니오", required ? "예" : "아니오", reason, changedByUserId, correlationId, cancellationToken, "Excel");
                    }
                }
            }

            await ApplyAssigneesFromExcelAsync(connection, transaction, project.ProjectId, row, changedByUserId, reason, correlationId, cancellationToken);
        }

        await InsertProductionPlanningImportBatchAsync(connection, transaction, fileName, bytes.Length, fileSha256, parsedRows.Count, saveable.Count, parsedRows.Count - saveable.Count, changedByUserId, reason, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProductionPlanningMutationResult<ProductionPlanningExcelApplyResponse>.Success(
            new ProductionPlanningExcelApplyResponse(saveable.Count, parsedRows.Count - saveable.Count, [projectId]));
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
        worksheet.Cell(2, 2).Value = "* 표시 항목은 입력 필수값입니다. 필수 여부는 예/아니오로 입력합니다.";
        worksheet.Cell(2, 2).Style.Font.Italic = true;
        worksheet.Cell(3, 1).Value = "생산단계 *";
        worksheet.Cell(3, 2).Value = "필수 여부";
        worksheet.Cell(3, 3).Value = "예정일";
        worksheet.Cell(3, 4).Value = "비고";
        worksheet.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
        var row = 4;
        foreach (var step in type.Steps.OrderBy(step => step.SequenceNumber))
        {
            worksheet.Cell(row, 1).Value = step.StepName;
            worksheet.Cell(row, 2).Value = step.IsRequired ? "예" : "아니오";
            worksheet.Cell(row, 3).Style.DateFormat.Format = "yyyy-mm-dd";
            worksheet.Cell(row, 4).Value = "";
            row++;
        }

        ApplyExcelTemplateLayout(worksheet, 4, headerRow: 3, wideColumns: [1, 4]);
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

    private static async Task InsertTemplatePlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, Guid planId, ProductionTemplateStepResponse step, string stepName, bool isRequired, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
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
        command.Parameters.AddWithValue("step_name", stepName);
        command.Parameters.AddWithValue("is_required", isRequired);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCustomPlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, Guid planId, int sequenceNumber, string stepName, bool isRequired, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into project_production_plan_items (
                id, production_plan_id, template_step_id, sequence_number, step_name_snapshot, is_required, planned_date, note
            )
            values (@id, @plan_id, null, @sequence_number, @step_name, @is_required, @planned_date, @note);
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("plan_id", planId);
        command.Parameters.AddWithValue("sequence_number", sequenceNumber);
        command.Parameters.AddWithValue("step_name", stepName);
        command.Parameters.AddWithValue("is_required", isRequired);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdatePlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, string stepName, bool isRequired, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_production_plan_items
            set step_name_snapshot = @step_name,
                is_required = @is_required,
                planned_date = @planned_date,
                note = @note,
                row_version = row_version + 1,
                updated_at_utc = now()
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.AddWithValue("step_name", stepName);
        command.Parameters.AddWithValue("is_required", isRequired);
        command.Parameters.Add("planned_date", NpgsqlDbType.Date).Value = plannedDate ?? (object)DBNull.Value;
        command.Parameters.Add("note", NpgsqlDbType.Text).Value = note ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdatePlanItemStaffingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid itemId,
        Guid? assignedUserId,
        int? requiredHeadcount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_production_plan_items
            set assigned_user_id = @assigned_user_id,
                required_headcount = @required_headcount,
                row_version = row_version + 1,
                updated_at_utc = now()
            where id = @id;
            """;
        command.Parameters.AddWithValue("id", itemId);
        command.Parameters.Add("assigned_user_id", NpgsqlDbType.Uuid).Value = assignedUserId ?? (object)DBNull.Value;
        command.Parameters.Add("required_headcount", NpgsqlDbType.Integer).Value = requiredHeadcount ?? (object)DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateCustomPlanItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, int sequenceNumber, string stepName, bool isRequired, DateOnly? plannedDate, string? note, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update project_production_plan_items
            set sequence_number = @sequence_number,
                step_name_snapshot = @step_name,
                is_required = @is_required,
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
        command.Parameters.AddWithValue("is_required", isRequired);
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
        var templateStepById = templateSteps.ToDictionary(step => step.TemplateStepId);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.TemplateStepId is not null && !templateStepById.ContainsKey(item.TemplateStepId.Value))
            {
                errors[$"items[{index}].templateStepId"] = ["현재 Item의 계획 항목이 아닙니다."];
                continue;
            }

            if (item.IsDeleted == true && item.ItemId is not null)
            {
                continue;
            }

            var stepName = TrimToNull(item.StepName)
                ?? (item.TemplateStepId is not null ? templateStepById[item.TemplateStepId.Value].StepName : null);
            if (stepName is null)
            {
                errors[$"items[{index}].stepName"] = ["계획 항목명을 입력해 주세요."];
                continue;
            }

            if (stepName.Length > 120)
            {
                errors[$"items[{index}].stepName"] = ["계획 항목명은 120자 이하로 입력해 주세요."];
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
        IReadOnlyList<ProjectManufacturingStepResponse> manufacturingSteps,
        IReadOnlyList<ProductionControlSourceCatalogItemResponse> availableSources,
        IReadOnlyList<ProjectAssigneeResponse> assignees,
        IReadOnlyList<AssigneeCandidateResponse> candidates,
        IReadOnlyList<NotificationFallbackResponse> fallbacks,
        bool isSetScoped = false,
        ProductionPlanSetScopeResponse? selectedScope = null,
        IReadOnlyList<ProductionPlanSetScopeResponse>? scopes = null,
        ProductionPlanSetDefaultResponse? setDefault = null)
    {
        var allAssignees = ProductionPlanningDomain.Responsibilities
            .Select(responsibility => FindAssigneeForResponsibility(assignees, responsibility) ?? new ProjectAssigneeResponse
            {
                ResponsibilityType = responsibility,
                ResponsibilityLabel = ProductionPlanningDomain.ResponsibilityLabel(responsibility)
            })
            .ToList();
        var status = ProductionPlanningDomain.CalculateStatus(plan?.ProductTypeId, items);
        if (isSetScoped && plan?.ProductTypeId is not null)
        {
            var statusScopes = selectedScope is null
                ? (scopes ?? []).Where(scope => scope.Status == "Active").ToList()
                : [selectedScope];
            var requiredCount = statusScopes.Sum(scope => scope.RequiredItemCount);
            var plannedCount = statusScopes.Sum(scope => scope.PlannedRequiredItemCount);
            status = requiredCount > 0 && requiredCount == plannedCount
                ? ProductionPlanningDomain.Planned
                : ProductionPlanningDomain.Planning;
        }
        return new ProductionPlanningResponse(
            project.ProjectId,
            project.ProjectTitle,
            project.ProjectCode,
            project.DeliveryDate,
            plan?.ModelVersion ?? ProductionControlModelVersions.Legacy,
            plan?.PlanId,
            plan?.RowVersion ?? 0,
            status,
            ProductionPlanningDomain.StatusLabel(status),
            plan?.ProductTypeId,
            plan?.TemplateId,
            plan?.ProductTypeCode,
            plan?.ProductTypeName,
            plan?.Notes,
            manufacturingSteps,
            availableSources,
            SortPlanItems(items),
            allAssignees,
            candidates,
            fallbacks,
            isSetScoped,
            selectedScope,
            scopes ?? [],
            setDefault);
    }

    private static IReadOnlyList<ProductionPlanItemResponse> SortPlanItems(IReadOnlyList<ProductionPlanItemResponse> items)
    {
        return items
            .OrderBy(item => (item.PlannedStartDate ?? item.PlannedDate) is null ? 1 : 0)
            .ThenBy(item => item.PlannedStartDate ?? item.PlannedDate)
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
                   sales_owner_user_id,
                   fat_required,
                   structure_mode
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
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetString(8))
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
                   sales_owner_user_id,
                   fat_required,
                   structure_mode
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
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetString(8))
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
                   pp.row_version,
                   pp.model_version,
                   pp.linked_plan_template_version_id,
                   pp.linked_manufacturing_template_version_id
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
                reader.GetInt32(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.IsDBNull(9) ? null : reader.GetGuid(9))
            : null;
    }

    private static async Task<IReadOnlyList<ProductionPlanItemResponse>> ReadPlanItemsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid planId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select item.id,
                   item.template_step_id,
                   item.sequence_number,
                   item.step_name_snapshot,
                   item.is_required,
                   item.planned_date,
                   item.note,
                   item.row_version,
                   item.definition_key,
                   item.planned_start_date,
                   item.planned_end_date,
                   item.assigned_user_id,
                   assigned.display_name,
                   item.required_headcount
            from project_production_plan_items item
            left join qms_users assigned on assigned.id = item.assigned_user_id
            where item.production_plan_id = @plan_id
              and item.is_active = true
            order by item.sequence_number;
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
                RowVersion = reader.GetInt32(7),
                DefinitionKey = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                PlannedStartDate = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9),
                PlannedEndDate = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10),
                AssignedUserId = reader.IsDBNull(11) ? null : reader.GetGuid(11),
                AssignedUserName = reader.IsDBNull(12) ? null : reader.GetString(12),
                RequiredHeadcount = reader.IsDBNull(13) ? null : reader.GetInt32(13)
            });
        }
        return items;
    }

    private static async Task<IReadOnlyList<ProductionPlanSetScopeResponse>> ReadSetScopesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid planId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select scope.id,
                   scope.set_instance_id,
                   spec.name,
                   spec.spec_no,
                   instance.instance_number,
                   instance.status,
                   count(distinct panel.id) filter (where panel.status = 'Active')::integer,
                   count(distinct item.id) filter (where item.is_active and item.is_required)::integer,
                   count(distinct item.id) filter (
                       where item.is_active
                         and item.is_required
                         and value.planned_start_date is not null
                         and value.planned_end_date is not null
                   )::integer,
                   scope.row_version
            from project_production_plan_set_scopes scope
            join ul891_set_instances instance on instance.id = scope.set_instance_id
            join ul891_set_specs spec on spec.id = instance.spec_id
            left join panel_placeholders panel on panel.set_instance_id = instance.id
            left join project_production_plan_items item on item.production_plan_id = scope.production_plan_id
            left join project_production_plan_set_item_values value
              on value.set_scope_id = scope.id
             and value.production_plan_item_id = item.id
            where scope.production_plan_id = @plan_id
            group by scope.id, scope.set_instance_id, spec.name, spec.spec_no,
                     instance.instance_number, instance.status, scope.row_version
            order by
                case when instance.status = 'Active' then 0 else 1 end,
                spec.spec_no,
                instance.instance_number;
            """;
        command.Parameters.AddWithValue("plan_id", planId);
        var result = new List<ProductionPlanSetScopeResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var specName = reader.GetString(2);
            var instanceNumber = reader.GetInt32(4);
            result.Add(new ProductionPlanSetScopeResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                $"{specName} · {instanceNumber}번 세트",
                specName,
                reader.GetInt32(3),
                instanceNumber,
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9)));
        }
        return result;
    }

    private static async Task<IReadOnlyDictionary<Guid, SetItemValue>> ReadSetItemValuesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var rows = await ReadSetItemValuesAsync(connection, transaction, [scopeId], cancellationToken);
        return rows.ToDictionary(row => row.ItemId);
    }

    private static async Task<ProductionPlanSetDefaultResponse?> ReadSetDefaultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid planId,
        IReadOnlyList<ProductionPlanItemResponse> items,
        CancellationToken cancellationToken)
    {
        Guid defaultId;
        int rowVersion;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select id,row_version from project_production_plan_set_defaults where production_plan_id=@plan_id;";
            command.Parameters.AddWithValue("plan_id", planId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            defaultId = reader.GetGuid(0);
            rowVersion = reader.GetInt32(1);
        }

        var values = new Dictionary<Guid, SetItemValue>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select value.id, value.set_default_id, value.production_plan_item_id,
                       value.planned_start_date, value.planned_end_date,
                       value.assigned_user_id, assigned.display_name,
                       value.required_headcount, value.note, value.row_version
                from project_production_plan_set_default_values value
                left join qms_users assigned on assigned.id=value.assigned_user_id
                where value.set_default_id=@default_id;
                """;
            command.Parameters.AddWithValue("default_id", defaultId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = new SetItemValue(
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
                    reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                    reader.IsDBNull(5) ? null : reader.GetGuid(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.GetInt32(9));
                values[value.ItemId] = value;
            }
        }
        return new ProductionPlanSetDefaultResponse(defaultId, rowVersion, ApplySetItemValues(items, values));
    }

    private static async Task<IReadOnlyList<SetItemValue>> ReadSetItemValuesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid[] scopeIds,
        CancellationToken cancellationToken)
    {
        if (scopeIds.Length == 0)
        {
            return [];
        }
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select value.id,
                   value.set_scope_id,
                   value.production_plan_item_id,
                   value.planned_start_date,
                   value.planned_end_date,
                   value.assigned_user_id,
                   assigned.display_name,
                   value.required_headcount,
                   value.note,
                   value.row_version
            from project_production_plan_set_item_values value
            left join qms_users assigned on assigned.id = value.assigned_user_id
            where value.set_scope_id = any(@scope_ids);
            """;
        command.Parameters.AddWithValue("scope_ids", scopeIds);
        var result = new List<SetItemValue>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new SetItemValue(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                reader.IsDBNull(5) ? null : reader.GetGuid(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9)));
        }
        return result;
    }

    private static IReadOnlyList<ProductionPlanItemResponse> ApplySetItemValues(
        IReadOnlyList<ProductionPlanItemResponse> items,
        IReadOnlyDictionary<Guid, SetItemValue> values) =>
        items.Select(item =>
        {
            if (item.ItemId is null || !values.TryGetValue(item.ItemId.Value, out var value))
            {
                return CopyPlanItemWithSchedule(item, null, null, null, null, null, null, 0);
            }
            return CopyPlanItemWithSchedule(
                item,
                value.PlannedStartDate,
                value.PlannedEndDate,
                value.AssignedUserId,
                value.AssignedUserName,
                value.RequiredHeadcount,
                value.Note,
                value.RowVersion);
        }).ToList();

    private static IReadOnlyList<ProductionPlanItemResponse> AggregateSetItemValues(
        IReadOnlyList<ProductionPlanItemResponse> items,
        IReadOnlyList<ProductionPlanSetScopeResponse> scopes,
        IReadOnlyList<SetItemValue> values)
    {
        var activeScopeIds = scopes
            .Where(scope => string.Equals(scope.Status, "Active", StringComparison.Ordinal))
            .Select(scope => scope.ScopeId)
            .ToHashSet();
        var activeCount = activeScopeIds.Count;
        var valuesByItem = values
            .Where(value => activeScopeIds.Contains(value.ScopeId))
            .GroupBy(value => value.ItemId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return items.Select(item =>
        {
            if (item.ItemId is null || !valuesByItem.TryGetValue(item.ItemId.Value, out var itemValues) || activeCount == 0)
            {
                return CopyPlanItemWithSchedule(item, null, null, null, null, null, null, item.RowVersion);
            }
            var starts = itemValues.Where(value => value.PlannedStartDate is not null).Select(value => value.PlannedStartDate!.Value).ToList();
            var ends = itemValues.Where(value => value.PlannedEndDate is not null).Select(value => value.PlannedEndDate!.Value).ToList();
            var assigned = itemValues.Where(value => value.AssignedUserId is not null).ToList();
            var distinctAssignees = assigned.Select(value => value.AssignedUserId!.Value).Distinct().ToList();
            var sameAssignee = distinctAssignees.Count == 1 && assigned.Count == activeCount;
            var anyAssignee = assigned.Count > 0;
            var headcountValues = itemValues.Where(value => value.RequiredHeadcount is not null).Select(value => value.RequiredHeadcount!.Value).ToList();
            var note = itemValues.Any(value => !string.IsNullOrWhiteSpace(value.Note)) ? "세트별 코멘트 있음" : null;
            return CopyPlanItemWithSchedule(
                item,
                starts.Count == 0 ? null : starts.Min(),
                ends.Count == 0 ? null : ends.Max(),
                sameAssignee ? distinctAssignees[0] : null,
                sameAssignee ? assigned[0].AssignedUserName : anyAssignee ? "세트별 상이" : null,
                headcountValues.Count == 0 ? null : headcountValues.Sum(),
                note,
                item.RowVersion);
        }).ToList();
    }

    private static ProductionPlanItemResponse CopyPlanItemWithSchedule(
        ProductionPlanItemResponse item,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        Guid? assignedUserId,
        string? assignedUserName,
        int? requiredHeadcount,
        string? note,
        int rowVersion) => new()
    {
        ItemId = item.ItemId,
        TemplateStepId = item.TemplateStepId,
        SequenceNumber = item.SequenceNumber,
        StepName = item.StepName,
        IsRequired = item.IsRequired,
        IsCustom = item.IsCustom,
        DefinitionKey = item.DefinitionKey,
        PlannedDate = null,
        PlannedStartDate = plannedStartDate,
        PlannedEndDate = plannedEndDate,
        AssignedUserId = assignedUserId,
        AssignedUserName = assignedUserName,
        RequiredHeadcount = requiredHeadcount,
        Connections = item.Connections,
        Evidence = item.Evidence,
        Note = note,
        RowVersion = rowVersion
    };

    private static async Task<IReadOnlyList<ProductionPlanItemResponse>> EnrichLinkedPlanItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        ProjectSnapshot project,
        IReadOnlyList<ProductionPlanItemResponse> items,
        Guid? setInstanceId,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var connections = await ReadLinkedPlanConnectionsAsync(
            connection,
            transaction,
            items.Where(item => item.ItemId is not null).Select(item => item.ItemId!.Value).ToArray(),
            cancellationToken);
        var blockedTargets = await ReadOpenPendingTargetsAsync(connection, transaction, project.ProjectId, cancellationToken);
        var setPanelIds = setInstanceId is null
            ? null
            : await ReadSetPanelIdsAsync(connection, transaction, project.ProjectId, setInstanceId.Value, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Seoul").DateTime);
        var result = new List<ProductionPlanItemResponse>(items.Count);

        foreach (var item in items.OrderBy(item => item.SequenceNumber))
        {
            var itemConnections = item.ItemId is not null && connections.TryGetValue(item.ItemId.Value, out var saved)
                ? saved
                : [];
            var evidence = new List<ProductionPlanEvidenceResponse>();
            var notApplicable = itemConnections.Count > 0;

            foreach (var connectionItem in itemConnections)
            {
                if (connectionItem.SourceCode == ProductionControlSourceCodes.FatPassed && !project.FatRequired)
                {
                    continue;
                }

                notApplicable = false;
                var sourceEvidence = await ReadLinkedEvidenceAsync(
                    connection,
                    transaction,
                    project.ProjectId,
                    connectionItem,
                    blockedTargets,
                    cancellationToken);
                if (IsProjectCommonSource(connectionItem.SourceCode))
                {
                    evidence.AddRange(sourceEvidence.Select(row => row with { EvidenceScope = "ProjectCommon" }));
                }
                else
                {
                    evidence.AddRange(setPanelIds is null
                        ? sourceEvidence
                        : sourceEvidence.Where(row => row.TargetType == "Panel" && Guid.TryParse(row.TargetId, out var panelId) && setPanelIds.Contains(panelId)));
                }
            }

            var uniqueEvidence = evidence
                .GroupBy(row => $"{row.SourceCode}:{row.TargetType}:{row.TargetId}", StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(row => row.CompletedDate).First())
                .ToList();
            var total = uniqueEvidence.Count;
            var completed = uniqueEvidence.Count(row => row.IsCompleted);
            var progress = total == 0 ? 0 : (int)Math.Round(completed * 100m / total, MidpointRounding.AwayFromZero);
            var actualStart = uniqueEvidence.Where(row => row.StartedDate is not null).Select(row => row.StartedDate!.Value).DefaultIfEmpty().Min();
            var actualEnd = total > 0 && completed == total
                ? uniqueEvidence.Where(row => row.CompletedDate is not null).Select(row => row.CompletedDate!.Value).DefaultIfEmpty().Max()
                : (DateOnly?)null;
            DateOnly? normalizedActualStart = uniqueEvidence.Any(row => row.StartedDate is not null) ? actualStart : null;
            DateOnly? normalizedActualEnd = actualEnd == default ? null : actualEnd;
            var blocked = uniqueEvidence.Any(row => row.IsBlocked);
            var (scheduleStatus, scheduleLabel, delayDays) = CalculateLinkedSchedule(
                itemConnections.Count,
                notApplicable,
                blocked,
                completed,
                total,
                item.PlannedStartDate,
                item.PlannedEndDate,
                normalizedActualStart,
                normalizedActualEnd,
                today);

            result.Add(CopyLinkedPlanItem(
                item,
                itemConnections,
                uniqueEvidence,
                normalizedActualStart,
                normalizedActualEnd,
                completed,
                total,
                progress,
                scheduleStatus,
                scheduleLabel,
                delayDays,
                blocked));
        }

        return result;
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<ProductionControlConnectionResponse>>> ReadLinkedPlanConnectionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid[] itemIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, IReadOnlyList<ProductionControlConnectionResponse>>();
        if (itemIds.Length == 0)
        {
            return result;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select production_plan_item_id, source_code, source_definition_key
            from project_production_plan_connections
            where production_plan_item_id = any(@item_ids)
            order by source_code, source_definition_key;
            """;
        command.Parameters.AddWithValue("item_ids", itemIds);
        var mutable = new Dictionary<Guid, List<ProductionControlConnectionResponse>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemId = reader.GetGuid(0);
            if (!mutable.TryGetValue(itemId, out var rows))
            {
                rows = [];
                mutable[itemId] = rows;
            }
            rows.Add(new ProductionControlConnectionResponse(
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetGuid(2)));
        }

        foreach (var pair in mutable)
        {
            result[pair.Key] = pair.Value;
        }
        return result;
    }

    private static async Task<HashSet<string>> ReadOpenPendingTargetsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select issue.target_type, issue.target_id
            from pending_issues issue
            where issue.project_id = @project_id
              and issue.status <> 'Closed'
            union
            select 'Panel', attempt.panel_id
            from pending_issues issue
            join panel_quality_inspection_attempts attempt on attempt.id = issue.target_id
            where issue.project_id = @project_id
              and issue.target_type = 'Inspection'
              and issue.status <> 'Closed'
            union
            select 'ProcurementItem', receipt.procurement_item_id
            from pending_issues issue
            join material_iqc_attempts attempt on attempt.pending_issue_id = issue.id
            join material_receipts receipt on receipt.id = attempt.material_receipt_id
            where issue.project_id = @project_id
              and issue.status <> 'Closed';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        var result = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add($"{reader.GetString(0)}:{reader.GetGuid(1)}");
        }
        return result;
    }

    private static bool IsProjectCommonSource(string sourceCode) =>
        sourceCode is ProductionControlSourceCodes.PurchaseOrdered
            or ProductionControlSourceCodes.MaterialReceiptConfirmed
            or ProductionControlSourceCodes.IqcPassed;

    private static async Task<HashSet<Guid>> ReadSetPanelIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        Guid setInstanceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select panel.id
            from panel_placeholders panel
            join ul891_set_instances instance on instance.id = panel.set_instance_id
            join ul891_set_specs spec on spec.id = instance.spec_id
            where panel.project_id = @project_id
              and spec.project_id = @project_id
              and panel.set_instance_id = @set_instance_id
              and panel.status = 'Active';
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("set_instance_id", setInstanceId);
        var result = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetGuid(0));
        }
        return result;
    }

    private static async Task<IReadOnlyList<ProductionPlanEvidenceResponse>> ReadLinkedEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid projectId,
        ProductionControlConnectionResponse source,
        IReadOnlySet<string> blockedTargets,
        CancellationToken cancellationToken)
    {
        var sourceCatalog = ProductionControlSourceCodes.Catalog.First(item => item.Code == source.SourceCode);
        var sql = source.SourceCode switch
        {
            ProductionControlSourceCodes.PurchaseOrdered => """
                select 'ProcurementItem', item.id, coalesce(nullif(item.order_item, ''), '구매품목 ' || item.sequence_number),
                       item.created_at_utc at time zone 'Asia/Seoul',
                       case when item.order_date is not null then item.order_date::timestamp end,
                       item.order_date is not null
                from project_procurement_items item
                where item.project_id = @project_id and item.status = 'Active'
                order by item.sequence_number
                """,
            ProductionControlSourceCodes.MaterialReceiptConfirmed => """
                select 'ProcurementItem', item.id, coalesce(nullif(item.order_item, ''), '구매품목 ' || item.sequence_number),
                       min(receipt.arrival_date)::timestamp,
                       case when item.receipt_completed then
                           coalesce(item.receipt_completed_at_utc at time zone 'Asia/Seoul', max(receipt.confirmed_at_utc at time zone 'Asia/Seoul'))
                       end,
                       item.receipt_completed
                from project_procurement_items item
                left join material_receipts receipt on receipt.procurement_item_id = item.id and receipt.status <> 'Cancelled'
                where item.project_id = @project_id and item.status = 'Active'
                group by item.id
                order by item.sequence_number
                """,
            ProductionControlSourceCodes.IqcPassed => IqcEvidenceSql(source.SourceDefinitionKey is not null),
            ProductionControlSourceCodes.ManufacturingStepCompleted => """
                select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
                       execution.started_at_utc at time zone 'Asia/Seoul',
                       step.checked_at_utc at time zone 'Asia/Seoul',
                       step.checked_at_utc is not null
                from panel_placeholders panel
                left join lateral (
                    select candidate.id, candidate.started_at_utc
                    from panel_manufacturing_executions candidate
                    where candidate.panel_id = panel.id and candidate.status <> 'Cancelled'
                    order by candidate.started_at_utc desc
                    limit 1
                ) execution on true
                left join panel_manufacturing_execution_steps step
                  on step.execution_id = execution.id and step.definition_key = @source_definition_key
                where panel.project_id = @project_id and panel.status = 'Active'
                order by panel.sequence_number
                """,
            ProductionControlSourceCodes.LqcPassed => """
                select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
                       coalesce(lqc.started_at_utc, execution.started_at_utc) at time zone 'Asia/Seoul',
                       case when lqc.passed and step.checked_at_utc is not null
                            then lqc.completed_at_utc at time zone 'Asia/Seoul' end,
                       lqc.passed and step.checked_at_utc is not null
                from panel_placeholders panel
                left join lateral (
                    select candidate.id, candidate.started_at_utc
                    from panel_manufacturing_executions candidate
                    where candidate.panel_id = panel.id and candidate.status <> 'Cancelled'
                    order by candidate.started_at_utc desc
                    limit 1
                ) execution on true
                left join panel_manufacturing_execution_steps step
                  on step.execution_id = execution.id and step.definition_key = @source_definition_key
                left join lateral (
                    select min(attempt.started_at_utc) as started_at_utc,
                           max(report.finalized_at_utc) filter (
                               where report.result = 'Passed' and response.check_result = 'Pass'
                           ) as completed_at_utc,
                           coalesce(bool_or(
                               report.result = 'Passed' and response.check_result = 'Pass'
                           ), false) as passed
                    from panel_quality_inspection_attempts attempt
                    join panel_quality_reports report on report.attempt_id = attempt.id
                    join panel_quality_report_responses response
                      on response.report_id = report.id
                     and response.manufacturing_definition_key = @source_definition_key
                    where attempt.panel_id = panel.id
                      and attempt.stage_code = 'LQC'
                      and attempt.status <> 'Cancelled'
                ) lqc on true
                where panel.project_id = @project_id and panel.status = 'Active'
                order by panel.sequence_number
                """,
            ProductionControlSourceCodes.OqcPassed => QualityEvidenceSql("OQC", source.SourceDefinitionKey is not null),
            ProductionControlSourceCodes.CustomerInspectionPassed => QualityEvidenceSql("CustomerInspection", false),
            ProductionControlSourceCodes.FatPassed => QualityEvidenceSql("FAT", false),
            ProductionControlSourceCodes.Packed => """
                select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
                       unit.created_at_utc at time zone 'Asia/Seoul',
                       case when unit.status = 'Finalized' then unit.finalized_at_utc at time zone 'Asia/Seoul' end,
                       unit.status = 'Finalized'
                from panel_placeholders panel
                left join logistics_packing_unit_panels membership on membership.panel_id = panel.id and membership.active
                left join logistics_packing_units unit on unit.id = membership.packing_unit_id and unit.status <> 'Cancelled'
                where panel.project_id = @project_id and panel.status = 'Active'
                order by panel.sequence_number
                """,
            ProductionControlSourceCodes.Departed => LogisticsBatchEvidenceSql("DepartureProcessed"),
            ProductionControlSourceCodes.Delivered => """
                select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
                       departure.finalized_at_utc at time zone 'Asia/Seoul',
                       delivery.delivered_at_utc at time zone 'Asia/Seoul',
                       delivery.delivered_at_utc is not null
                from panel_placeholders panel
                left join logistics_delivery_results delivery on delivery.panel_id = panel.id
                left join logistics_batches departure on departure.id = delivery.batch_id
                where panel.project_id = @project_id and panel.status = 'Active'
                order by panel.sequence_number
                """,
            _ => throw new InvalidOperationException($"지원하지 않는 생산계획 연결 소스입니다: {source.SourceCode}")
        };

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("project_id", projectId);
        if (source.SourceDefinitionKey is not null)
        {
            command.Parameters.AddWithValue("source_definition_key", source.SourceDefinitionKey.Value);
        }

        var rows = new List<ProductionPlanEvidenceResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var targetType = reader.GetString(0);
            var targetId = reader.GetGuid(1);
            var completed = !reader.IsDBNull(5) && reader.GetBoolean(5);
            var blocked = blockedTargets.Contains($"{targetType}:{targetId}")
                || blockedTargets.Contains($"Project:{projectId}");
            rows.Add(new ProductionPlanEvidenceResponse(
                source.SourceCode,
                SourceLabel(sourceCatalog, source.SourceDefinitionKey),
                targetType,
                targetId.ToString(),
                reader.GetString(2),
                ReadEvidenceDate(reader, 3),
                ReadEvidenceDate(reader, 4),
                completed,
                blocked,
                blocked ? "Pending" : completed ? "완료" : "대기"));
        }
        return rows;
    }

    private static string IqcEvidenceSql(bool itemSpecific) => itemSpecific
        ? """
            select 'MaterialReceipt', receipt.id,
                   coalesce(nullif(item.order_item, ''), '구매품목 ' || item.sequence_number) || ' · ' || receipt.arrival_date,
                   receipt.arrival_date::timestamp,
                   case when latest.status = 'Passed'
                             and response.check_result in ('Pass','NotApplicable')
                        then latest.decided_at_utc at time zone 'Asia/Seoul' end,
                   coalesce(latest.status = 'Passed'
                            and response.check_result in ('Pass','NotApplicable'), false)
            from project_procurement_items item
            join material_receipts receipt
              on receipt.procurement_item_id = item.id
             and receipt.status <> 'Cancelled'
            left join lateral (
                select attempt.status, attempt.decided_at_utc, report.id as report_id
                from material_iqc_attempts attempt
                left join iqc_reports report on report.attempt_id=attempt.id
                where attempt.material_receipt_id = receipt.id
                order by attempt.attempt_number desc
                limit 1
            ) latest on true
            left join iqc_report_responses response
              on response.report_id=latest.report_id
             and exists (
                 select 1
                 from iqc_report_template_items template_item
                 where template_item.id=response.template_item_id
                   and template_item.definition_key=@source_definition_key
             )
            where item.project_id = @project_id and item.status = 'Active'
            order by item.sequence_number, receipt.arrival_date, receipt.id
            """
        : """
            select 'MaterialReceipt', receipt.id,
                   coalesce(nullif(item.order_item, ''), '구매품목 ' || item.sequence_number) || ' · ' || receipt.arrival_date,
                   receipt.arrival_date::timestamp,
                   case when latest.status = 'Passed' then latest.decided_at_utc at time zone 'Asia/Seoul' end,
                   coalesce(latest.status = 'Passed', false)
            from project_procurement_items item
            join material_receipts receipt on receipt.procurement_item_id = item.id and receipt.status <> 'Cancelled'
            left join lateral (
                select attempt.status, attempt.decided_at_utc
                from material_iqc_attempts attempt
                where attempt.material_receipt_id = receipt.id
                order by attempt.attempt_number desc
                limit 1
            ) latest on true
            where item.project_id = @project_id and item.status = 'Active'
            order by item.sequence_number, receipt.arrival_date, receipt.id
            """;

    private static string QualityEvidenceSql(string stageCode, bool itemSpecific) => itemSpecific
        ? $"""
            select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
                   attempt.started_at_utc at time zone 'Asia/Seoul',
                   case when attempt.status = 'Passed'
                             and response.check_result in ('Pass','NotApplicable')
                        then attempt.completed_at_utc at time zone 'Asia/Seoul' end,
                   coalesce(attempt.status = 'Passed'
                            and response.check_result in ('Pass','NotApplicable'), false)
            from panel_placeholders panel
            left join lateral (
                select candidate.status, candidate.started_at_utc, candidate.completed_at_utc,
                       report.id as report_id
                from panel_quality_inspection_attempts candidate
                left join panel_quality_reports report on report.attempt_id=candidate.id
                where candidate.panel_id = panel.id
                  and candidate.stage_code = '{stageCode}'
                  and candidate.status <> 'Cancelled'
                order by candidate.attempt_number desc
                limit 1
            ) attempt on true
            left join panel_quality_report_responses response
              on response.report_id=attempt.report_id
             and exists (
                 select 1
                 from panel_quality_template_items template_item
                 where template_item.id=response.template_item_id
                   and template_item.definition_key=@source_definition_key
             )
            where panel.project_id = @project_id and panel.status = 'Active'
            order by panel.sequence_number
            """
        : $"""
        select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
               attempt.started_at_utc at time zone 'Asia/Seoul',
               case when attempt.status = 'Passed' then attempt.completed_at_utc at time zone 'Asia/Seoul' end,
               coalesce(attempt.status = 'Passed', false)
        from panel_placeholders panel
        left join lateral (
            select candidate.status, candidate.started_at_utc, candidate.completed_at_utc
            from panel_quality_inspection_attempts candidate
            where candidate.panel_id = panel.id and candidate.stage_code = '{stageCode}' and candidate.status <> 'Cancelled'
            order by candidate.attempt_number desc
            limit 1
        ) attempt on true
        where panel.project_id = @project_id and panel.status = 'Active'
        order by panel.sequence_number
        """;

    private static string SourceLabel(
        ProductionControlSourceCatalogItemResponse source,
        Guid? definitionKey)
    {
        if (definitionKey is null) return source.Label;
        var definition = source.Definitions.FirstOrDefault(item => item.DefinitionKey == definitionKey);
        return definition is null ? source.Label : $"{source.Label} · {definition.Label}";
    }

    private static string LogisticsBatchEvidenceSql(string stageCode) => $"""
        select 'Panel', panel.id, coalesce(panel.panel_name, panel.display_code),
               unit.finalized_at_utc at time zone 'Asia/Seoul',
               case when batch.status = 'Finalized' then batch.finalized_at_utc at time zone 'Asia/Seoul' end,
               batch.status = 'Finalized'
        from panel_placeholders panel
        left join logistics_batch_panels membership
          on membership.panel_id = panel.id and membership.active and membership.stage_code = '{stageCode}'
        left join logistics_batches batch
          on batch.id = membership.batch_id and batch.stage_code = '{stageCode}' and batch.status <> 'Cancelled'
        left join logistics_packing_units unit on unit.id = membership.packing_unit_id
        where panel.project_id = @project_id and panel.status = 'Active'
        order by panel.sequence_number
        """;

    private static DateOnly? ReadEvidenceDate(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.DateTime),
            _ => DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture)
        };
    }

    private static (string Status, string Label, int? DelayDays) CalculateLinkedSchedule(
        int connectionCount,
        bool notApplicable,
        bool blocked,
        int completed,
        int total,
        DateOnly? plannedStart,
        DateOnly? plannedEnd,
        DateOnly? actualStart,
        DateOnly? actualEnd,
        DateOnly today)
    {
        if (connectionCount == 0)
        {
            return ("NotConnected", "연결 안 됨", null);
        }
        if (notApplicable)
        {
            return ("NotApplicable", "해당 없음", null);
        }
        if (blocked)
        {
            return ("Blocked", "Pending", plannedEnd is not null && today > plannedEnd ? today.DayNumber - plannedEnd.Value.DayNumber : null);
        }
        if (total > 0 && completed == total)
        {
            var delay = plannedEnd is not null && actualEnd is not null && actualEnd > plannedEnd
                ? actualEnd.Value.DayNumber - plannedEnd.Value.DayNumber
                : 0;
            return delay > 0 ? ("CompletedLate", "지연 완료", delay) : ("Completed", "완료", 0);
        }
        if (actualStart is not null || completed > 0)
        {
            var delay = plannedEnd is not null && today > plannedEnd ? today.DayNumber - plannedEnd.Value.DayNumber : 0;
            return delay > 0 ? ("Delayed", "지연", delay) : ("InProgress", "진행 중", 0);
        }
        if (plannedStart is not null && today > plannedStart)
        {
            return ("Delayed", "착수 지연", today.DayNumber - plannedStart.Value.DayNumber);
        }
        return ("NotStarted", "대기", null);
    }

    private static ProductionPlanItemResponse CopyLinkedPlanItem(
        ProductionPlanItemResponse item,
        IReadOnlyList<ProductionControlConnectionResponse> connections,
        IReadOnlyList<ProductionPlanEvidenceResponse> evidence,
        DateOnly? actualStart,
        DateOnly? actualEnd,
        int completed,
        int total,
        int progress,
        string scheduleStatus,
        string scheduleLabel,
        int? delayDays,
        bool isBlocked)
        => new()
        {
            ItemId = item.ItemId,
            TemplateStepId = item.TemplateStepId,
            SequenceNumber = item.SequenceNumber,
            StepName = item.StepName,
            IsRequired = item.IsRequired,
            IsCustom = item.IsCustom,
            DefinitionKey = item.DefinitionKey,
            PlannedDate = item.PlannedDate,
            PlannedStartDate = item.PlannedStartDate,
            PlannedEndDate = item.PlannedEndDate,
            ActualStartDate = actualStart,
            ActualEndDate = actualEnd,
            AssignedUserId = item.AssignedUserId,
            AssignedUserName = item.AssignedUserName,
            RequiredHeadcount = item.RequiredHeadcount,
            CompletedTargetCount = completed,
            TotalTargetCount = total,
            ProgressPercent = progress,
            ScheduleStatus = scheduleStatus,
            ScheduleStatusLabel = scheduleLabel,
            DelayDays = delayDays,
            IsBlocked = isBlocked,
            Connections = connections,
            Evidence = evidence,
            Note = item.Note,
            RowVersion = item.RowVersion
        };

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
        command.Parameters.AddWithValue("code", NormalizeItemCode(productTypeCode));
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
            where pt.code = any(@canonical_codes)
            order by case pt.code
                when 'UL67' then 1
                when 'UL891' then 2
                when 'UL508A' then 3
                when 'IEC' then 4
                when 'LLP' then 5
                when 'RPP' then 6
                else 100
            end, pt.code;
            """;
        command.Parameters.AddWithValue("canonical_codes", ProductionPlanningDomain.CanonicalProductTypeCodes.ToArray());
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
              and pt.code = any(@canonical_codes)
            order by case pt.code
                when 'UL67' then 1
                when 'UL891' then 2
                when 'UL508A' then 3
                when 'IEC' then 4
                when 'LLP' then 5
                when 'RPP' then 6
                else 100
            end, pt.code;
            """;
        command.Parameters.AddWithValue("canonical_codes", ProductionPlanningDomain.CanonicalProductTypeCodes.ToArray());
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
            var assigned = FindAssigneeForResponsibility(assignees, responsibility);
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

    private static ProjectAssigneeResponse? FindAssigneeForResponsibility(
        IReadOnlyList<ProjectAssigneeResponse> assignees,
        string responsibility)
    {
        var direct = assignees.FirstOrDefault(item => item.ResponsibilityType == responsibility);
        if (direct is not null)
        {
            return direct;
        }

        var legacy = ProductionPlanningDomain.LegacyResponsibilityAlias(responsibility);
        if (legacy is null)
        {
            return null;
        }

        var legacyAssignee = assignees.FirstOrDefault(item => item.ResponsibilityType == legacy && item.AssignedUserId is not null);
        return legacyAssignee is null
            ? null
            : new ProjectAssigneeResponse
            {
                ResponsibilityType = responsibility,
                ResponsibilityLabel = ProductionPlanningDomain.ResponsibilityLabel(responsibility),
                AssignedUserId = legacyAssignee.AssignedUserId,
                AssignedUserName = legacyAssignee.AssignedUserName,
                Note = legacyAssignee.Note
            };
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

    private static string? FormatPeriod(DateOnly? start, DateOnly? end)
    {
        if (start is null && end is null)
        {
            return null;
        }
        return $"{FormatDate(start) ?? "-"} ~ {FormatDate(end) ?? "-"}";
    }

    private static string Normalize(string? value)
    {
        return string.Join(' ', (value ?? "").Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ItemCodesEqual(string? left, string? right)
    {
        return string.Equals(NormalizeItemCode(left), NormalizeItemCode(right), StringComparison.Ordinal);
    }

    private static string NormalizeItemCode(string? value)
    {
        var normalized = Normalize(value);
        return normalized == "RRP" ? "RPP" : normalized;
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

        return (string.Equals(expected, "Item", StringComparison.OrdinalIgnoreCase)
                && string.Equals(actual, "제품 구분", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(expected, "생산단계", StringComparison.OrdinalIgnoreCase)
                && string.Equals(actual, "계획 항목", StringComparison.OrdinalIgnoreCase))
            || (string.Equals(expected, "필수 여부", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(actual, "필수", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actual, "필수여부", StringComparison.OrdinalIgnoreCase)));
    }

    private static readonly string[] BulkExcelHeaders =
    [
        "프로젝트명",
        "PJT Code",
        "Item",
        "생산단계",
        "필수 여부",
        "예정일",
        "비고"
    ];

    private static readonly string[] ProjectExcelHeaders =
    [
        "생산단계",
        "필수 여부",
        "예정일",
        "비고"
    ];

    private static readonly string[] LegacyBulkAssigneeHeaders =
    [
        "구매 담당자",
        "생산관리 담당자",
        "제조 담당자",
        "품질 담당자",
        "물류 담당자"
    ];

    private static Dictionary<string, int> BuildBulkExcelHeaderMap(IXLWorksheet worksheet, int headerRow)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? BulkExcelHeaders.Length;
        var expectedHeaders = BulkExcelHeaders.Concat(LegacyBulkAssigneeHeaders).ToArray();
        for (var column = 1; column <= lastColumn; column++)
        {
            var actual = NormalizeExcelHeader(worksheet.Cell(headerRow, column).GetString());
            if (string.IsNullOrWhiteSpace(actual))
            {
                continue;
            }

            var canonical = expectedHeaders.FirstOrDefault(expected => HeaderMatches(actual, expected));
            if (canonical is null)
            {
                continue;
            }

            if (result.ContainsKey(canonical))
            {
                throw new InvalidDataException($"생산계획 Excel 양식에 중복 Header가 있습니다: {canonical}");
            }

            result[canonical] = column;
        }

        var missing = BulkExcelHeaders.Where(header => !result.ContainsKey(header)).ToList();
        if (missing.Count > 0)
        {
            var legacyOrderCompatible = BulkExcelHeaders
                .Where(header => header != "필수 여부")
                .Select((header, index) => HeaderMatches(NormalizeExcelHeader(worksheet.Cell(headerRow, index + 1).GetString()), header))
                .All(matches => matches);
            if (legacyOrderCompatible)
            {
                result["필수 여부"] = 0;
                return result;
            }

            throw new InvalidDataException($"생산계획 Excel 양식의 Header를 확인해 주세요. 누락: {string.Join(", ", missing)}");
        }

        return result;
    }

    private static Dictionary<string, int> BuildProjectExcelHeaderMap(IXLWorksheet worksheet, int headerRow)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? ProjectExcelHeaders.Length;
        for (var column = 1; column <= lastColumn; column++)
        {
            var actual = NormalizeExcelHeader(worksheet.Cell(headerRow, column).GetString());
            if (string.IsNullOrWhiteSpace(actual))
            {
                continue;
            }

            var canonical = ProjectExcelHeaders.FirstOrDefault(expected => HeaderMatches(actual, expected));
            if (canonical is null)
            {
                continue;
            }

            if (result.ContainsKey(canonical))
            {
                throw new InvalidDataException($"생산계획 Excel 양식에 중복 Header가 있습니다: {canonical}");
            }

            result[canonical] = column;
        }

        var missing = ProjectExcelHeaders.Where(header => !result.ContainsKey(header)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException($"생산계획 Excel 양식의 Header를 확인해 주세요. 누락: {string.Join(", ", missing)}");
        }

        return result;
    }

    private static string? ReadOptionalExcelText(IXLWorksheet worksheet, int rowNumber, IReadOnlyDictionary<string, int> headerMap, string header)
    {
        return headerMap.TryGetValue(header, out var column) && column > 0
            ? CellText(worksheet.Cell(rowNumber, column))
            : null;
    }

    private static bool? ParseRequiredFlag(string? value, ICollection<string> errors)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            "예" or "Y" or "YES" or "TRUE" or "1" or "필수" => true,
            "아니오" or "N" or "NO" or "FALSE" or "0" or "선택" => false,
            _ => AddRequiredFlagError(errors)
        };
    }

    private static bool? AddRequiredFlagError(ICollection<string> errors)
    {
        errors.Add("필수 여부는 예 또는 아니오로 입력해 주세요.");
        return null;
    }

    private async Task<IReadOnlyList<ProductionPlanningExcelRow>> ParseBulkExcelRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        byte[] bytes,
        ProjectSnapshot? projectContext,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        if (workbook.Worksheets.Count != 1)
        {
            throw new InvalidDataException("생산계획 Excel은 하나의 시트만 포함해야 합니다.");
        }

        var worksheet = workbook.Worksheet(1);
        var projectTemplateHeaderRow = 3;
        var isProjectTemplate = projectContext is not null
            && HeaderMatches(NormalizeExcelHeader(worksheet.Cell(projectTemplateHeaderRow, 1).GetString()), "생산단계");
        var headerRow = isProjectTemplate ? projectTemplateHeaderRow : 1;
        var headerMap = isProjectTemplate
            ? BuildProjectExcelHeaderMap(worksheet, headerRow)
            : BuildBulkExcelHeaderMap(worksheet, headerRow);

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
        if (lastRow - headerRow > 500)
        {
            throw new InvalidDataException("생산계획 Excel은 최대 500행까지 업로드할 수 있습니다.");
        }

        var productTypes = await ReadProductTypesAsync(connection, transaction, cancellationToken);
        var rows = new List<ProductionPlanningExcelRow>();
        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var projectTitle = isProjectTemplate ? projectContext!.ProjectTitle : CellText(worksheet.Cell(rowNumber, headerMap["프로젝트명"]));
            var projectCode = isProjectTemplate ? projectContext!.ProjectCode : CellText(worksheet.Cell(rowNumber, headerMap["PJT Code"]));
            var productTypeCode = isProjectTemplate ? NormalizeItemCode(projectContext!.Item) : NormalizeItemCode(CellText(worksheet.Cell(rowNumber, headerMap["Item"])));
            var stepName = CellText(worksheet.Cell(rowNumber, headerMap["생산단계"]));
            var isRequiredText = ReadOptionalExcelText(worksheet, rowNumber, headerMap, "필수 여부");
            var plannedDateText = CellText(worksheet.Cell(rowNumber, headerMap["예정일"]));
            var note = CellText(worksheet.Cell(rowNumber, headerMap["비고"]));
            var procurement = isProjectTemplate ? null : ReadOptionalExcelText(worksheet, rowNumber, headerMap, "구매 담당자");
            var production = isProjectTemplate ? null : ReadOptionalExcelText(worksheet, rowNumber, headerMap, "생산관리 담당자");
            var manufacturing = isProjectTemplate ? null : ReadOptionalExcelText(worksheet, rowNumber, headerMap, "제조 담당자");
            var quality = isProjectTemplate ? null : ReadOptionalExcelText(worksheet, rowNumber, headerMap, "품질 담당자");
            var logistics = isProjectTemplate ? null : ReadOptionalExcelText(worksheet, rowNumber, headerMap, "물류 담당자");

            if (new[] { projectTitle, projectCode, productTypeCode, stepName, isRequiredText, plannedDateText, note, procurement, production, manufacturing, quality, logistics }.All(string.IsNullOrWhiteSpace))
            {
                rows.Add(new ProductionPlanningExcelRow(rowNumber, "Skipped", null, projectTitle, projectCode, null, productTypeCode, null, stepName, false, null, null, note, procurement, production, manufacturing, quality, logistics, ["빈 행입니다."]));
                continue;
            }

            var errors = new List<string>();
            var isRequired = ParseRequiredFlag(isRequiredText, errors);
            var project = isProjectTemplate
                ? projectContext
                : await MatchProjectForExcelAsync(connection, transaction, projectCode, projectTitle, cancellationToken);
            if (project is null)
            {
                errors.Add("등록되지 않은 프로젝트입니다.");
            }
            else if (projectContext is not null && project.ProjectId != projectContext.ProjectId)
            {
                errors.Add("현재 프로젝트의 생산계획 Excel만 업로드할 수 있습니다.");
            }

            var productType = productTypes.FirstOrDefault(item => string.Equals(item.Code, productTypeCode, StringComparison.OrdinalIgnoreCase));
            if (productType is null)
            {
                errors.Add("Item은 UL67, UL891, UL508A, IEC, LLP, RPP 중 하나여야 합니다.");
            }

            if (project is not null
                && !string.IsNullOrWhiteSpace(productTypeCode)
                && !ItemCodesEqual(project.Item, productTypeCode))
            {
                errors.Add($"Excel의 Item이 프로젝트 Item과 일치하지 않습니다. 프로젝트 Item: {project.Item}, Excel Item: {productTypeCode}");
            }

            if (string.IsNullOrWhiteSpace(stepName))
            {
                errors.Add("생산단계는 필수입니다.");
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
                ("구매 담당자", procurement, "ProcurementPrimary"),
                ("생산관리 담당자", production, "ProductionPlanningPrimary"),
                ("제조 담당자", manufacturing, "ManufacturingPrimary"),
                ("품질 담당자", quality, "QualityIQC"),
                ("물류 담당자", logistics, "LogisticsPrimary")
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
                if (project.StructureMode == "Ul891Set"
                    && existingPlan?.ModelVersion == ProductionControlModelVersions.LinkedV1)
                {
                    errors.Add("UL891 세트형 프로젝트의 계획 일정은 세트 일정 화면에서 수정해 주세요.");
                }
                else if (existingPlan is not null)
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
            var effectiveIsRequired = isRequired ?? templateStep?.IsRequired ?? false;
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
                effectiveIsRequired,
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
            IsRequired = row.IsRequired,
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
            select id, coalesce(project_title, name, ''), coalesce(project_code, project_number, ''), coalesce(item, ''), delivery_date, status, sales_owner_user_id, fat_required, structure_mode
            from projects
            where deleted_at_utc is null
              and upper(btrim(coalesce(project_code, project_number, ''))) = upper(btrim(@project_code));
            """;
        command.Parameters.AddWithValue("project_code", projectCode);
        var rows = new List<ProjectSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return rows;
    }

    private static async Task<ProjectSnapshot?> FindProjectByNormalizedTitleAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string projectTitle, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id, coalesce(project_title, name, ''), coalesce(project_code, project_number, ''), coalesce(item, ''), delivery_date, status, sales_owner_user_id, fat_required, structure_mode
            from projects
            where deleted_at_utc is null
              and upper(btrim(coalesce(project_title, name, ''))) = upper(btrim(@project_title))
            limit 1;
            """;
        command.Parameters.AddWithValue("project_title", projectTitle);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ProjectSnapshot(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetBoolean(7), reader.IsDBNull(8) ? null : reader.GetString(8))
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
            ("ProcurementPrimary", row.ProcurementAssigneeText),
            ("ProductionPlanningPrimary", row.ProductionPlanningAssigneeText),
            ("ManufacturingPrimary", row.ManufacturingAssigneeText),
            ("QualityIQC", row.QualityAssigneeText),
            ("LogisticsPrimary", row.LogisticsAssigneeText)
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

    private sealed record ProjectSnapshot(
        Guid ProjectId,
        string ProjectTitle,
        string ProjectCode,
        string Item,
        DateOnly? DeliveryDate,
        string Status,
        Guid? SalesOwnerUserId,
        bool FatRequired,
        string? StructureMode = null);
    private sealed record PlanHeader(
        Guid PlanId,
        Guid? ProductTypeId,
        Guid? TemplateId,
        string? ProductTypeCode,
        string? ProductTypeName,
        string? Notes,
        int RowVersion,
        string ModelVersion,
        Guid? LinkedPlanTemplateVersionId,
        Guid? LinkedManufacturingTemplateVersionId);
    private sealed record ProductTypeSnapshot(Guid ProductTypeId, string ProductTypeCode, string ProductTypeName, Guid TemplateId);
    private sealed record SetItemValue(
        Guid ValueId,
        Guid ScopeId,
        Guid ItemId,
        DateOnly? PlannedStartDate,
        DateOnly? PlannedEndDate,
        Guid? AssignedUserId,
        string? AssignedUserName,
        int? RequiredHeadcount,
        string? Note,
        int RowVersion);
    private sealed record SetScopeSnapshot(
        Guid ScopeId,
        int RowVersion,
        string Status,
        string SpecName,
        int SpecNumber,
        int InstanceNumber);
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
        bool? IsRequired,
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
