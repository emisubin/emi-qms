using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed record UpdateOsanProjectRequest(CreateOsanProjectRequest Fields, string ExpectedToken);
public sealed record DeleteOsanProjectRequest(string ExpectedToken, string Reason);
public sealed record OsanManagementResult(int Status, object? Value = null, string? Message = null);

public sealed partial class OsanProjectStore
{
    public static string EditToken(OsanProjectDetailResponse p) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { p.Title, p.ProjectCode, p.CustomerName,
            p.PoNumber, p.WorkOrderNumber, p.DeliveryDate, p.ProductName, p.Quantity }))));

    public async Task<OsanManagementResult> ManageAsync(Guid id, string expectedToken,
        NormalizedCreateOsanProjectInput? input, string? deleteReason, Guid actor, CancellationToken ct)
    {
        await using var source = CreateDataSource();
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("actor", actor);
        command.CommandText = "select id from projects where id=@id and project_profile='Osan' and deleted_at_utc is null for update";
        if (await command.ExecuteScalarAsync(ct) is null) return new(404);
        var current = await ReadDetailAsync(connection, tx, id, ct);
        if (current is null) return new(404);
        if (!string.Equals(expectedToken, EditToken(current), StringComparison.Ordinal))
            return new(409, Message: "다른 사용자가 정보를 변경했습니다. 새로고침 후 다시 확인해 주세요.");
        // The same project lock is acquired by progress completion, so quantity cannot race with first work.
        if (input is not null && input.Quantity != current.Quantity)
        {
            command.CommandText = """
                select exists(select 1 from osan_project_targets where project_id=@id
                  and (version>1 or started_at_utc is not null or status<>'NotStarted'))
                or exists(select 1 from osan_project_target_steps where project_id=@id
                  and (status<>'NotStarted' or started_at_utc is not null or completed_at_utc is not null))
                or exists(select 1 from osan_progress_operations where project_id=@id);
                """;
            if ((bool)(await command.ExecuteScalarAsync(ct))!)
                return new(409, Message: "진행이 시작된 프로젝트의 수량은 변경할 수 없습니다.");
            // Keep all rows and identities: a pristine excess target is hidden, never deleted.
            if (input.Quantity < current.Quantity)
            {
                command.Parameters.AddWithValue("new_quantity", input.Quantity);
                command.CommandText = "update osan_project_targets set is_active=false,updated_at_utc=now() where project_id=@id and sequence_number>@new_quantity";
                await command.ExecuteNonQueryAsync(ct);
            }
            else await InsertTargetsAndStepsAsync(connection, tx, id, input, ct, current.Quantity + 1);
        }
        if (input is null)
        {
            if (string.IsNullOrWhiteSpace(deleteReason) || deleteReason.Trim().Length > 500)
                return new(400, Message: "삭제 사유를 1~500자로 입력해 주세요.");
            command.Parameters.AddWithValue("reason", deleteReason.Trim());
            command.CommandText = """
                update projects set deleted_at_utc=now(), deleted_by_user_id=@actor,
                  delete_reason=@reason, updated_at_utc=now() where id=@id;
                """;
        }
        else
        {
            command.Parameters.AddWithValue("title", input.Title);
            command.Parameters.AddWithValue("code", input.ProjectCode);
            command.Parameters.AddWithValue("customer", input.CustomerName);
            command.Parameters.AddWithValue("po", (object?)input.PoNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("wo", (object?)input.WorkOrderNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("date", input.DeliveryDate);
            command.Parameters.AddWithValue("part", input.ProductName);
            command.Parameters.AddWithValue("quantity", input.Quantity);
            command.CommandText = """
                update projects set project_title=@title, project_code=@code, customer_name=@customer,
                  osan_po_number=@po, osan_work_order_number=@wo, delivery_date=@date,
                  osan_product_name=@part, osan_quantity=@quantity, updated_at_utc=now() where id=@id;
                update osan_project_targets set display_name=@part || ' ' || sequence_number::text,
                  updated_at_utc=now() where project_id=@id;
                """;
        }
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = """
            insert into osan_project_management_history(project_id,actor_user_id,action,before_json,after_json)
            values(@id,@actor,@action,cast(@before as jsonb),cast(@after as jsonb));
            """;
        command.Parameters.AddWithValue("action", input is null ? "Delete" : "Update");
        command.Parameters.AddWithValue("before", JsonSerializer.Serialize(current));
        command.Parameters.AddWithValue("after", JsonSerializer.Serialize(input));
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new(200, new { saved = true });
    }
}
