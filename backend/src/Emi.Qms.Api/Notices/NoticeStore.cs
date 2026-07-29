using System.Data;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;

namespace Emi.Qms.Api.Notices;

public sealed partial class NoticeStore(DatabaseConnectionStringProvider connectionStringProvider)
{
    public async Task<NoticeListResponse> ListAsync(
        Guid actorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select id,title,body,author_user_id,author_display_name_snapshot,
                   author_department_name_snapshot,created_at_utc,count(*) over ()::integer
            from notice_posts
            where deleted_at_utc is null
            order by created_at_utc desc,id desc
            limit @page_size offset @offset;
            """);
        command.Parameters.AddWithValue("page_size", pageSize);
        command.Parameters.AddWithValue("offset", checked((long)(page - 1) * pageSize));

        var items = new List<NoticeListItemResponse>();
        var totalCount = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            totalCount = reader.GetInt32(7);
            items.Add(new NoticeListItemResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                CreatePreview(reader.GetString(2)),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetGuid(3) == actorUserId));
        }

        if (items.Count == 0 && page > 1)
        {
            await using var countCommand = dataSource.CreateCommand(
                "select count(*)::integer from notice_posts where deleted_at_utc is null;");
            totalCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        }

        return new NoticeListResponse(items, totalCount, page, pageSize);
    }

    public async Task<NoticeMutationResult<NoticeDetailResponse>> GetAsync(
        Guid noticeId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var detail = await ReadActiveDetailAsync(connection, null, noticeId, actorUserId, cancellationToken);
        return detail is null
            ? NoticeMutationResult<NoticeDetailResponse>.NotFound()
            : NoticeMutationResult<NoticeDetailResponse>.Success(detail);
    }

    public async Task<NoticeMutationResult<NoticeDetailResponse>> CreateAsync(
        CreateNoticeRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return NoticeMutationResult<NoticeDetailResponse>.Validation(errors);
        }

        var title = request.Title!.Trim();
        var body = request.Body!.Trim();
        var requestId = request.RequestId!.Value;

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var author = await ReadAuthorAsync(connection, transaction, actorUserId, cancellationToken);
        if (author is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return NoticeMutationResult<NoticeDetailResponse>.Forbidden();
        }

        Guid? noticeId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into notice_posts (
                    title,body,author_user_id,author_display_name_snapshot,
                    author_department_name_snapshot,request_id)
                values (@title,@body,@author_user_id,@author_display_name,@author_department_name,@request_id)
                on conflict (author_user_id,request_id) do nothing
                returning id;
                """;
            insert.Parameters.AddWithValue("title", title);
            insert.Parameters.AddWithValue("body", body);
            insert.Parameters.AddWithValue("author_user_id", actorUserId);
            insert.Parameters.AddWithValue("author_display_name", author.DisplayName);
            AddNullableText(insert, "author_department_name", author.DepartmentName);
            insert.Parameters.AddWithValue("request_id", requestId);
            noticeId = await insert.ExecuteScalarAsync(cancellationToken) as Guid?;
        }

