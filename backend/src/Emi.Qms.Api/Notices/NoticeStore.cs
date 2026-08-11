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
                   author_department_name_snapshot,created_at_utc,updated_at_utc,
                   count(*) over ()::integer
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
            totalCount = reader.GetInt32(8);
            items.Add(new NoticeListItemResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                CreatePreview(reader.GetString(2)),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetGuid(3) == actorUserId,
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7)));
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
        var bodyFormat = request.BodyFormat ?? NoticeBodyFormats.BoldMarkupV1;
        var errors = ValidateFields(request.Title, request.Body, bodyFormat);
        if (request.RequestId is null || request.RequestId == Guid.Empty)
        {
            errors["requestId"] = ["등록 요청을 식별할 수 없습니다. 공지 작성을 다시 열어 주세요."];
        }
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
            return NoticeMutationResult<NoticeDetailResponse>.Forbidden();
        }

        Guid? noticeId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into notice_posts (
                    title,body,body_format,author_user_id,author_display_name_snapshot,
                    author_department_name_snapshot,request_id)
                values (@title,@body,@body_format,@author_user_id,@author_display_name,@author_department_name,@request_id)
                on conflict (author_user_id,request_id) do nothing
                returning id;
                """;
            insert.Parameters.AddWithValue("title", title);
            insert.Parameters.AddWithValue("body", body);
            insert.Parameters.AddWithValue("body_format", bodyFormat);
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

    public async Task<NoticeMutationResult<NoticeDetailResponse>> UpdateAsync(
        Guid noticeId,
        UpdateNoticeRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var errors = ValidateFields(request.Title, request.Body, request.BodyFormat);
        if (request.ExpectedVersion is null or < 1)
        {
            errors["expectedVersion"] = ["최신 공지 version이 필요합니다. 새로고침해 주세요."];
        }
        if (errors.Count > 0)
        {
            return NoticeMutationResult<NoticeDetailResponse>.Validation(errors);
        }

        var title = request.Title!.Trim();
        var body = request.Body!.Trim();
        var bodyFormat = request.BodyFormat!;

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var current = await ReadNoticeForUpdateAsync(connection, transaction, noticeId, cancellationToken);
        if (current is null || current.Deleted)
        {
            return NoticeMutationResult<NoticeDetailResponse>.NotFound();
        }
        if (current.AuthorUserId != actorUserId)
        {
            return NoticeMutationResult<NoticeDetailResponse>.Forbidden();
        }
        if (current.Version != request.ExpectedVersion)
        {
            return NoticeMutationResult<NoticeDetailResponse>.Conflict("다른 화면에서 공지가 먼저 수정되었습니다. 최신 내용을 다시 불러와 주세요.");
        }

        if (current.Title == title && current.Body == body && current.BodyFormat == bodyFormat)
        {
            var unchanged = await ReadActiveDetailAsync(connection, transaction, noticeId, actorUserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return unchanged is null
                ? NoticeMutationResult<NoticeDetailResponse>.NotFound()
                : NoticeMutationResult<NoticeDetailResponse>.Success(unchanged);
        }

        await using (var revision = connection.CreateCommand())
        {
            revision.Transaction = transaction;
            revision.CommandText = """
                insert into notice_post_revisions (
                    notice_post_id,version,title,body,body_format,changed_by_user_id)
                values (@notice_id,@version,@title,@body,@body_format,@actor_user_id);
                """;
            revision.Parameters.AddWithValue("notice_id", noticeId);
            revision.Parameters.AddWithValue("version", current.Version);
            revision.Parameters.AddWithValue("title", current.Title);
            revision.Parameters.AddWithValue("body", current.Body);
            revision.Parameters.AddWithValue("body_format", current.BodyFormat);
            revision.Parameters.AddWithValue("actor_user_id", actorUserId);
            await revision.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                update notice_posts
                set title=@title,body=@body,body_format=@body_format,
                    version=version + 1,updated_at_utc=now(),updated_by_user_id=@actor_user_id
                where id=@notice_id and version=@expected_version and deleted_at_utc is null;
                """;
            update.Parameters.AddWithValue("title", title);
            update.Parameters.AddWithValue("body", body);
            update.Parameters.AddWithValue("body_format", bodyFormat);
            update.Parameters.AddWithValue("actor_user_id", actorUserId);
            update.Parameters.AddWithValue("notice_id", noticeId);
            update.Parameters.AddWithValue("expected_version", request.ExpectedVersion.Value);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return NoticeMutationResult<NoticeDetailResponse>.Conflict("다른 화면에서 공지가 먼저 수정되었습니다. 최신 내용을 다시 불러와 주세요.");
            }
        }

        var detail = await ReadActiveDetailAsync(connection, transaction, noticeId, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return detail is null
            ? NoticeMutationResult<NoticeDetailResponse>.NotFound()
            : NoticeMutationResult<NoticeDetailResponse>.Success(detail);
    }

    public async Task<NoticeMutationResult<NoticeAttachmentResponse>> AddAttachmentAsync(
        Guid noticeId,
        string? fileName,
        byte[] content,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var validated = NoticeAttachmentValidator.Validate(fileName, content);
        if (!validated.IsValid)
        {
            return NoticeMutationResult<NoticeAttachmentResponse>.Validation(
                new Dictionary<string, string[]> { ["file"] = [validated.Error!] });
        }

        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var current = await ReadNoticeForUpdateAsync(connection, transaction, noticeId, cancellationToken);
        if (current is null || current.Deleted)
        {
            return NoticeMutationResult<NoticeAttachmentResponse>.NotFound();
        }
        if (current.AuthorUserId != actorUserId)
        {
            return NoticeMutationResult<NoticeAttachmentResponse>.Forbidden();
        }

        await using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = """
                select count(*)::integer
                from notice_attachments
                where notice_post_id=@notice_id and deleted_at_utc is null;
                """;
            count.Parameters.AddWithValue("notice_id", noticeId);
            if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) >= NoticeAttachmentValidator.MaximumAttachments)
            {
                return NoticeMutationResult<NoticeAttachmentResponse>.Conflict("공지당 첨부파일은 최대 5개까지 등록할 수 있습니다.");
            }
        }

        Guid attachmentId;
        DateTimeOffset createdAtUtc;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                insert into notice_attachments (
                    notice_post_id,original_file_name,normalized_mime,byte_size,
                    sha256,content,created_by_user_id)
                values (@notice_id,@file_name,@content_type,@byte_size,@sha256,@content,@actor_user_id)
                returning id,created_at_utc;
                """;
            insert.Parameters.AddWithValue("notice_id", noticeId);
            insert.Parameters.AddWithValue("file_name", validated.FileName!);
            insert.Parameters.AddWithValue("content_type", validated.ContentType!);
            insert.Parameters.AddWithValue("byte_size", content.Length);
            insert.Parameters.AddWithValue("sha256", validated.Sha256!);
            insert.Parameters.Add("content", NpgsqlDbType.Bytea).Value = content;
            insert.Parameters.AddWithValue("actor_user_id", actorUserId);
            await using var reader = await insert.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            attachmentId = reader.GetGuid(0);
            createdAtUtc = reader.GetFieldValue<DateTimeOffset>(1);
        }

        await transaction.CommitAsync(cancellationToken);
        return NoticeMutationResult<NoticeAttachmentResponse>.Success(new NoticeAttachmentResponse(
            attachmentId,
            validated.FileName!,
            validated.ContentType!,
            content.Length,
            createdAtUtc,
            true));
    }

    public async Task<NoticeMutationResult<NoticeAttachmentDeleteResponse>> DeleteAttachmentAsync(
        Guid noticeId,
        Guid attachmentId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var current = await ReadNoticeForUpdateAsync(connection, transaction, noticeId, cancellationToken);
        if (current is null || current.Deleted)
        {
            return NoticeMutationResult<NoticeAttachmentDeleteResponse>.NotFound();
        }
        if (current.AuthorUserId != actorUserId)
        {
            return NoticeMutationResult<NoticeAttachmentDeleteResponse>.Forbidden();
        }

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            update notice_attachments
            set deleted_at_utc=now(),deleted_by_user_id=@actor_user_id
            where id=@attachment_id and notice_post_id=@notice_id and deleted_at_utc is null;
            """;
        update.Parameters.AddWithValue("actor_user_id", actorUserId);
        update.Parameters.AddWithValue("attachment_id", attachmentId);
        update.Parameters.AddWithValue("notice_id", noticeId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            return NoticeMutationResult<NoticeAttachmentDeleteResponse>.NotFound();
        }

        await transaction.CommitAsync(cancellationToken);
        return NoticeMutationResult<NoticeAttachmentDeleteResponse>.Success(
            new NoticeAttachmentDeleteResponse(noticeId, attachmentId, true));
    }

    public async Task<NoticeMutationResult<NoticeAttachmentContentResult>> GetAttachmentContentAsync(
        Guid noticeId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await using var dataSource = CreateDataSource();
        await using var command = dataSource.CreateCommand("""
            select attachment.content,attachment.normalized_mime,attachment.original_file_name
            from notice_attachments attachment
            join notice_posts notice on notice.id=attachment.notice_post_id
            where notice.id=@notice_id
              and attachment.id=@attachment_id
              and notice.deleted_at_utc is null
              and attachment.deleted_at_utc is null;
            """);
        command.Parameters.AddWithValue("notice_id", noticeId);
        command.Parameters.AddWithValue("attachment_id", attachmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? NoticeMutationResult<NoticeAttachmentContentResult>.Success(new NoticeAttachmentContentResult(
                reader.GetFieldValue<byte[]>(0),
                reader.GetString(1),
                reader.GetString(2)))
            : NoticeMutationResult<NoticeAttachmentContentResult>.NotFound();
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
        NoticeDetailRow? row;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select id,title,body,body_format,version,author_user_id,author_display_name_snapshot,
                       author_department_name_snapshot,created_at_utc,updated_at_utc
                from notice_posts
                where id=@notice_id and deleted_at_utc is null;
                """;
            command.Parameters.AddWithValue("notice_id", noticeId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            row = new NoticeDetailRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetGuid(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9));
        }

        var canManage = row.AuthorUserId == actorUserId;
        var attachments = await ReadAttachmentsAsync(
            connection,
            transaction,
            noticeId,
            canManage,
            cancellationToken);
        return new NoticeDetailResponse(
            row.NoticeId,
            row.Title,
            row.Body,
            row.BodyFormat,
            row.Version,
            row.AuthorDisplayName,
            row.AuthorDepartmentName,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            canManage,
            canManage,
            attachments);
    }

    private static async Task<IReadOnlyList<NoticeAttachmentResponse>> ReadAttachmentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid noticeId,
        bool canDelete,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select id,original_file_name,normalized_mime,byte_size,created_at_utc
            from notice_attachments
            where notice_post_id=@notice_id and deleted_at_utc is null
            order by created_at_utc,id;
            """;
        command.Parameters.AddWithValue("notice_id", noticeId);
        var items = new List<NoticeAttachmentResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new NoticeAttachmentResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                canDelete));
        }
        return items;
    }

    private static async Task<NoticeForUpdate?> ReadNoticeForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid noticeId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            select author_user_id,deleted_at_utc is not null,version,title,body,body_format
            from notice_posts
            where id=@notice_id
            for update;
            """;
        command.Parameters.AddWithValue("notice_id", noticeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new NoticeForUpdate(
                reader.GetGuid(0),
                reader.GetBoolean(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5))
            : null;
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

    private static Dictionary<string, string[]> ValidateFields(string? title, string? body, string? bodyFormat)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["제목을 입력해 주세요."];
        }
        else if (title.Trim().Length > 100)
        {
            errors["title"] = ["제목은 100자 이하로 입력해 주세요."];
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            errors["body"] = ["내용을 입력해 주세요."];
        }
        else if (body.Trim().Length > 2000)
        {
            errors["body"] = ["내용은 2,000자 이하로 입력해 주세요."];
        }
        if (bodyFormat is not NoticeBodyFormats.PlainTextV1 and not NoticeBodyFormats.BoldMarkupV1)
        {
            errors["bodyFormat"] = ["지원하지 않는 공지 본문 형식입니다."];
        }
        return errors;
    }

    private static string CreatePreview(string body)
    {
        var collapsed = WhitespaceRegex().Replace(body.Replace("**", string.Empty, StringComparison.Ordinal), " ").Trim();
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

    private sealed record NoticeForUpdate(
        Guid AuthorUserId,
        bool Deleted,
        int Version,
        string Title,
        string Body,
        string BodyFormat);

    private sealed record NoticeDetailRow(
        Guid NoticeId,
        string Title,
        string Body,
        string BodyFormat,
        int Version,
        Guid AuthorUserId,
        string AuthorDisplayName,
        string? AuthorDepartmentName,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc);
}
