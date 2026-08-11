using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.Notices;

public static class NoticeEndpointExtensions
{
    public static IEndpointRouteBuilder MapNoticeEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/notices").RequireAuthorization();

        api.MapGet("", async (int? page, int? pageSize, NoticeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }
            if (page is < 1 || pageSize is < 1 or > 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [page is < 1 ? "page" : "pageSize"] = [page is < 1 ? "페이지는 1 이상이어야 합니다." : "페이지 크기는 1~100 사이여야 합니다."]
                });
            }
            return Results.Ok(await store.ListAsync(actorUserId.Value, page ?? 1, pageSize ?? 20, token));
        }).WithName("ListNotices");

        api.MapGet("/{noticeId:guid}", async (Guid noticeId, NoticeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(await store.GetAsync(noticeId, actorUserId.Value, token), Results.Ok);
        }).WithName("GetNotice");

        api.MapPost("", async (CreateNoticeRequest request, NoticeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(await store.CreateAsync(request, actorUserId.Value, token), Results.Ok);
        }).WithName("CreateNotice");

        api.MapPut("/{noticeId:guid}", async (
            Guid noticeId,
            UpdateNoticeRequest request,
            NoticeStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(await store.UpdateAsync(noticeId, request, actorUserId.Value, token), Results.Ok);
        }).WithName("UpdateNotice");

        api.MapPost("/{noticeId:guid}/attachments", async (
            Guid noticeId,
            [FromForm] IFormFile file,
            NoticeStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            if (actorUserId is null)
            {
                return Results.Unauthorized();
            }
            if (file.Length is < 1 or > NoticeAttachmentValidator.MaximumFileBytes)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["파일은 개별 10MB 이하여야 합니다."]
                });
            }
            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, token);
            return ToResult(
                await store.AddAttachmentAsync(noticeId, file.FileName, memory.ToArray(), actorUserId.Value, token),
                Results.Ok);
        })
        .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024))
        .DisableAntiforgery()
        .WithName("UploadNoticeAttachment");

        api.MapDelete("/{noticeId:guid}/attachments/{attachmentId:guid}", async (
            Guid noticeId,
            Guid attachmentId,
            NoticeStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(
                    await store.DeleteAttachmentAsync(noticeId, attachmentId, actorUserId.Value, token),
                    Results.Ok);
        }).WithName("DeleteNoticeAttachment");

        api.MapGet("/{noticeId:guid}/attachments/{attachmentId:guid}/content", async (
            Guid noticeId,
            Guid attachmentId,
            NoticeStore store,
            HttpContext context,
            CancellationToken token) =>
        {
            var result = await store.GetAttachmentContentAsync(noticeId, attachmentId, token);
            if (result.Status == NoticeMutationStatus.NotFound || result.Value is null)
            {
                return Results.NotFound();
            }
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).WithName("DownloadNoticeAttachment");

        api.MapDelete("/{noticeId:guid}", async (Guid noticeId, NoticeStore store, ClaimsPrincipal user, CancellationToken token) =>
        {
            var actorUserId = UserId(user);
            return actorUserId is null
                ? Results.Unauthorized()
                : ToResult(await store.DeleteAsync(noticeId, actorUserId.Value, token), Results.Ok);
        }).WithName("DeleteNotice");

        return app;
    }

    private static Guid? UserId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;

    private static IResult ToResult<T>(NoticeMutationResult<T> result, Func<T, IResult> success)
        => result.Status switch
        {
            NoticeMutationStatus.Success when result.Value is not null => success(result.Value),
            NoticeMutationStatus.NotFound => Results.NotFound(),
            NoticeMutationStatus.Forbidden => Results.Forbid(),
            NoticeMutationStatus.Conflict => Results.Problem(
                title: result.Message ?? "공지 상태가 변경되었습니다. 새로고침해 주세요.",
                statusCode: StatusCodes.Status409Conflict),
            NoticeMutationStatus.Validation when result.Errors is not null => Results.ValidationProblem(result.Errors),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
}
