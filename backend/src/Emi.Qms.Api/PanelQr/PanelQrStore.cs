using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.PanelQr;

public sealed partial class PanelQrStore(
    DatabaseConnectionStringProvider connectionStringProvider,
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    private const int MaxPrintSheetItems = 50;

    public async Task<ProjectPanelQrListResponse?> ListProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select p.id,
                   pp.id,
                   pp.sequence_number,
                   pp.display_code,
                   coalesce(pp.panel_name, pp.display_code),
                   p.deleted_at_utc is null and p.status = 'Active' and pp.status = 'Active' and pp.panel_name is not null as qr_eligible,
                   q.id,
                   q.token,
                   q.status,
                   coalesce(u.display_name, u.development_user_key),
                   q.issued_at_utc
            from projects p
            join panel_placeholders pp on pp.project_id = p.id
            left join panel_qr_codes q on q.panel_id = pp.id and q.status = 'Active'
            left join qms_users u on u.id = q.issued_by_user_id
            where p.id = @project_id
              and p.deleted_at_utc is null
            order by pp.sequence_number;
            """);
        command.Parameters.AddWithValue("project_id", projectId);

        var panels = new List<ProjectPanelQrItemResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            PanelQrRecordResponse? qr = null;
            if (!reader.IsDBNull(6))
            {
                qr = new PanelQrRecordResponse(
                    reader.GetGuid(6),
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(8),
                    BuildScanUrl(reader.GetString(7)),
                    reader.GetString(9),
                    reader.GetFieldValue<DateTimeOffset>(10));
            }

            panels.Add(new ProjectPanelQrItemResponse(
                reader.GetGuid(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5),
                qr is not null,
                qr));
        }

        if (panels.Count == 0)
        {
            await using var existsCommand = dataSource.CreateCommand("select exists(select 1 from projects where id = @project_id and deleted_at_utc is null);");
            existsCommand.Parameters.AddWithValue("project_id", projectId);
            if (await existsCommand.ExecuteScalarAsync(cancellationToken) is not true)
            {
                return null;
            }
        }

        return new ProjectPanelQrListResponse(
            projectId,
            panels.Count(panel => panel.QrEligible),
            panels.Count(panel => panel.HasActiveQr),
            panels);
    }

    public async Task<PanelQrRecordResponse?> GetActiveAsync(Guid projectId, Guid panelId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand(ActiveQrSelect + " and q.project_id = @project_id and q.panel_id = @panel_id;");
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new PanelQrRecordResponse(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(4),
                BuildScanUrl(reader.GetString(3)),
                reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6))
            : null;
    }

    internal async Task<PanelQrSnapshot?> GetActiveSnapshotAsync(Guid projectId, Guid panelId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand(SnapshotSelect + " where q.project_id = @project_id and q.panel_id = @panel_id and q.status = 'Active';");
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
    }

    public async Task<PanelQrMutationResult<PanelQrRecordResponse>> IssueAsync(
        Guid projectId,
        Guid panelId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var panel = await LockPanelAsync(connection, transaction, projectId, panelId, cancellationToken);
            if (panel is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelQrMutationResult<PanelQrRecordResponse>.NotFound();
            }

            if (!panel.QrEligible)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelQrMutationResult<PanelQrRecordResponse>.Conflict("Active 프로젝트의 패널명을 입력한 활성 패널만 QR을 발급할 수 있습니다.");
            }

            var existing = await ReadActiveAsync(connection, transaction, projectId, panelId, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return PanelQrMutationResult<PanelQrRecordResponse>.Success(ToResponse(existing));
            }

            var qrId = Guid.NewGuid();
            var token = GenerateToken();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into panel_qr_codes (id, project_id, panel_id, token, status, issued_by_user_id, issued_at_utc)
                    values (@id, @project_id, @panel_id, @token, 'Active', @actor_user_id, @issued_at_utc);
                    """;
                command.Parameters.AddWithValue("id", qrId);
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("panel_id", panelId);
                command.Parameters.AddWithValue("token", token);
                command.Parameters.AddWithValue("actor_user_id", actorUserId);
                command.Parameters.AddWithValue("issued_at_utc", timeProvider.GetUtcNow());
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertEventAsync(connection, transaction, qrId, projectId, panelId, "Issued", null, null, actorUserId, correlationId, cancellationToken);
            var created = await ReadActiveAsync(connection, transaction, projectId, panelId, cancellationToken)
                ?? throw new InvalidOperationException("Issued QR record could not be read.");
            await transaction.CommitAsync(cancellationToken);
            return PanelQrMutationResult<PanelQrRecordResponse>.Success(ToResponse(created));
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<PanelQrMutationResult<PanelQrRecordResponse>> RotateAsync(
        Guid projectId,
        Guid panelId,
        string? reason,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedReason = reason?.Trim();
        if (normalizedReason is null || normalizedReason.Length is < 2 or > 500)
        {
            return PanelQrMutationResult<PanelQrRecordResponse>.Validation("reason", "재발급 사유를 2~500자로 입력해 주세요.");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var panel = await LockPanelAsync(connection, transaction, projectId, panelId, cancellationToken);
            if (panel is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelQrMutationResult<PanelQrRecordResponse>.NotFound();
            }

            var active = await ReadActiveAsync(connection, transaction, projectId, panelId, cancellationToken);
            if (active is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelQrMutationResult<PanelQrRecordResponse>.Conflict("재발급할 활성 QR이 없습니다. 먼저 QR을 발급해 주세요.");
            }

            var now = timeProvider.GetUtcNow();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    update panel_qr_codes
                    set status = 'Revoked',
                        revoked_by_user_id = @actor_user_id,
                        revoked_at_utc = @revoked_at_utc,
                        revoke_reason = @reason
                    where id = @id and status = 'Active';
                    """;
                command.Parameters.AddWithValue("id", active.QrCodeId);
                command.Parameters.AddWithValue("actor_user_id", actorUserId);
                command.Parameters.AddWithValue("revoked_at_utc", now);
                command.Parameters.AddWithValue("reason", normalizedReason);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var qrId = Guid.NewGuid();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    insert into panel_qr_codes (id, project_id, panel_id, token, status, issued_by_user_id, issued_at_utc)
                    values (@id, @project_id, @panel_id, @token, 'Active', @actor_user_id, @issued_at_utc);
                    """;
                command.Parameters.AddWithValue("id", qrId);
                command.Parameters.AddWithValue("project_id", projectId);
                command.Parameters.AddWithValue("panel_id", panelId);
                command.Parameters.AddWithValue("token", GenerateToken());
                command.Parameters.AddWithValue("actor_user_id", actorUserId);
                command.Parameters.AddWithValue("issued_at_utc", now);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await InsertEventAsync(connection, transaction, qrId, projectId, panelId, "Rotated", null, null, actorUserId, correlationId, cancellationToken);
            var created = await ReadActiveAsync(connection, transaction, projectId, panelId, cancellationToken)
                ?? throw new InvalidOperationException("Rotated QR record could not be read.");
            await transaction.CommitAsync(cancellationToken);
            return PanelQrMutationResult<PanelQrRecordResponse>.Success(ToResponse(created));
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    internal async Task<PanelQrMutationResult<IReadOnlyList<PanelQrSnapshot>>> PreparePrintSheetAsync(
        Guid projectId,
        IReadOnlyList<Guid>? panelIds,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var ids = (panelIds ?? []).Distinct().ToArray();
        if (ids.Length is < 1 or > MaxPrintSheetItems)
        {
            return PanelQrMutationResult<IReadOnlyList<PanelQrSnapshot>>.Validation("panelIds", "같은 프로젝트에서 QR이 발급된 패널 1~50개를 선택해 주세요.");
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = SnapshotSelect + "\n" + """
                where q.project_id = @project_id
                  and q.panel_id = any(@panel_ids)
                  and q.status = 'Active'
                  and p.deleted_at_utc is null
                  and pp.status = 'Active'
                order by pp.sequence_number
                for share of q, p, pp;
                """;
            command.Parameters.AddWithValue("project_id", projectId);
            command.Parameters.Add(new NpgsqlParameter<Guid[]>("panel_ids", ids));
            var rows = new List<PanelQrSnapshot>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(ReadSnapshot(reader));
                }
            }

            if (rows.Count != ids.Length)
            {
                await transaction.RollbackAsync(cancellationToken);
                return PanelQrMutationResult<IReadOnlyList<PanelQrSnapshot>>.Conflict($"선택 {ids.Length}개 중 현재 인쇄 가능한 QR은 {rows.Count}개입니다. 목록을 새로고침해 주세요.");
            }

            foreach (var row in rows)
            {
                await InsertEventAsync(connection, transaction, row.QrCodeId, projectId, row.PanelId, "PrintSheetRendered", null, rows.Count, actorUserId, correlationId, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return PanelQrMutationResult<IReadOnlyList<PanelQrSnapshot>>.Success(rows);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            throw;
        }
    }

    internal async Task<PanelQrSnapshot?> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        if (!TokenPattern().IsMatch(token))
        {
            return null;
        }

        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand(SnapshotSelect + " where q.token = @token;");
        command.Parameters.AddWithValue("token", token);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
    }

    public async Task<string?> GetUserDepartmentCodeAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select d.code
            from qms_users u
            left join departments d on d.id = u.department_id
            where u.id = @user_id and u.is_active = true;
            """);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    internal async Task RecordImageRenderedAsync(PanelQrSnapshot snapshot, Guid actorUserId, string correlationId, CancellationToken cancellationToken)
        => await RecordEventAsync(snapshot, "ImageRendered", null, null, actorUserId, correlationId, cancellationToken);

    internal async Task RecordResolveAsync(PanelQrSnapshot snapshot, string status, Guid actorUserId, string correlationId, CancellationToken cancellationToken)
        => await RecordEventAsync(snapshot, status is "Ok" or "OkCompletedProject" ? "ResolveSucceeded" : "ResolveStateViewed", status, null, actorUserId, correlationId, cancellationToken);

    public string BuildScanUrl(string token)
    {
        var origin = configuration["Qr:ScanOrigin"]
            ?? configuration["QR_SCAN_ORIGIN"]
            ?? configuration["Frontend:Origin"]
            ?? configuration["FRONTEND_ORIGIN"]
            ?? "https://localhost:5174";
        var firstOrigin = origin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
            ?? "https://localhost:5174";
        return $"{firstOrigin.TrimEnd('/')}/q/{token}";
    }

    private async Task RecordEventAsync(PanelQrSnapshot snapshot, string eventType, string? outcomeStatus, int? itemCount, Guid actorUserId, string correlationId, CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await InsertEventAsync(connection, null, snapshot.QrCodeId, snapshot.ProjectId, snapshot.PanelId, eventType, outcomeStatus, itemCount, actorUserId, correlationId, cancellationToken);
    }

    private static async Task<LockedPanel?> LockPanelAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid panelId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select p.deleted_at_utc is null and p.status = 'Active' and pp.status = 'Active' and pp.panel_name is not null
            from projects p
            join panel_placeholders pp on pp.project_id = p.id
            where p.id = @project_id and pp.id = @panel_id and p.deleted_at_utc is null
            for update of p, pp;
            """;
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool eligible ? new LockedPanel(eligible) : null;
    }

    private static async Task<PanelQrSnapshot?> ReadActiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid panelId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SnapshotSelect + " where q.project_id = @project_id and q.panel_id = @panel_id and q.status = 'Active';";
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSnapshot(reader) : null;
    }

    private static async Task InsertEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid qrCodeId,
        Guid projectId,
        Guid panelId,
        string eventType,
        string? outcomeStatus,
        int? itemCount,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            insert into panel_qr_events (
                id, qr_code_id, project_id, panel_id, event_type, outcome_status,
                item_count, actor_user_id, correlation_id, occurred_at_utc
            )
            values (
                @id, @qr_code_id, @project_id, @panel_id, @event_type, @outcome_status,
                @item_count, @actor_user_id, @correlation_id, now()
            );
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("qr_code_id", qrCodeId);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("panel_id", panelId);
        command.Parameters.AddWithValue("event_type", eventType);
        command.Parameters.Add("outcome_status", NpgsqlDbType.Text).Value = outcomeStatus ?? (object)DBNull.Value;
        command.Parameters.Add("item_count", NpgsqlDbType.Integer).Value = itemCount ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        command.Parameters.Add("correlation_id", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(correlationId) ? DBNull.Value : correlationId;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private PanelQrRecordResponse ToResponse(PanelQrSnapshot snapshot)
        => new(snapshot.QrCodeId, snapshot.ProjectId, snapshot.PanelId, snapshot.Status, BuildScanUrl(snapshot.Token), snapshot.IssuedByName, snapshot.IssuedAtUtc);

    private static PanelQrSnapshot ReadSnapshot(NpgsqlDataReader reader)
        => new(
            reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4),
            reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7), reader.GetString(8),
            reader.GetString(9), reader.GetString(10), reader.GetBoolean(11), reader.GetString(12), reader.GetString(13), reader.GetString(14));

    private static string GenerateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task RollbackQuietlyAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        try { await transaction.RollbackAsync(cancellationToken); } catch (InvalidOperationException) { }
    }

    private NpgsqlDataSource CreateDataSource()
    {
        var connectionString = connectionStringProvider.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("QMS database connection string is not configured.");
        return NpgsqlDataSource.Create(connectionString);
    }

    private const string ActiveQrSelect = """
        select q.id, q.project_id, q.panel_id, q.token, q.status,
               coalesce(u.display_name, u.development_user_key), q.issued_at_utc
        from panel_qr_codes q
        join qms_users u on u.id = q.issued_by_user_id
        where q.status = 'Active'
        """;

    private const string SnapshotSelect = """
        select q.id, q.project_id, q.panel_id, q.token, q.status,
               coalesce(u.display_name, u.development_user_key), q.issued_at_utc,
               p.project_key, coalesce(p.project_code, p.project_number), coalesce(p.project_title, p.name),
               p.status, p.deleted_at_utc is not null, pp.status, pp.display_code, coalesce(pp.panel_name, pp.display_code)
        from panel_qr_codes q
        join projects p on p.id = q.project_id
        join panel_placeholders pp on pp.id = q.panel_id and pp.project_id = p.id
        join qms_users u on u.id = q.issued_by_user_id
        """;

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    private sealed record LockedPanel(bool QrEligible);
}
