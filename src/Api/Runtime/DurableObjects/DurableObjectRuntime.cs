namespace Workers;

public sealed record AlarmInfo(int RetryCount, bool IsRetry);
public sealed class DurableObjectState
{
    public DurableObjectId Id => WorkerApi.NotExecutable<DurableObjectId>();
    public DurableObjectStorage Storage => WorkerApi.NotExecutable<DurableObjectStorage>();
    public DurableObjectContainer Container => WorkerApi.NotExecutable<DurableObjectContainer>();

    public void WaitUntil(Task task) => WorkerApi.NotExecutable();
    public Task BlockConcurrencyWhileAsync(Func<Task> callback) => WorkerApi.NotExecutable<Task>();
    public void Abort(string? reason = null) => WorkerApi.NotExecutable();
    public void AcceptWebSocket(WebSocket socket, IEnumerable<string>? tags = null) => WorkerApi.NotExecutable();
    public IReadOnlyList<WebSocket> GetWebSockets(string? tag = null) =>
        WorkerApi.NotExecutable<IReadOnlyList<WebSocket>>();
    public IReadOnlyList<string> GetTags(WebSocket socket) =>
        WorkerApi.NotExecutable<IReadOnlyList<string>>();
    public void SetWebSocketAutoResponse(WebSocketAutoResponse? pair) => WorkerApi.NotExecutable();
    public WebSocketAutoResponse? GetWebSocketAutoResponse() =>
        WorkerApi.NotExecutable<WebSocketAutoResponse?>();
    public DateTimeOffset? GetWebSocketAutoResponseTimestamp(WebSocket socket) =>
        WorkerApi.NotExecutable<DateTimeOffset?>();
    public void SetHibernatableWebSocketEventTimeout(TimeSpan? timeout = null) => WorkerApi.NotExecutable();
    public TimeSpan? GetHibernatableWebSocketEventTimeout() => WorkerApi.NotExecutable<TimeSpan?>();
}

public sealed class DurableObjectStorage
{
    public DurableObjectKvStorage Kv => WorkerApi.NotExecutable<DurableObjectKvStorage>();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<IReadOnlyDictionary<string, T?>> GetAsync<T>(
        IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T?>>>();
    public Task PutAsync<T>(string key, T value, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task PutAsync<T>(IReadOnlyDictionary<string, T> values, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<bool>>();
    public Task DeleteAllAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task SyncAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<IReadOnlyDictionary<string, T>> ListAsync<T>(
        DurableObjectStorageListOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public Task TransactionAsync(Func<DurableObjectTransaction, Task> callback) => WorkerApi.NotExecutable<Task>();
    public void TransactionSync(Action callback) => WorkerApi.NotExecutable();
    public Task<DateTimeOffset?> GetAlarmAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<DateTimeOffset?>>();
    public Task SetAlarmAsync(DateTimeOffset scheduledTime, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAlarmAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<string?> GetCurrentBookmarkAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<string?>>();
    public Task<string?> GetBookmarkForTimeAsync(
        DateTimeOffset time, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<string?>>();
    public Task OnNextSessionRestoreBookmarkAsync(string bookmark, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public DurableObjectSqlStorage Sql => WorkerApi.NotExecutable<DurableObjectSqlStorage>();
}
