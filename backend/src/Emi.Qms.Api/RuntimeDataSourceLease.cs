using Npgsql;

namespace Emi.Qms.Api;

/// <summary>The provider owns shared pools; callers own only request-specific sources.</summary>
public sealed class RuntimeDataSourceLease(NpgsqlDataSource source, bool ownsSource) : IAsyncDisposable
{
    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        source.OpenConnectionAsync(cancellationToken);

    public NpgsqlCommand CreateCommand(string? commandText = null) => source.CreateCommand(commandText);

    public ValueTask DisposeAsync() => ownsSource ? source.DisposeAsync() : ValueTask.CompletedTask;
}
