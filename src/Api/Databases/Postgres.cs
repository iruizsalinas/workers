namespace Workers;

/// <summary>
/// A PostgreSQL connection backed by node-postgres (<c>pg</c>). Install it with <c>npm install pg</c>
/// and enable the <c>nodejs_compat</c> compatibility flag. Works with Hyperdrive and with any
/// Postgres-compatible service reachable over TCP, such as Neon, Supabase, Xata or PlanetScale.
/// </summary>
public sealed class PostgresClient : IAsyncDisposable
{
    public static Task<PostgresClient> ConnectAsync(string connectionString, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<PostgresClient>>();

    public static Task<PostgresClient> ConnectAsync(IHyperdriveBinding hyperdrive, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<PostgresClient>>();

    public Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql, IEnumerable<object?>? parameters = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<T>>>();

    public Task<PostgresResult> QueryAsync(
        string sql, IEnumerable<object?>? parameters = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<PostgresResult>>();

    public ValueTask DisposeAsync() => WorkerApi.NotExecutable<ValueTask>();
}

public sealed class PostgresResult
{
    public string Command { get; init; } = "";

    public int? RowCount { get; init; }
}
