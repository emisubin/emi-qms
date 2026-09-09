using System.Security.Claims;
using System.Text.Json;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.BusinessUnits;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Microsoft.AspNetCore.Mvc;

namespace Emi.Qms.Api.OsanProjects;

public static class OsanProgressEndpointExtensions
{
    public static IEndpointRouteBuilder MapOsanProgressEndpoints(this IEndpointRouteBuilder app)
    {
        var progress = app.MapGroup("/api/osan/projects/{projectId:guid}/progress");

        progress.MapGet("", async (
            Guid projectId,
            OsanProgressStore store,
            OsanProjectStore projectStore,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = await AuthorizeProjectAsync(
                projectId,
                QmsPermissions.ProjectRead,
                projectStore,
                connectionStringProvider,
                user,
                cancellationToken);
            if (denied is not null)
            {
                return denied;
            }

            var value = await store.GetAsync(projectId, cancellationToken);
            return value is null ? Results.NotFound() : Results.Ok(value);
        })
        .RequireAuthorization()
        .WithName("GetOsanProgress");

        progress.MapPost("/start", async (
            Guid projectId,
            StartOsanProgressRequest request,
            OsanProgressStore store,
            OsanProjectStore projectStore,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = await AuthorizeProjectAsync(
                projectId,
                QmsPermissions.ManufacturingUpdate,
                projectStore,
                connectionStringProvider,
                user,
                cancellationToken);
            if (denied is not null)
            {
                return denied;
            }

            var actorId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorId is null)
            {
                return Results.Unauthorized();
            }

            return ToResult(await store.StartAsync(
                projectId,
                request,
                actorId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithName("StartOsanProgress");

        progress.MapPost("/completions", async (
            Guid projectId,
            HttpRequest request,
            OsanProgressStore store,
            OsanProjectStore projectStore,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = await AuthorizeProjectAsync(
                projectId,
                QmsPermissions.ManufacturingUpdate,
                projectStore,
                connectionStringProvider,
                user,
                cancellationToken);
            if (denied is not null)
            {
                return denied;
            }

            var actorId = ProjectEndpointExtensions.GetCurrentUserId(user);
            if (actorId is null)
            {
                return Results.Unauthorized();
            }

            var parsed = await ReadCompletionAsync(request, cancellationToken);
            if (parsed.Input is null)
            {
                return Results.ValidationProblem(parsed.Errors);
            }

            return ToResult(await store.CompleteAsync(
                projectId,
                parsed.Input,
                actorId.Value,
                cancellationToken));
        })
        .RequireAuthorization(QmsPolicies.ManufacturingUpdate)
        .WithMetadata(new RequestSizeLimitAttribute(OsanProgressPhotoValidator.MaximumMultipartBytes))
        .WithName("CompleteOsanProgress");

        progress.MapGet("/photos/{photoId:guid}", async (
            Guid projectId,
            Guid photoId,
            OsanProgressStore store,
            OsanProjectStore projectStore,
            DatabaseConnectionStringProvider connectionStringProvider,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var denied = await AuthorizeProjectAsync(
                projectId,
                QmsPermissions.ProjectRead,
                projectStore,
                connectionStringProvider,
                user,
                cancellationToken);
            if (denied is not null)
            {
                return denied;
            }

            var photo = await store.GetPhotoAsync(projectId, photoId, cancellationToken);
            return photo is null
                ? Results.NotFound()
                : Results.File(photo.Content, photo.ContentType);
        })
        .RequireAuthorization()
        .WithName("DownloadOsanProgressPhoto");

        return app;
    }

    private static async Task<IResult?> AuthorizeProjectAsync(
        Guid projectId,
        string permission,
        OsanProjectStore projectStore,
        DatabaseConnectionStringProvider connectionStringProvider,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                connectionStringProvider.GetCurrentBusinessUnit()?.Code,
                BusinessUnitCodes.Osan,
                StringComparison.Ordinal))
        {
            return Results.Json(
                new BusinessUnitAccessDeniedResponse(
                    "business_unit_capability_disabled",
                    "선택한 사업부에서 사용할 수 없는 기능입니다."),
                statusCode: StatusCodes.Status403Forbidden);
        }
        if (!ProjectEndpointExtensions.HasPermission(user, permission))
        {
            return Results.Forbid();
        }

        var access = await projectStore.GetAccessRecordAsync(projectId, cancellationToken);
        if (access is null)
        {
            return Results.NotFound();
        }
        if (!OsanProjectEndpointExtensions.CanAccessProject(user, access.ProjectKey))
        {
            return Results.Forbid();
        }
        return null;
    }

    private static async Task<(CompleteOsanProgressInput? Input, Dictionary<string, string[]> Errors)>
        ReadCompletionAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!request.HasFormContentType)
        {
            errors["request"] = ["multipart/form-data 요청이 필요합니다."];
            return (null, errors);
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is BadHttpRequestException or InvalidDataException or IOException)
        {
            errors["request"] = ["업로드 요청을 읽을 수 없습니다. 파일 형식과 크기를 확인해 주세요."];
            return (null, errors);
        }

