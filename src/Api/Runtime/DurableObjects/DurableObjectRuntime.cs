namespace Workers;

public sealed class DurableObjectState
{
    private DurableObjectState() { }

    public DurableObjectId Id => WorkerApi.NotExecutable<DurableObjectId>();
    public DurableObjectStorage Storage => WorkerApi.NotExecutable<DurableObjectStorage>();
    /// <summary>The object's container, or null when the class is not configured with one.</summary>
    public Container? Container => WorkerApi.NotExecutable<Container?>();
    public T Props<T>() => WorkerApi.NotExecutable<T>();

    public void WaitUntil(Task promise) => WorkerApi.NotExecutable();
    public Task BlockConcurrencyWhileAsync(Func<Task> callback) => WorkerApi.NotExecutable<Task>();
    public Task<T> BlockConcurrencyWhileAsync<T>(Func<Task<T>> callback) => WorkerApi.NotExecutable<Task<T>>();
    public void Abort(string? reason = null) => WorkerApi.NotExecutable();

    public void AcceptWebSocket(WebSocket ws, IEnumerable<string>? tags = null) => WorkerApi.NotExecutable();
    public IReadOnlyList<WebSocket> GetWebSockets(string? tag = null) => WorkerApi.NotExecutable<IReadOnlyList<WebSocket>>();
    public IReadOnlyList<string> GetTags(WebSocket ws) => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public void SetWebSocketAutoResponse(WebSocketRequestResponsePair? pair = null) => WorkerApi.NotExecutable();
    public WebSocketRequestResponsePair? GetWebSocketAutoResponse() => WorkerApi.NotExecutable<WebSocketRequestResponsePair?>();
    public Date? GetWebSocketAutoResponseTimestamp(WebSocket ws) => WorkerApi.NotExecutable<Date?>();
    public void SetHibernatableWebSocketEventTimeout(double? timeoutMs = null) => WorkerApi.NotExecutable();
    public double? GetHibernatableWebSocketEventTimeout() => WorkerApi.NotExecutable<double?>();
}

/// <summary>Transactional key-value storage. Values are structured clones; typed reads map them to C# classes and records.</summary>
public sealed class DurableObjectStorage
{
    private DurableObjectStorage() { }

    public SqlStorage Sql => WorkerApi.NotExecutable<SqlStorage>();
    public SyncKvStorage Kv => WorkerApi.NotExecutable<SyncKvStorage>();

