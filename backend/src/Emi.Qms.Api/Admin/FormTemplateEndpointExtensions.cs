using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.DataExports;
using Emi.Qms.Api.Identity;

namespace Emi.Qms.Api.Admin;

public static class FormTemplateEndpointExtensions
{
    public static IEndpointRouteBuilder MapFormTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/form-templates").RequireAuthorization("AuthenticatedIdentity");
        group.MapGet("/my-scope", async (FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.GetScopeAsync(UserId(user), IsAdmin(user), token))).WithName("GetFormTemplateScope");
        group.MapGet("", async (FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.GetCatalogAsync(UserId(user), IsAdmin(user), token))).WithName("GetFormTemplates");
        group.MapGet("/{family}/{templateKey}/current", async (string family, string templateKey, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.GetCurrentAsync(family, templateKey, UserId(user), IsAdmin(user), token))).WithName("GetCurrentFormTemplate");
        group.MapPut("/{family}/{templateKey}/current", async (string family, string templateKey, SaveFormTemplateItemsRequest request, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.SaveCurrentAsync(family, templateKey, request, UserId(user), IsAdmin(user), token))).WithName("SaveCurrentFormTemplate");
        group.MapGet("/{family}/{templateKey}/versions", async (string family, string templateKey, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.GetVersionsAsync(family, templateKey, UserId(user), IsAdmin(user), token))).WithName("GetFormTemplateVersions");
        group.MapPost("/{family}/{templateKey}/versions", async (string family, string templateKey, CreateFormTemplateDraftRequest request, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.CreateDraftAsync(family, templateKey, request, UserId(user), IsAdmin(user), token))).WithName("CreateFormTemplateDraft");
        group.MapPut("/{family}/{templateKey}/versions/{versionId:guid}/items", async (string family, string templateKey, Guid versionId, SaveFormTemplateItemsRequest request, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.SaveItemsAsync(family, templateKey, versionId, request, UserId(user), IsAdmin(user), token))).WithName("SaveFormTemplateItems");
        group.MapPost("/{family}/{templateKey}/versions/{versionId:guid}/activate", async (string family, string templateKey, Guid versionId, TransitionFormTemplateVersionRequest request, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.ActivateAsync(family, templateKey, versionId, request, UserId(user), IsAdmin(user), token))).WithName("ActivateFormTemplateVersion");
        group.MapPost("/{family}/{templateKey}/versions/{versionId:guid}/cancel", async (string family, string templateKey, Guid versionId, TransitionFormTemplateVersionRequest request, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.CancelAsync(family, templateKey, versionId, request, UserId(user), IsAdmin(user), token))).WithName("CancelFormTemplateDraft");
        group.MapGet("/managers", async (FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.GetManagersAsync(IsAdmin(user), token))).WithName("GetFormTemplateManagers");
        group.MapPost("/managers", async (AssignFormTemplateManagerRequest request, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.AssignManagerAsync(request, UserId(user), IsAdmin(user), token))).WithName("AssignFormTemplateManager");
        group.MapPost("/managers/{bindingId:guid}/revoke", async (Guid bindingId, FormTemplateStore store, ClaimsPrincipal user, CancellationToken token) => await Safe(() => store.RevokeManagerAsync(bindingId, UserId(user), IsAdmin(user), token))).WithName("RevokeFormTemplateManager");
        group.MapGet("/material-categories", async (
            bool? includeInactive,
            MaterialCategoryStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
            await Safe(() => store.ListAsync(UserId(user), IsAdmin(user), includeInactive == true, token)))
            .WithName("ListMaterialCategories");
        group.MapPost("/material-categories", async (
            CreateMaterialCategoryRequest request,
            MaterialCategoryStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
            await Safe(() => store.CreateAsync(request, UserId(user), IsAdmin(user), token)))
            .WithName("CreateMaterialCategory");
        group.MapPut("/material-categories/{categoryId:guid}", async (
            Guid categoryId,
            UpdateMaterialCategoryRequest request,
            MaterialCategoryStore store,
            ClaimsPrincipal user,
            CancellationToken token) =>
            await Safe(() => store.UpdateAsync(categoryId, request, UserId(user), IsAdmin(user), token)))
            .WithName("UpdateMaterialCategory");
        group.MapPost("/export", async (ExportFormTemplateVersionsRequest request, FormTemplateStore store, ExcelWorkbookBuilder workbookBuilder, ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
        {
            if (request.VersionIds.Count is < 1 or > 1000 || request.VersionIds.Distinct().Count() != request.VersionIds.Count)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["versionIds"] = ["중복되지 않은 버전을 한 건 이상 선택해 주세요."] });
            try
            {
                var actor = UserId(user);
                var response = await store.GetVersionsAsync(request.Family, request.TemplateKey, actor, IsAdmin(user), token);
                var byId = response.Versions.ToDictionary(item => item.VersionId);
                if (request.VersionIds.Any(id => !byId.ContainsKey(id)))
                    return Results.Problem(title: "목록을 새로고침한 뒤 다시 선택해 주세요.", statusCode: StatusCodes.Status422UnprocessableEntity);
                var rows = request.VersionIds.Select(id => byId[id]).ToArray();
                var content = workbookBuilder.Build(
                    $"{response.DisplayName} 버전",
                    "양식버전",
                    $"선택 {rows.Length}건",
                    rows,
                    new ExcelColumn<FormTemplateVersionResponse>[]
                    {
                        new("버전", row => row.VersionNumber),
                        new("이름", row => row.DisplayName),
                        new("상태", row => row.LifecycleStatus),
                        new("항목 수", row => row.Items.Count),
                        new("활성일시", row => row.ActivatedAtUtc),
                        new("보관일시", row => row.ArchivedAtUtc)
                    });
                await store.RecordExportAsync(request.Family, request.TemplateKey, actor, IsAdmin(user), rows.Length, token);
                context.Response.Headers["X-Export-Row-Count"] = rows.Length.ToString();
                return Results.File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"EMI_{response.DisplayName}_선택.xlsx");
            }
            catch (FormTemplateForbiddenException) { return Results.Forbid(); }
            catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] }); }
        }).WithName("ExportFormTemplateVersions");
        return app;
    }
    private static async Task<IResult> Safe<T>(Func<Task<T>> action) { try { return Results.Ok(await action()); } catch (FormTemplateForbiddenException) { return Results.Forbid(); } catch (FormTemplateConflictException exception) { return Results.Problem(title: exception.Message, statusCode: 409); } catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] }); } }
    private static Guid UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : throw new FormTemplateForbiddenException();
    private static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(QmsRoles.SystemAdministrator);
}
