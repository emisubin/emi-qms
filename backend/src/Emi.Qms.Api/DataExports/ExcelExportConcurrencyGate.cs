namespace Emi.Qms.Api.DataExports;

public sealed class ExcelExportConcurrencyGate
{
    private readonly SemaphoreSlim semaphore = new(2, 2);

    public bool TryAcquire(out IDisposable lease)
    {
        if (!semaphore.Wait(0))
        {
            lease = NoopLease.Instance;
            return false;
        }

        lease = new Lease(semaphore);
        return true;
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }

    private sealed class NoopLease : IDisposable
    {
        public static NoopLease Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