    public Task<T?> GetAsync<T>(string key, DurableObjectGetOptions? options = null) => WorkerApi.NotExecutable<Task<T?>>();
    /// <summary>Reads several keys; keys that do not exist are absent from the result.</summary>
    public Task<IReadOnlyDictionary<string, T>> GetAsync<T>(IEnumerable<string> keys, DurableObjectGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public Task<IReadOnlyDictionary<string, T>> ListAsync<T>(DurableObjectListOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public Task PutAsync<T>(string key, T value, DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task PutAsync<T>(IReadOnlyDictionary<string, T> entries, DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task<bool> DeleteAsync(string key, DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task<bool>>();
    /// <summary>Deletes several keys and returns how many existed.</summary>
    public Task<int> DeleteAsync(IEnumerable<string> keys, DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task<int>>();
    public Task DeleteAllAsync(DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task TransactionAsync(Func<DurableObjectTransaction, Task> closure) => WorkerApi.NotExecutable<Task>();
    public Task<T> TransactionAsync<T>(Func<DurableObjectTransaction, Task<T>> closure) => WorkerApi.NotExecutable<Task<T>>();
    public void TransactionSync(Action closure) => WorkerApi.NotExecutable();
    public T TransactionSync<T>(Func<T> closure) => WorkerApi.NotExecutable<T>();
    public Task SyncAsync() => WorkerApi.NotExecutable<Task>();

    public Task<Date?> GetAlarmAsync() => WorkerApi.NotExecutable<Task<Date?>>();
    public Task SetAlarmAsync(Date scheduledTime) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAlarmAsync() => WorkerApi.NotExecutable<Task>();

    public Task<string> GetCurrentBookmarkAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<string> GetBookmarkForTimeAsync(Date timestamp) => WorkerApi.NotExecutable<Task<string>>();
    public Task<string> OnNextSessionRestoreBookmarkAsync(string bookmark) => WorkerApi.NotExecutable<Task<string>>();
}

public sealed class DurableObjectTransaction
{
    private DurableObjectTransaction() { }

    public Task<T?> GetAsync<T>(string key, DurableObjectGetOptions? options = null) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<IReadOnlyDictionary<string, T>> GetAsync<T>(IEnumerable<string> keys, DurableObjectGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public Task<IReadOnlyDictionary<string, T>> ListAsync<T>(DurableObjectListOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public Task PutAsync<T>(string key, T value, DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task<bool> DeleteAsync(string key, DurableObjectPutOptions? options = null) => WorkerApi.NotExecutable<Task<bool>>();
    public void Rollback() => WorkerApi.NotExecutable();
}

public sealed class DurableObjectGetOptions
{
    public bool? AllowConcurrency { get; init; }
    public bool? NoCache { get; init; }
}

public sealed class DurableObjectPutOptions
{
    public bool? AllowConcurrency { get; init; }
    public bool? AllowUnconfirmed { get; init; }
    public bool? NoCache { get; init; }
}

public sealed class DurableObjectListOptions
{
    public string? Start { get; init; }
    public string? StartAfter { get; init; }
    public string? End { get; init; }
    public string? Prefix { get; init; }
    public bool? Reverse { get; init; }
    public int? Limit { get; init; }
    public bool? AllowConcurrency { get; init; }
    public bool? NoCache { get; init; }
}

/// <summary>Synchronous key-value storage backed by the object's SQLite database.</summary>
public sealed class SyncKvStorage
{
    private SyncKvStorage() { }

    public T? Get<T>(string key) => WorkerApi.NotExecutable<T?>();
    public void Put<T>(string key, T value) => WorkerApi.NotExecutable();
    public bool Delete(string key) => WorkerApi.NotExecutable<bool>();
    public IReadOnlyDictionary<string, T> List<T>(SyncKvListOptions? options = null) => WorkerApi.NotExecutable<IReadOnlyDictionary<string, T>>();
}

public sealed class SyncKvListOptions
{
    public string? Start { get; init; }
    public string? StartAfter { get; init; }
    public string? End { get; init; }
    public string? Prefix { get; init; }
    public bool? Reverse { get; init; }
    public int? Limit { get; init; }
}

/// <summary>SQLite storage for a Durable Object. Queries run synchronously when <see cref="Exec{T}"/> is called.</summary>
public sealed class SqlStorage
{
    private SqlStorage() { }

    public long DatabaseSize => WorkerApi.NotExecutable<long>();
    public SqlStorageCursor<T> Exec<T>(string query, params object?[] bindings) => WorkerApi.NotExecutable<SqlStorageCursor<T>>();
}

/// <summary>Rows returned by <c>sql.exec()</c>. Typed reads map columns to C# classes and records.</summary>
public sealed class SqlStorageCursor<T>
{
    private SqlStorageCursor() { }

    public IReadOnlyList<string> ColumnNames => WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public long RowsRead => WorkerApi.NotExecutable<long>();
    public long RowsWritten => WorkerApi.NotExecutable<long>();

    /// <summary>The next row, or null when the cursor is exhausted.</summary>
    public T? Next() => WorkerApi.NotExecutable<T?>();
    /// <summary>The only row; throws unless the query returned exactly one.</summary>
    public T One() => WorkerApi.NotExecutable<T>();
    public IReadOnlyList<T> ToArray() => WorkerApi.NotExecutable<IReadOnlyList<T>>();
    public IReadOnlyList<IReadOnlyList<JsonValue>> Raw() => WorkerApi.NotExecutable<IReadOnlyList<IReadOnlyList<JsonValue>>>();
}

public sealed class Container
{
    private Container() { }

    public bool Running => WorkerApi.NotExecutable<bool>();
    public void Start(ContainerStartupOptions? options = null) => WorkerApi.NotExecutable();
    public Task MonitorAsync() => WorkerApi.NotExecutable<Task>();
    public Task DestroyAsync(string? error = null) => WorkerApi.NotExecutable<Task>();
    public void Signal(int signo) => WorkerApi.NotExecutable();
    public Fetcher GetTcpPort(int port) => WorkerApi.NotExecutable<Fetcher>();
    public Task InterceptOutboundHttpAsync(string addr, Fetcher binding) => WorkerApi.NotExecutable<Task>();
    public Task InterceptAllOutboundHttpAsync(Fetcher binding) => WorkerApi.NotExecutable<Task>();
    public Task InterceptOutboundHttpsAsync(string addr, Fetcher binding) => WorkerApi.NotExecutable<Task>();
    public Task SetInactivityTimeoutAsync(double durationMs) => WorkerApi.NotExecutable<Task>();
    public Task<ExecProcess> ExecAsync(IEnumerable<string> cmd, ContainerExecOptions? options = null) => WorkerApi.NotExecutable<Task<ExecProcess>>();
}

public sealed class ContainerStartupOptions
{
    public IReadOnlyList<string>? Entrypoint { get; init; }
    public bool EnableInternet { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    public IReadOnlyDictionary<string, string>? Labels { get; init; }
}

public sealed class ContainerExecOptions
{
    public string? Cwd { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    public string? User { get; init; }
    public AbortSignal? Signal { get; init; }
    /// <summary>Input piped to the process.</summary>
    public ReadableStream? Stdin { get; init; }
}

public sealed class ExecProcess
{
    private ExecProcess() { }

    public int Pid => WorkerApi.NotExecutable<int>();
    public ReadableStream? Stdout => WorkerApi.NotExecutable<ReadableStream?>();
    public ReadableStream? Stderr => WorkerApi.NotExecutable<ReadableStream?>();
    public Task<int> ExitCode => WorkerApi.NotExecutable<Task<int>>();
    public Task<ExecOutput> OutputAsync() => WorkerApi.NotExecutable<Task<ExecOutput>>();
    public void Kill(int? signal = null) => WorkerApi.NotExecutable();
}

public sealed class ExecOutput
{
    private ExecOutput() { }

    public byte[] Stdout => WorkerApi.NotExecutable<byte[]>();
    public byte[] Stderr => WorkerApi.NotExecutable<byte[]>();
    public int ExitCode => WorkerApi.NotExecutable<int>();
}
