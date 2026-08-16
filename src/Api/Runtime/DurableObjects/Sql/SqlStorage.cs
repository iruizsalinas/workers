namespace Workers;

public sealed class DurableObjectSqlStorage
{
    public DurableObjectSqlStatement Prepare(string query) => WorkerApi.NotExecutable<DurableObjectSqlStatement>();
    public long DatabaseSize => WorkerApi.NotExecutable<long>();
    public DurableObjectSqlCursor<T> Exec<T>(string query, params object?[] values) =>
        WorkerApi.NotExecutable<DurableObjectSqlCursor<T>>();

    public Task<IReadOnlyList<DurableObjectSqlRawResult>> TransactionSyncRawAsync(
        IEnumerable<DurableObjectSqlStatement> statements, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyList<DurableObjectSqlRawResult>>>();
}

public sealed class DurableObjectSqlStatement
{
    public DurableObjectSqlStatement Bind(params object?[] values) => WorkerApi.NotExecutable<DurableObjectSqlStatement>();
    public Task<DurableObjectSqlResult<T>> AllAsync<T>(
        CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<DurableObjectSqlResult<T>>>();
    public Task<T> OneAsync<T>(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T>>();
    public Task<DurableObjectSqlRawResult> RawAsync(
        CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<DurableObjectSqlRawResult>>();
    public Task<DurableObjectSqlCursor<T>> OpenCursorAsync<T>(
        CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<DurableObjectSqlCursor<T>>>();
}

public sealed class DurableObjectSqlResult<T>
{
    public IReadOnlyList<T> Rows { get; init; } = [];
    public IReadOnlyList<string> ColumnNames { get; init; } = [];
    public long RowsRead { get; init; }
    public long RowsWritten { get; init; }
}

public sealed class DurableObjectSqlRawResult
{
    public IReadOnlyList<IReadOnlyList<JsonElement>> Rows { get; init; } = [];
    public IReadOnlyList<string> ColumnNames { get; init; } = [];
    public long RowsRead { get; init; }
    public long RowsWritten { get; init; }
}

public sealed class DurableObjectSqlCursor<T> : IAsyncDisposable
{
    public IReadOnlyList<string> ColumnNames => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public long RowsRead => WorkerApi.NotExecutable<long>();
    public long RowsWritten => WorkerApi.NotExecutable<long>();

    public Task<T?> NextAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<IAsyncEnumerable<T>>();
    public T One() => WorkerApi.NotExecutable<T>();
    public IReadOnlyList<T> ToArray() => WorkerApi.NotExecutable<IReadOnlyList<T>>();
    public ValueTask DisposeAsync() => WorkerApi.NotExecutable<ValueTask>();
}
