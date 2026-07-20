using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Identity;
using Emi.Qms.Api.Projects;
using Emi.Qms.Api.Workflow;

namespace Emi.Qms.Api.PanelQr;

public static class PanelQrEndpointExtensions
{
    public static IEndpointRouteBuilder MapPanelQrEndpoints(this IEndpointRouteBuilder app)
    {
        var projectApi = app.MapGroup("/api/projects/{projectId:guid}");

        projectApi.MapGet("/qr", async (
            Guid projectId,
            ProjectStore projectStore,
            PanelQrStore qrStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null) return access;
            var response = await qrStore.ListProjectAsync(projectId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("ListProjectPanelQrs");

        projectApi.MapGet("/panels/{panelId:guid}/qr", async (
            Guid projectId,
            Guid panelId,
            ProjectStore projectStore,
            PanelQrStore qrStore,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null) return access;
            var response = await qrStore.GetActiveAsync(projectId, panelId, cancellationToken);
            return response is null ? Results.NoContent() : Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("GetPanelQr");

        projectApi.MapPost("/panels/{panelId:guid}/qr", async (
            Guid projectId,
            Guid panelId,
            ProjectStore projectStore,
            PanelQrStore qrStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null) return access;
            var actorUserId = CurrentUserId(user);
            if (actorUserId is null) return Results.Unauthorized();
            var result = await qrStore.IssueAsync(projectId, panelId, actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
            return ToMutationResult(result, Results.Ok);
        })
        .RequireAuthorization(QmsPolicies.PanelInfoUpdate)
        .WithName("IssuePanelQr");

        projectApi.MapPost("/panels/{panelId:guid}/qr/rotate", async (
            Guid projectId,
            Guid panelId,
            RotatePanelQrRequest request,
            ProjectStore projectStore,
            PanelQrStore qrStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!user.IsInRole(QmsRoles.SystemAdministrator)) return Results.Forbid();
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null) return access;
            var actorUserId = CurrentUserId(user);
            if (actorUserId is null) return Results.Unauthorized();
            var result = await qrStore.RotateAsync(projectId, panelId, request.Reason, actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
            return ToMutationResult(result, Results.Ok);
        })
        .RequireAuthorization()
        .WithName("RotatePanelQr");

        projectApi.MapGet("/panels/{panelId:guid}/qr/image", async (
            Guid projectId,
            Guid panelId,
            string? format,
            ProjectStore projectStore,
            PanelQrStore qrStore,
            PanelQrRenderer renderer,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null) return access;
            var actorUserId = CurrentUserId(user);
            if (actorUserId is null) return Results.Unauthorized();
            var snapshot = await qrStore.GetActiveSnapshotAsync(projectId, panelId, cancellationToken);
            if (snapshot is null) return Results.NotFound();

            var normalizedFormat = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase) ? "png" : "svg";
            var scanUrl = qrStore.BuildScanUrl(snapshot.Token);
            var bytes = normalizedFormat == "png" ? renderer.RenderPng(scanUrl) : renderer.RenderSvg(scanUrl);
            var contentType = normalizedFormat == "png" ? "image/png" : "image/svg+xml";
            var fileName = $"panel-qr-{SafeLabel(snapshot.PanelDisplayCode)}.{normalizedFormat}";
            await qrStore.RecordImageRenderedAsync(snapshot, actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
            httpContext.Response.Headers.CacheControl = "private, no-store";
            httpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(bytes, contentType, fileName);
        })
        .RequireAuthorization()
        .WithName("RenderPanelQrImage");

        projectApi.MapPost("/qr/print-sheet", async (
            Guid projectId,
            PanelQrPrintSheetRequest request,
            ProjectStore projectStore,
            PanelQrStore qrStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var access = await AuthorizeProjectReadAsync(projectStore, user, projectId, cancellationToken);
            if (access is not null) return access;
            var actorUserId = CurrentUserId(user);
            if (actorUserId is null) return Results.Unauthorized();
            var result = await qrStore.PreparePrintSheetAsync(projectId, request.PanelIds, actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
            if (result.Status != PanelQrMutationStatus.Success || result.Value is null)
            {
                return ToMutationResult(result, value => Results.Ok(value));
            }

            var response = new PanelQrPrintSheetResponse(
                projectId,
                result.Value.Count,
                result.Value.Select(snapshot => new PanelQrPrintSheetItemResponse(
                    snapshot.PanelId,
                    snapshot.PanelDisplayCode,
                    snapshot.PanelDisplayName,
                    $"/api/projects/{projectId}/panels/{snapshot.PanelId}/qr/image?format=svg")).ToList());
            return Results.Ok(response);
        })
        .RequireAuthorization()
        .WithName("PreparePanelQrPrintSheet");

        app.MapPost("/api/qr/resolve", async (
            PanelQrResolveRequest request,
            PanelQrStore qrStore,
            WorkflowStore workflowStore,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!HasPermission(user, QmsPermissions.ProjectRead)) return Results.Forbid();
            var actorUserId = CurrentUserId(user);
            if (actorUserId is null) return Results.Unauthorized();
            var snapshot = await qrStore.ResolveAsync(request.Token ?? string.Empty, cancellationToken);
            if (snapshot is null)
            {
                return Results.NotFound(new PanelQrResolveResponse("NotFound", "QR을 확인할 수 없습니다. 새 QR인지 확인해 주세요."));
            }

            if (!CanAccessProject(user, snapshot.ProjectKey)) return Results.Forbid();

            if (snapshot.ProjectDeleted)
            {
                var canReadDeleted = HasPermission(user, QmsPermissions.ProjectDeletedRead);
                await qrStore.RecordResolveAsync(snapshot, "ProjectDeleted", actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
                return canReadDeleted
                    ? Results.Ok(new PanelQrResolveResponse("ProjectDeleted", "삭제 보관함으로 이동한 프로젝트의 QR입니다. 삭제 프로젝트 조회 화면에서 확인해 주세요."))
                    : Results.NotFound(new PanelQrResolveResponse("NotFound", "QR을 확인할 수 없습니다. 새 QR인지 확인해 주세요."));
            }

            if (snapshot.Status == "Revoked")
            {
                await qrStore.RecordResolveAsync(snapshot, "Revoked", actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
                return Results.Json(
                    new PanelQrResolveResponse("Revoked", "더 이상 사용할 수 없는 QR입니다. 패널의 최신 QR을 확인해 주세요."),
                    statusCode: StatusCodes.Status410Gone);
            }

            var workflow = await workflowStore.GetProjectWorkflowAsync(snapshot.ProjectId, cancellationToken);
            if (workflow is null)
            {
                return Results.NotFound(new PanelQrResolveResponse("NotFound", "QR을 확인할 수 없습니다. 새 QR인지 확인해 주세요."));
            }

            var inactive = snapshot.PanelStatus != "Active" || snapshot.ProjectStatus is "OnHold" or "Cancelled";
            var completed = snapshot.ProjectStatus == "Completed";
            var status = inactive ? "PanelInactiveOrProjectHold" : completed ? "OkCompletedProject" : "Ok";
            var userDepartment = await qrStore.GetUserDepartmentCodeAsync(actorUserId.Value, cancellationToken);
            var requiredPermission = PermissionForStage(workflow.CurrentStageCode);
            var canEdit = status == "Ok"
                && requiredPermission is not null
                && string.Equals(userDepartment, workflow.CurrentDepartmentCode, StringComparison.Ordinal)
                && HasPermission(user, requiredPermission);
            var overviewPath = $"/projects/{snapshot.ProjectId}/panels/{snapshot.PanelId}";
            var actionPath = canEdit
                ? PathForStage(workflow.CurrentStageCode, snapshot.ProjectId, snapshot.PanelId, snapshot.ProjectCode)
                : overviewPath;
            var message = inactive
                ? "현재 보류·취소 또는 비활성 상태입니다. 입력은 할 수 없으며 종합현황만 확인할 수 있습니다."
                : completed
                    ? "완료된 프로젝트입니다. 패널 종합현황을 조회할 수 있습니다."
                    : "패널과 현재 업무를 확인했습니다.";

            await qrStore.RecordResolveAsync(snapshot, status, actorUserId.Value, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(new PanelQrResolveResponse(
                status,
                message,
                snapshot.ProjectId,
                snapshot.PanelId,
                snapshot.ProjectCode,
                snapshot.ProjectTitle,
                snapshot.PanelDisplayName,
                workflow.CurrentStageCode,
                workflow.CurrentStageName,
                workflow.CurrentDepartmentCode,
                workflow.CurrentDepartmentLabel,
                canEdit,
                canEdit ? "현재 업무 열기" : "패널 종합현황 보기",
                actionPath,
                overviewPath));
        })
        .RequireAuthorization()
        .WithName("ResolvePanelQr");

        return app;
    }

    private static string? PermissionForStage(string stageCode) => stageCode switch
    {
        WorkflowStageCodes.SalesProjectCreated => QmsPermissions.ProjectUpdate,
        WorkflowStageCodes.ProductionPlanning => QmsPermissions.ProductionPlanUpdate,
        WorkflowStageCodes.DesignPanelInfo => QmsPermissions.PanelInfoUpdate,
        WorkflowStageCodes.ProcurementInfo => QmsPermissions.ProcurementPlanUpdate,
        WorkflowStageCodes.MaterialArrived or WorkflowStageCodes.ReceiptConfirmed or WorkflowStageCodes.KittingCompleted => QmsPermissions.MaterialReceiptUpdate,
        WorkflowStageCodes.ManufacturingWork or WorkflowStageCodes.ManufacturingCompleted => QmsPermissions.ManufacturingUpdate,
        WorkflowStageCodes.IQC or WorkflowStageCodes.LQC or WorkflowStageCodes.OQC or WorkflowStageCodes.CustomerInspection or WorkflowStageCodes.FAT => QmsPermissions.QualityInspect,
        WorkflowStageCodes.PackingCompleted or WorkflowStageCodes.DepartureProcessed or WorkflowStageCodes.DeliveryCompleted => QmsPermissions.LogisticsShip,
        WorkflowStageCodes.SalesSettlementCompleted => QmsPermissions.SalesSettle,
        _ => null
    };

    private static string PathForStage(string stageCode, Guid projectId, Guid panelId, string projectCode) => stageCode switch
    {
        WorkflowStageCodes.SalesProjectCreated => $"/projects/{projectId}",
        WorkflowStageCodes.ProductionPlanning => $"/projects/{projectId}/production-planning/edit",
        WorkflowStageCodes.DesignPanelInfo => $"/projects/{projectId}/panel-information/edit",
        WorkflowStageCodes.ProcurementInfo => $"/projects/{projectId}/procurement/edit",
        WorkflowStageCodes.MaterialArrived or WorkflowStageCodes.ReceiptConfirmed => $"/materials/receipts?project={Uri.EscapeDataString(projectCode)}&panel={panelId}",
        WorkflowStageCodes.KittingCompleted => $"/materials/kitting?project={projectId}&panel={panelId}",
        WorkflowStageCodes.ManufacturingWork or WorkflowStageCodes.ManufacturingCompleted => $"/manufacturing/work?project={projectId}&panel={panelId}",
        WorkflowStageCodes.IQC => $"/quality/iqc?project={projectId}&panel={panelId}",
        WorkflowStageCodes.LQC => $"/quality/inspections?stage=LQC&project={projectId}&panel={panelId}",
        WorkflowStageCodes.OQC => $"/quality/inspections?stage=OQC&project={projectId}&panel={panelId}",
        WorkflowStageCodes.CustomerInspection => $"/quality/inspections?stage=CustomerInspection&project={projectId}&panel={panelId}",
        WorkflowStageCodes.FAT => $"/quality/inspections?stage=FAT&project={projectId}&panel={panelId}",
        WorkflowStageCodes.PackingCompleted => $"/logistics?stage=packing&project={projectId}&panel={panelId}",
        WorkflowStageCodes.DepartureProcessed => $"/logistics?stage=departure&project={projectId}&panel={panelId}",
        WorkflowStageCodes.DeliveryCompleted => $"/logistics?stage=delivery&project={projectId}&panel={panelId}",
        WorkflowStageCodes.SalesSettlementCompleted => $"/projects/{projectId}/settlement",
        _ => $"/projects/{projectId}/panels/{panelId}"
    };

    private static async Task<IResult?> AuthorizeProjectReadAsync(ProjectStore projectStore, ClaimsPrincipal user, Guid projectId, CancellationToken cancellationToken)
    {
        if (!HasPermission(user, QmsPermissions.ProjectRead)) return Results.Forbid();
        var record = await projectStore.GetProjectAccessRecordAsync(projectId, cancellationToken);
        if (record is null) return Results.NotFound();
        return CanAccessProject(user, record.ProjectKey) ? null : Results.Forbid();
    }

    private static bool CanAccessProject(ClaimsPrincipal user, string projectKey)
        => HasPermission(user, QmsPermissions.ProjectReadAll)
            || user.FindAll(QmsClaimTypes.Project).Any(claim => string.Equals(claim.Value, projectKey, StringComparison.Ordinal));

    private static bool HasPermission(ClaimsPrincipal user, string permission)
        => user.Identity?.IsAuthenticated == true && user.HasClaim(QmsClaimTypes.Permission, permission);

    private static Guid? CurrentUserId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirst(QmsClaimTypes.UserId)?.Value, out var value) ? value : null;

    private static string SafeLabel(string value)
    {
        var safe = new string(value.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').Take(48).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "panel" : safe;
    }

    private static IResult ToMutationResult<T>(PanelQrMutationResult<T> result, Func<T, IResult> success)
        => result.Status switch
        {
            PanelQrMutationStatus.Success when result.Value is not null => success(result.Value),
            PanelQrMutationStatus.NotFound => Results.NotFound(),
            PanelQrMutationStatus.Forbidden => Results.Forbid(),
            PanelQrMutationStatus.ValidationFailed => Results.ValidationProblem(result.Errors ?? new Dictionary<string, string[]>()),
            PanelQrMutationStatus.Conflict => Results.Problem(title: result.Message ?? "QR 요청을 처리할 수 없습니다.", statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
}
