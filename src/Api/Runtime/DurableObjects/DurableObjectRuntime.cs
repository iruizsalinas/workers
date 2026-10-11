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
    public Date? GetWebSocketAutoResponseTimestamp(WebSocket socket) =>
        WorkerApi.NotExecutable<Date?>();
    public void SetHibernatableWebSocketEventTimeout(double? milliseconds = null) => WorkerApi.NotExecutable();
    public double? GetHibernatableWebSocketEventTimeout() => WorkerApi.NotExecutable<double?>();
}

public sealed class DurableObjectStorage
{
    public DurableObjectKvStorage Kv => WorkerApi.NotExecutable<DurableObjectKvStorage>();

    public Task<T?> GetAsync<T>(string key) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<IReadOnlyDictionary<string, T?>> GetAsync<T>(
        IEnumerable<string> keys) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T?>>>();
    public Task PutAsync<T>(string key, T value) => WorkerApi.NotExecutable<Task>();
    public Task PutAsync<T>(IReadOnlyDictionary<string, T> values) => WorkerApi.NotExecutable<Task>();
    public Task<bool> DeleteAsync(string key) => WorkerApi.NotExecutable<Task<bool>>();
    public Task DeleteAllAsync() => WorkerApi.NotExecutable<Task>();
    public Task SyncAsync() => WorkerApi.NotExecutable<Task>();
    public Task<IReadOnlyDictionary<string, T>> ListAsync<T>(
        DurableObjectStorageListOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public Task TransactionAsync(Func<DurableObjectTransaction, Task> callback) => WorkerApi.NotExecutable<Task>();
    public void TransactionSync(Action callback) => WorkerApi.NotExecutable();
    public Task<Date?> GetAlarmAsync() => WorkerApi.NotExecutable<Task<Date?>>();
    public Task SetAlarmAsync(Date scheduledTime) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAlarmAsync() => WorkerApi.NotExecutable<Task>();
    public Task<string?> GetCurrentBookmarkAsync() => WorkerApi.NotExecutable<Task<string?>>();
    public Task<string?> GetBookmarkForTimeAsync(
        Date time) =>
        WorkerApi.NotExecutable<Task<string?>>();
    public Task OnNextSessionRestoreBookmarkAsync(string bookmark) => WorkerApi.NotExecutable<Task>();
    public DurableObjectSqlStorage Sql => WorkerApi.NotExecutable<DurableObjectSqlStorage>();
}
