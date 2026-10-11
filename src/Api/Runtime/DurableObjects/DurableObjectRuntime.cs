namespace Workers;

public sealed class DurableObjectState
{
    private DurableObjectState() { }

    public DurableObjectId Id => WorkerApi.NotExecutable<DurableObjectId>();
    /// <summary>Loopback bindings to this Worker's own exports. Requires a compatibility date of 2025-11-17 or later,
    /// or the <c>enable_ctx_exports</c> flag.</summary>
    public Exports Exports => WorkerApi.NotExecutable<Exports>();
    public DurableObjectStorage Storage => WorkerApi.NotExecutable<DurableObjectStorage>();
    /// <summary>The object's container, or null when the class is not configured with one.</summary>
    public Container? Container => WorkerApi.NotExecutable<Container?>();
    public T Props<T>() => WorkerApi.NotExecutable<T>();

    public void WaitUntil(Task promise) => WorkerApi.NotExecutable();
    public Task BlockConcurrencyWhileAsync(Func<Task> callback) => WorkerApi.NotExecutable<Task>();
    public Task<T> BlockConcurrencyWhileAsync<T>(Func<Task<T>> callback) => WorkerApi.NotExecutable<Task<T>>();
    /// <summary>Resets the object, ending in-flight requests with the reason; the next request recreates it.</summary>
    public void Abort(string? reason = null, DurableObjectAbortOptions? options = null) => WorkerApi.NotExecutable();

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

