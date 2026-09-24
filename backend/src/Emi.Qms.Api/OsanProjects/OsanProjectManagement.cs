using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Emi.Qms.Api.OsanProjects;

public sealed record UpdateOsanProjectRequest(CreateOsanProjectRequest Fields, string ExpectedToken, bool? DeliveryHold = null, string? HoldReason = null);
public sealed record DeleteOsanProjectRequest(string ExpectedToken, string Reason);
public sealed record OsanManagementResult(int Status, object? Value = null, string? Message = null);

public sealed partial class OsanProjectStore
{
    public static string EditToken(OsanProjectDetailResponse p) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { p.Title, p.ProjectCode, p.CustomerName,
            p.PoNumber, p.WorkOrderNumber, p.DeliveryDate, p.ProductName, p.Quantity, p.DeliveryHold }))));

    public async Task<OsanManagementResult> ManageAsync(Guid id, string expectedToken,
        NormalizedCreateOsanProjectInput? input, string? deleteReason, Guid actor, CancellationToken ct,
        bool? deliveryHold = null, string? holdReason = null, bool quantityWasSpecified = true)
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
        if (input is not null)
        {
            var customer = await OsanPolicyStore.BoundCustomerAsync(connection, tx,
                input.CustomerId, input.CustomerName, ct);
            if (customer is null)
                return new(400, Message: "등록된 고객사를 선택하고 연결 상태를 다시 확인해 주세요.");
            input = input with { CustomerName = customer.Name };
        }
        var nextHold = deliveryHold ?? current.DeliveryHold;
        var holdChanged = input is not null && nextHold != current.DeliveryHold;
        if (holdChanged && (string.IsNullOrWhiteSpace(holdReason) || holdReason.Trim().Length > 500))
            return new(400, Message: "납기 HOLD 변경 사유를 1~500자로 입력해 주세요.");
        if (input is not null && quantityWasSpecified && input.Quantity != current.Quantity)
        {
            return new(400, Message: "오산 프로젝트 수량은 변경할 수 없습니다.");
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
            command.Parameters.AddWithValue("customer_id", input.CustomerId!.Value);
            command.Parameters.AddWithValue("po", (object?)input.PoNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("wo", (object?)input.WorkOrderNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("date", input.DeliveryDate);
            command.Parameters.AddWithValue("part", input.ProductName);
            command.Parameters.AddWithValue("quantity", current.Quantity);
            command.Parameters.AddWithValue("hold", nextHold);
            command.CommandText = """
                update projects set project_title=@title, project_code=@code, customer_name=@customer,
                  osan_po_number=@po, osan_work_order_number=@wo, delivery_date=@date,
                  osan_product_name=@part, osan_quantity=@quantity, osan_customer_id=@customer_id,
                  osan_delivery_hold=@hold, updated_at_utc=now() where id=@id;
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
        command.Parameters.AddWithValue("after", JsonSerializer.Serialize(new
        {
            Fields = input is null ? null : input with { Quantity = current.Quantity },
            DeliveryHold = nextHold,
            HoldReason = holdChanged ? holdReason!.Trim() : null
        }));
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new(200, new { saved = true });
    }
}
