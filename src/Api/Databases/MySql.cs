namespace Workers;

/// <summary>
/// A MySQL connection backed by <c>mysql2</c>. Install it with <c>npm install mysql2</c> and enable
/// the <c>nodejs_compat</c> compatibility flag. Works with Hyperdrive and with any MySQL-compatible
/// service reachable over TCP, such as PlanetScale or TiDB Cloud.
/// </summary>
public sealed class MySqlClient : IAsyncDisposable
{
    public static Task<MySqlClient> ConnectAsync(string connectionString, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MySqlClient>>();

    public static Task<MySqlClient> ConnectAsync(IHyperdriveBinding hyperdrive, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MySqlClient>>();

    public Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql, IEnumerable<object?>? parameters = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<T>>>();

    public Task<MySqlResult> QueryAsync(
        string sql, IEnumerable<object?>? parameters = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<MySqlResult>>();

    public ValueTask DisposeAsync() => WorkerApi.NotExecutable<ValueTask>();
}

public sealed class MySqlResult
{
    public int AffectedRows { get; init; }
    public long InsertId { get; init; }
}