    public Task<Date?> GetAlarmAsync(DurableObjectGetAlarmOptions? options = null) => WorkerApi.NotExecutable<Task<Date?>>();
    public Task SetAlarmAsync(Date scheduledTime, DurableObjectSetAlarmOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAlarmAsync(DurableObjectSetAlarmOptions? options = null) => WorkerApi.NotExecutable<Task>();

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
    public Task<Date?> GetAlarmAsync(DurableObjectGetAlarmOptions? options = null) => WorkerApi.NotExecutable<Task<Date?>>();
    public Task SetAlarmAsync(Date scheduledTime, DurableObjectSetAlarmOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAlarmAsync(DurableObjectSetAlarmOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public void Rollback() => WorkerApi.NotExecutable();
}

public sealed class DurableObjectGetAlarmOptions
{
    public bool? AllowConcurrency { get; init; }
}

public sealed class DurableObjectSetAlarmOptions
{
    public bool? AllowConcurrency { get; init; }
    public bool? AllowUnconfirmed { get; init; }
}

public sealed class DurableObjectAbortOptions
{
    /// <summary>When false, aborting during an alarm handler stops that alarm from being retried. Defaults to true.</summary>
    public bool? RetryAlarm { get; init; }
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
    /// <summary>The images the container can run, keyed by name.</summary>
    public IReadOnlyDictionary<string, string> Images => WorkerApi.NotExecutable<IReadOnlyDictionary<string, string>>();
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
    /// <summary>Snapshots the running container's filesystem; start from it with <see cref="ContainerStartupOptions.ContainerSnapshot"/>.</summary>
    public Task<ContainerSnapshot> SnapshotContainerAsync(ContainerSnapshotOptions? options = null) =>
        WorkerApi.NotExecutable<Task<ContainerSnapshot>>();
    /// <summary>The running container's image and labels, or null when it is not running.</summary>
    public Task<ContainerInfo?> InspectAsync() => WorkerApi.NotExecutable<Task<ContainerInfo?>>();
}

public sealed class ContainerStartupOptions
{
    public IReadOnlyList<string>? Entrypoint { get; init; }
    public bool EnableInternet { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    /// <summary>The image to run, by name from <see cref="Container.Images"/>. Cannot be combined with a snapshot.</summary>
    public string? Image { get; init; }
    /// <summary>An instance type such as <c>"standard-1"</c>, or custom <see cref="ContainerStartResources"/>.</summary>
    public ContainerInstanceType? Instance { get; init; }
    public IReadOnlyDictionary<string, string>? Labels { get; init; }
    /// <summary>Starts from a snapshot taken by <see cref="Container.SnapshotContainerAsync"/>. Cannot be combined with an image.</summary>
    public ContainerSnapshotRestoreParams? ContainerSnapshot { get; init; }
}

/// <summary>A container instance type: a name such as <c>lite</c> or <c>standard-1</c> to <c>standard-4</c>, or custom resources.</summary>
public sealed class ContainerInstanceType
{
    private ContainerInstanceType() { }

    public static implicit operator ContainerInstanceType(string name) => WorkerApi.NotExecutable<ContainerInstanceType>();
    public static implicit operator ContainerInstanceType(ContainerStartResources resources) => WorkerApi.NotExecutable<ContainerInstanceType>();
}

public sealed class ContainerStartResources
{
    public required double Vcpu { get; init; }
    /// <summary>Memory in MiB (2^20 bytes).</summary>
    public required double MemoryMib { get; init; }
    /// <summary>Disk in MB (10^6 bytes).</summary>
    public required double DiskMb { get; init; }
}

public sealed class ContainerSnapshotOptions
{
    public string? Name { get; init; }
}

public sealed class ContainerSnapshot
{
    private ContainerSnapshot() { }

    public string Id => WorkerApi.NotExecutable<string>();
    /// <summary>The snapshot size in bytes.</summary>
    public double Size => WorkerApi.NotExecutable<double>();
    public string? Name => WorkerApi.NotExecutable<string?>();
}

public sealed class ContainerSnapshotRestoreParams
{
    public required string Id { get; init; }
}

public sealed class ContainerInfo
{
    private ContainerInfo() { }

    public string Image => WorkerApi.NotExecutable<string>();
    public IReadOnlyDictionary<string, string> Labels => WorkerApi.NotExecutable<IReadOnlyDictionary<string, string>>();
}

public sealed class ContainerExecOptions
{
    public string? Cwd { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    public string? User { get; init; }
    public AbortSignal? Signal { get; init; }
    /// <summary>A stream piped to the process, or <see cref="ContainerStdio.Pipe"/> to write to <see cref="ExecProcess.Stdin"/>.</summary>
    public ContainerExecStdin? Stdin { get; init; }
    /// <summary><see cref="ContainerStdio.Pipe"/> (the default) or <see cref="ContainerStdio.Ignore"/>.</summary>
    public ContainerStdio? Stdout { get; init; }
    /// <summary><see cref="ContainerStdio.Pipe"/> (the default), <see cref="ContainerStdio.Ignore"/>, or
    /// <see cref="ContainerStdio.Combined"/> to merge it into stdout.</summary>
    public ContainerStdio? Stderr { get; init; }
    /// <summary>Allocates a terminal: <c>true</c> for the default size, or <see cref="ContainerExecPtyOptions"/>.</summary>
    public ContainerExecPty? Pty { get; init; }
}

public enum ContainerStdio
{
    Pipe,
    Ignore,
    Combined
}

/// <summary>A process's input: a stream, or <see cref="ContainerStdio.Pipe"/>.</summary>
public sealed class ContainerExecStdin
{
    private ContainerExecStdin() { }

    public static implicit operator ContainerExecStdin(ReadableStream stream) => WorkerApi.NotExecutable<ContainerExecStdin>();
    public static implicit operator ContainerExecStdin(ContainerStdio mode) => WorkerApi.NotExecutable<ContainerExecStdin>();
}

/// <summary>Whether to allocate a terminal: a boolean, or <see cref="ContainerExecPtyOptions"/> with its initial size.</summary>
public sealed class ContainerExecPty
{
    private ContainerExecPty() { }

    public static implicit operator ContainerExecPty(bool enabled) => WorkerApi.NotExecutable<ContainerExecPty>();
    public static implicit operator ContainerExecPty(ContainerExecPtyOptions options) => WorkerApi.NotExecutable<ContainerExecPty>();
}

public sealed class ContainerExecPtyOptions
{
    /// <summary>Columns; defaults to 80.</summary>
    public int? Cols { get; init; }
    /// <summary>Rows; defaults to 24.</summary>
    public int? Rows { get; init; }
}

public sealed class ExecProcess
{
    private ExecProcess() { }

    public int Pid => WorkerApi.NotExecutable<int>();
    /// <summary>The process's input when <see cref="ContainerExecOptions.Stdin"/> is <see cref="ContainerStdio.Pipe"/>, otherwise null.</summary>
    public WritableStream? Stdin => WorkerApi.NotExecutable<WritableStream?>();
    public ReadableStream? Stdout => WorkerApi.NotExecutable<ReadableStream?>();
    public ReadableStream? Stderr => WorkerApi.NotExecutable<ReadableStream?>();
    public bool IsPty => WorkerApi.NotExecutable<bool>();
    public Task<int> ExitCode => WorkerApi.NotExecutable<Task<int>>();
    public Task<ExecOutput> OutputAsync() => WorkerApi.NotExecutable<Task<ExecOutput>>();
    public void Kill(int? signal = null) => WorkerApi.NotExecutable();
    /// <summary>Resizes the terminal of a process started with <see cref="ContainerExecOptions.Pty"/>.</summary>
    public void Resize(int cols, int rows) => WorkerApi.NotExecutable();
}

public sealed class ExecOutput
{
    private ExecOutput() { }

    public byte[] Stdout => WorkerApi.NotExecutable<byte[]>();
    public byte[] Stderr => WorkerApi.NotExecutable<byte[]>();
    public int ExitCode => WorkerApi.NotExecutable<int>();
}
