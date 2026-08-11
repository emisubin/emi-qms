using Npgsql;

namespace Emi.Qms.Api.Workflow;

internal static class WorkItemDueDateSynchronizer
{
    public static async Task SyncProcurementProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items work_item
            set due_date = item.expected_receipt_date
            from project_procurement_items item
            where work_item.project_id = @project_id
              and work_item.target_type = 'ProcurementItem'
              and work_item.target_id = item.id
              and item.project_id = @project_id
              and item.status = 'Active'
              and work_item.status in ('Requested', 'InProgress')
              and work_item.due_date is distinct from item.expected_receipt_date;

            update work_items work_item
            set due_date = aggregate_due.due_date
            from (
                select min(item.expected_receipt_date) as due_date
                from project_procurement_items item
                where item.project_id = @project_id
                  and item.status = 'Active'
                  and item.receipt_completed = false
                  and item.expected_receipt_date is not null
            ) aggregate_due
            where work_item.project_id = @project_id
              and work_item.target_type = 'Project'
              and work_item.workflow_stage_code = 'MaterialArrived'
              and work_item.status in ('Requested', 'InProgress')
              and work_item.due_date is distinct from aggregate_due.due_date;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task SyncProductionProjectAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            update work_items work_item
            set due_date = coalesce(
                (
                    select min(scope_value.planned_end_date)
                    from project_production_plan_set_item_values scope_value
                    join project_production_plan_set_scopes scope on scope.id = scope_value.set_scope_id
                    where scope.production_plan_id = plan_item.production_plan_id
                      and scope_value.production_plan_item_id = plan_item.id
                      and scope_value.planned_end_date is not null
                ),
                plan_item.planned_end_date)
            from project_production_plan_items plan_item
            join project_production_plans plan on plan.id = plan_item.production_plan_id
            where plan.project_id = @project_id
              and plan_item.is_active = true
              and work_item.project_id = @project_id
              and work_item.target_type = 'ProductionPlan'
              and work_item.target_id = plan_item.id
              and work_item.status in ('Requested', 'InProgress');
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
