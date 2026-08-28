namespace Emi.Qms.Api.Audit;

public sealed record AuditMutationContext(
    Guid ActorUserId,
    Guid? ActualActorUserId,
    Guid RequestCorrelationId,
    Guid? LoginCorrelationId,
    string Domain,
    string Action,
    string RouteKey);

public static class AuditRequestContext
{
    private static readonly AsyncLocal<AuditMutationContext?> CurrentContext = new();

    public static AuditMutationContext? Current => CurrentContext.Value;

    public static IDisposable Push(AuditMutationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new Scope(previous);
    }

    private sealed class Scope(AuditMutationContext? previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            CurrentContext.Value = previous;
            disposed = true;
        }
    }
}
