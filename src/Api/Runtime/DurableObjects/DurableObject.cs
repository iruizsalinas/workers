namespace Workers;

/// <summary>
/// A Durable Object class, exported under its class name. Public methods are RPC methods; override the
/// handlers the object receives. A constructor, if declared, takes no parameters and can read <see cref="Ctx"/> and <see cref="Env"/>.
/// </summary>
public abstract class DurableObject
{
    protected DurableObject() { }

    protected DurableObjectState Ctx => WorkerApi.NotExecutable<DurableObjectState>();
    protected Env Env => WorkerApi.NotExecutable<Env>();

    public virtual Task<Response> FetchAsync(Request request) => WorkerApi.NotExecutable<Task<Response>>();
    public virtual Task AlarmAsync(AlarmInvocationInfo alarmInfo) => WorkerApi.NotExecutable<Task>();
    public virtual Task WebSocketMessageAsync(WebSocket ws, WebSocketMessage message) => WorkerApi.NotExecutable<Task>();
    public virtual Task WebSocketCloseAsync(WebSocket ws, int code, string reason, bool wasClean) => WorkerApi.NotExecutable<Task>();
    public virtual Task WebSocketErrorAsync(WebSocket ws, JsonValue error) => WorkerApi.NotExecutable<Task>();
}

public sealed class AlarmInvocationInfo
{
    private AlarmInvocationInfo() { }

    public bool IsRetry => WorkerApi.NotExecutable<bool>();
    public int RetryCount => WorkerApi.NotExecutable<int>();
    public Date ScheduledTime => WorkerApi.NotExecutable<Date>();
}
