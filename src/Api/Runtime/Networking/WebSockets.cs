namespace Workers;

public sealed class WebSocketPair
{
    public static WebSocketPair Create() => WorkerApi.NotExecutable<WebSocketPair>();

    public WebSocket Client => WorkerApi.NotExecutable<WebSocket>();
    public WebSocket Server => WorkerApi.NotExecutable<WebSocket>();
}

public sealed class WebSocket
{
    public string? Url => WorkerApi.NotExecutable<string?>();
    public string? Protocol => WorkerApi.NotExecutable<string?>();

    public void Accept() => WorkerApi.NotExecutable();
    public void SendText(string message) => WorkerApi.NotExecutable();
    public void SendBytes(ReadOnlyMemory<byte> message) => WorkerApi.NotExecutable();
    public void Close(ushort? code = null, string? reason = null) => WorkerApi.NotExecutable();
    public Task<WebSocketEvent?> ReceiveAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<WebSocketEvent?>>();
    public WebSocketEventStream Events() => WorkerApi.NotExecutable<WebSocketEventStream>();
}

public sealed class WebSocketMessage
{
    public string? Text => WorkerApi.NotExecutable<string?>();
    public ReadOnlyMemory<byte> Bytes => WorkerApi.NotExecutable<ReadOnlyMemory<byte>>();
    public bool IsText => WorkerApi.NotExecutable<bool>();
    public bool IsBinary => WorkerApi.NotExecutable<bool>();

    public T? Json<T>() => WorkerApi.NotExecutable<T?>();
}

public sealed record WebSocketError(string Message);
public enum WebSocketEventKind
{
    Message,
    Close,
    Error
}

public sealed class WebSocketEvent
{
    public WebSocketEventKind Kind => WorkerApi.NotExecutable<WebSocketEventKind>();
    public string? Text => WorkerApi.NotExecutable<string?>();
    public ReadOnlyMemory<byte> Bytes => WorkerApi.NotExecutable<ReadOnlyMemory<byte>>();
    public ushort? CloseCode => WorkerApi.NotExecutable<ushort?>();
    public string? CloseReason => WorkerApi.NotExecutable<string?>();
    public bool WasClean => WorkerApi.NotExecutable<bool>();
    public WebSocketError? Error => WorkerApi.NotExecutable<WebSocketError?>();
}

public sealed class WebSocketEventStream
{
    public Task<WebSocketEvent?> NextAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<WebSocketEvent?>>();
}

public sealed record WebSocketAutoResponse(string Request, string Response);
