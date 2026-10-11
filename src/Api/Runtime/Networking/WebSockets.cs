namespace Workers;

/// <summary>A connected pair of WebSockets: return <see cref="Client"/> in a 101 response and use <see cref="Server"/>.</summary>
public sealed class WebSocketPair
{
    public WebSocketPair() => WorkerApi.NotExecutable();

    public WebSocket Client => WorkerApi.NotExecutable<WebSocket>();
    public WebSocket Server => WorkerApi.NotExecutable<WebSocket>();
}

public sealed class WebSocket
{
    private WebSocket() { }

    public const int Connecting = 0;
    public const int Open = 1;
    public const int Closing = 2;
    public const int Closed = 3;

    public int ReadyState => WorkerApi.NotExecutable<int>();
    public string? Url => WorkerApi.NotExecutable<string?>();
    public string? Protocol => WorkerApi.NotExecutable<string?>();
    public string? Extensions => WorkerApi.NotExecutable<string?>();

    /// <summary>Accepts the server end of a pair so this Worker can send and receive messages.
    /// Durable Objects accept with <see cref="DurableObjectState.AcceptWebSocket"/> to allow hibernation.</summary>
    public void Accept() => WorkerApi.NotExecutable();
    public void Send(string message) => WorkerApi.NotExecutable();
    public void Send(ReadOnlyMemory<byte> message) => WorkerApi.NotExecutable();
    public void Close(int? code = null, string? reason = null) => WorkerApi.NotExecutable();
    /// <summary>Stores a structured-cloneable value of up to 16 KiB that survives Durable Object hibernation.</summary>
    public void SerializeAttachment<T>(T attachment) => WorkerApi.NotExecutable();
    public T? DeserializeAttachment<T>() => WorkerApi.NotExecutable<T?>();

    public void OnMessage(Action<WebSocketMessage> handler) => WorkerApi.NotExecutable();
    public void OnMessage(Func<WebSocketMessage, Task> handler) => WorkerApi.NotExecutable();
    public void OnClose(Action<CloseEvent> handler) => WorkerApi.NotExecutable();
    public void OnClose(Func<CloseEvent, Task> handler) => WorkerApi.NotExecutable();
    public void OnError(Action<ErrorEvent> handler) => WorkerApi.NotExecutable();
    public void OnError(Func<ErrorEvent, Task> handler) => WorkerApi.NotExecutable();
}

/// <summary>A message's data: text for a text frame, bytes for a binary frame.</summary>
public sealed class WebSocketMessage
{
    private WebSocketMessage() { }

    public bool IsText => WorkerApi.NotExecutable<bool>();
    /// <summary>The text of a text frame, or a binary frame decoded as UTF-8.</summary>
    public string AsText() => WorkerApi.NotExecutable<string>();
    /// <summary>The bytes of a binary frame, or a text frame encoded as UTF-8.</summary>
    public byte[] AsBytes() => WorkerApi.NotExecutable<byte[]>();
}

public sealed class CloseEvent
{
    private CloseEvent() { }

    public int Code => WorkerApi.NotExecutable<int>();
    public string Reason => WorkerApi.NotExecutable<string>();
    public bool WasClean => WorkerApi.NotExecutable<bool>();
}

public sealed class ErrorEvent
{
    private ErrorEvent() { }

    public string Message => WorkerApi.NotExecutable<string>();
}

/// <summary>A request message and the reply a Durable Object sends without waking from hibernation.</summary>
public sealed class WebSocketRequestResponsePair
{
    public WebSocketRequestResponsePair(string request, string response) => WorkerApi.NotExecutable();

    public string Request => WorkerApi.NotExecutable<string>();
    public string Response => WorkerApi.NotExecutable<string>();
}
