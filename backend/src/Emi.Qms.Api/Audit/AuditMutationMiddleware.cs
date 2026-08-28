using System.Security.Claims;
using Emi.Qms.Api.Authorization;
using Emi.Qms.Api.Workflow;

namespace Emi.Qms.Api.Audit;

public static class AuditHeaderNames
{
    public const string LoginCorrelation = "X-Qms-Audit-Correlation";
    public const string IdempotencyReceipt = "X-Qms-Audit-Receipt";
}

public sealed class AuditMutationMiddleware(
    RequestDelegate next,
    AuditStore auditStore,
    ILogger<AuditMutationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!AuditMutationRegistry.TryResolve(context, out var definition)
            || !definition.Included
            || !TryReadUserId(context.User, QmsClaimTypes.UserId, out var actorUserId))
        {
            await next(context);
            return;
        }

        var actualActorUserId = TryReadUserId(context.User, QmsClaimTypes.ActualUserId, out var parsedActualActorId)
            && parsedActualActorId != actorUserId
                ? parsedActualActorId
                : (Guid?)null;
        var sessionOwnerId = actualActorUserId ?? actorUserId;
        var loginCorrelationId = await ResolveLoginCorrelationAsync(context, sessionOwnerId);
        var mutationContext = new AuditMutationContext(
            actorUserId,
            actualActorUserId,
            Guid.NewGuid(),
            loginCorrelationId,
            definition.Domain,
            definition.Action,
            definition.RouteKey);

        using var scope = AuditRequestContext.Push(mutationContext);
        try
        {
            await next(context);
        }
        catch (DepartmentHeadRequiredException)
        {
            await auditStore.TryAppendFailedMutationAsync(
                actorUserId,
                actualActorUserId,
                definition,
                AuditFailureReasons.Conflict,
                loginCorrelationId,
                CancellationToken.None);
            throw;
        }

        var failureReason = ClassifyFailure(context.Response.StatusCode, definition);
        if (failureReason is not null)
        {
            await auditStore.TryAppendFailedMutationAsync(
                actorUserId,
                actualActorUserId,
                definition,
                failureReason,
                loginCorrelationId,
                CancellationToken.None);
        }
    }

    private async Task<Guid?> ResolveLoginCorrelationAsync(HttpContext context, Guid sessionOwnerId)
    {
        if (!Guid.TryParse(context.Request.Headers[AuditHeaderNames.LoginCorrelation], out var correlationId)
            || !Guid.TryParse(context.Request.Headers[AuditHeaderNames.IdempotencyReceipt], out var receipt))
        {
            return null;
        }

        try
        {
            return await auditStore.ResolveOwnedSessionAsync(
                sessionOwnerId,
                correlationId,
                receipt,
                context.RequestAborted)
                ? correlationId
                : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Audit login correlation validation failed.");
            return null;
        }
    }

    private static string? ClassifyFailure(int statusCode, AuditMutationDefinition definition) => statusCode switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => AuditFailureReasons.Validation,
        StatusCodes.Status409Conflict => definition.ConflictFailureReason,
        StatusCodes.Status412PreconditionFailed => AuditFailureReasons.Conflict,
        _ => null
    };

    private static bool TryReadUserId(ClaimsPrincipal user, string claimType, out Guid userId) =>
        Guid.TryParse(user.FindFirst(claimType)?.Value, out userId);
}