        if (!Guid.TryParse(form["operationId"].ToString(), out var operationId))
        {
            errors["operationId"] = ["작업 식별자가 필요합니다."];
        }
        var completionMode = form["completionMode"].ToString().Trim();
        if (completionMode is not (OsanCompletionModes.Individual or OsanCompletionModes.Batch))
        {
            errors["completionMode"] = ["완료 방식은 individual 또는 batch여야 합니다."];
        }
        if (!int.TryParse(form["stageSequence"].ToString(), out var stageSequence)
            || stageSequence is < 1 or > 7)
        {
            errors["stageSequence"] = ["단계 번호는 1~7이어야 합니다."];
        }

        IReadOnlyList<OsanProgressTargetRequest?>? targets = null;
        try
        {
            targets = JsonSerializer.Deserialize<IReadOnlyList<OsanProgressTargetRequest?>>(
                form["targets"].ToString(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            errors["targets"] = ["대상 목록 형식이 올바르지 않습니다."];
        }
        if (targets is null)
        {
            errors.TryAdd("targets", ["대상 목록이 필요합니다."]);
        }

        if (form.Files.Count > OsanProgressPhotoValidator.MaximumPhotoCount)
        {
            errors["photos"] = ["사진은 최대 5장까지 첨부할 수 있습니다."];
        }
        if (form.Files.Sum(file => file.Length) > OsanProgressPhotoValidator.MaximumTotalBytes)
        {
            errors["photos"] = ["사진 전체 크기는 15MiB 이하여야 합니다."];
        }

        var photos = new List<OsanProgressPhotoInput>();
        if (!errors.ContainsKey("photos"))
        {
            foreach (var file in form.Files)
            {
                if (!string.Equals(file.Name, "photos", StringComparison.Ordinal))
                {
                    errors["photos"] = ["사진 파일 필드 이름은 photos여야 합니다."];
                    break;
                }
                if (file.Length is < 1 or > OsanProgressPhotoValidator.MaximumPhotoBytes)
                {
                    errors["photos"] = ["사진은 장당 5MiB 이하여야 합니다."];
                    break;
                }

                await using var input = file.OpenReadStream();
                using var buffer = new MemoryStream((int)file.Length);
                var chunk = new byte[81920];
                long totalRead = 0;
                while (true)
                {
                    var read = await input.ReadAsync(chunk, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }
                    totalRead += read;
                    if (totalRead > OsanProgressPhotoValidator.MaximumPhotoBytes)
                    {
                        errors["photos"] = ["사진은 장당 5MiB 이하여야 합니다."];
                        break;
                    }
                    buffer.Write(chunk, 0, read);
                }
                if (errors.ContainsKey("photos"))
                {
                    break;
                }
                var (photo, error) = await OsanProgressPhotoValidator.ValidateAsync(
                    file.FileName,
                    file.ContentType,
                    buffer.ToArray(),
                    cancellationToken);
                if (photo is null)
                {
                    errors["photos"] = [error ?? "사진을 확인할 수 없습니다."];
                    break;
                }
                photos.Add(photo);
            }
        }

        if (photos.Select(photo => photo.Sha256).Distinct(StringComparer.Ordinal).Count() != photos.Count)
        {
            errors["photos"] = ["같은 사진을 중복해서 첨부할 수 없습니다."];
        }
        if (errors.Count > 0 || targets is null)
        {
            return (null, errors);
        }

        return (new CompleteOsanProgressInput(
            operationId,
            completionMode,
            stageSequence,
            targets,
            photos), errors);
    }

    private static IResult ToResult(OsanProgressMutationResult result) =>
        result.Status switch
        {
            OsanProgressMutationStatus.Success when result.Value is not null => Results.Ok(result.Value),
            OsanProgressMutationStatus.Validation => Results.ValidationProblem(
                result.Errors ?? new Dictionary<string, string[]>()),
            OsanProgressMutationStatus.NotFound => Results.NotFound(),
            OsanProgressMutationStatus.Conflict => Results.Conflict(new OsanProjectErrorResponse(
                result.ErrorCode ?? "osan_progress_conflict",
                result.Message ?? "진행 상태가 변경되었습니다.")),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
}