        if (noticeId is null)
        {
            var existingWasDeleted = false;
            await using (var existing = connection.CreateCommand())
            {
                existing.Transaction = transaction;
                existing.CommandText = """
                    select id,deleted_at_utc is not null
                    from notice_posts
                    where author_user_id=@author_user_id and request_id=@request_id;
                    """;
                existing.Parameters.AddWithValue("author_user_id", actorUserId);
                existing.Parameters.AddWithValue("request_id", requestId);
                await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NoticeMutationResult<NoticeDetailResponse>.Conflict("공지 등록 상태를 확인할 수 없습니다. 새로고침 후 다시 시도해 주세요.");
                }
                noticeId = reader.GetGuid(0);
                existingWasDeleted = reader.GetBoolean(1);
            }
            if (existingWasDeleted)
            {
                return NoticeMutationResult<NoticeDetailResponse>.Conflict("삭제된 등록 요청입니다. 공지 작성을 다시 열어 등록해 주세요.");
            }
        }

        var detail = await ReadActiveDetailAsync(connection, transaction, noticeId.Value, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return detail is null
            ? NoticeMutationResult<NoticeDetailResponse>.Conflict("등록된 공지를 불러올 수 없습니다. 새로고침해 주세요.")
            : NoticeMutationResult<NoticeDetailResponse>.Success(detail);
    }

    public async Task<NoticeMutationResult<NoticeDeleteResponse>> DeleteAsync(
        Guid noticeId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update notice_posts
                set deleted_at_utc=now(),deleted_by_user_id=@actor_user_id
                where id=@notice_id and author_user_id=@actor_user_id and deleted_at_utc is null;
                """;
            update.Parameters.AddWithValue("notice_id", noticeId);
            update.Parameters.AddWithValue("actor_user_id", actorUserId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 1)
            {
                await transaction.CommitAsync(cancellationToken);
                return NoticeMutationResult<NoticeDeleteResponse>.Success(new NoticeDeleteResponse(noticeId, true));
            }
        }

        Guid? authorUserId = null;
        var wasDeleted = false;
        await using (var inspect = connection.CreateCommand())
        {
            inspect.Transaction = transaction;
            inspect.CommandText = "select author_user_id,deleted_at_utc is not null from notice_posts where id=@notice_id;";
            inspect.Parameters.AddWithValue("notice_id", noticeId);
            await using var reader = await inspect.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                authorUserId = reader.GetGuid(0);
                wasDeleted = reader.GetBoolean(1);
            }
        }
        if (authorUserId is null)
        {
            return NoticeMutationResult<NoticeDeleteResponse>.NotFound();
        }
        if (authorUserId != actorUserId)
        {
            return NoticeMutationResult<NoticeDeleteResponse>.Forbidden();
        }

        await transaction.CommitAsync(cancellationToken);
        return NoticeMutationResult<NoticeDeleteResponse>.Success(new NoticeDeleteResponse(noticeId, wasDeleted));
    }

    private static async Task<NoticeDetailResponse?> ReadActiveDetailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid noticeId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,title,body,author_user_id,author_display_name_snapshot,
                   author_department_name_snapshot,created_at_utc
            from notice_posts
            where id=@notice_id and deleted_at_utc is null;
            """;
        command.Parameters.AddWithValue("notice_id", noticeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new NoticeDetailResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetGuid(3) == actorUserId);
    }

    private static async Task<AuthorSnapshot?> ReadAuthorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select qms_user.display_name,department.name
            from qms_users qms_user
            left join departments department on department.id=qms_user.department_id
            where qms_user.id=@actor_user_id
              and qms_user.is_active=true
              and qms_user.deletion_requested_at_utc is null;
            """;
        command.Parameters.AddWithValue("actor_user_id", actorUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        return new AuthorSnapshot(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static Dictionary<string, string[]> Validate(CreateNoticeRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.RequestId is null || request.RequestId == Guid.Empty)
        {
            errors["requestId"] = ["등록 요청을 식별할 수 없습니다. 공지 작성을 다시 열어 주세요."];
        }
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["제목을 입력해 주세요."];
        }
        else if (request.Title.Trim().Length > 100)
        {
            errors["title"] = ["제목은 100자 이하로 입력해 주세요."];
        }
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            errors["body"] = ["내용을 입력해 주세요."];
        }
        else if (request.Body.Trim().Length > 2000)
        {
            errors["body"] = ["내용은 2,000자 이하로 입력해 주세요."];
        }
        return errors;
    }

    private static string CreatePreview(string body)
    {
        var collapsed = WhitespaceRegex().Replace(body, " ").Trim();
        return collapsed.Length <= 100 ? collapsed : collapsed[..100];
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

    private static void AddNullableText(NpgsqlCommand command, string name, string? value)
        => command.Parameters.Add(name, NpgsqlDbType.Text).Value = value ?? (object)DBNull.Value;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private sealed record AuthorSnapshot(string DisplayName, string? DepartmentName);
}
