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
    public int ReadyState => WorkerApi.NotExecutable<int>();

    public void Accept() => WorkerApi.NotExecutable();
    public void SendText(string message) => WorkerApi.NotExecutable();
    public void SendBytes(ReadOnlyMemory<byte> message) => WorkerApi.NotExecutable();
    public void SendJson<T>(T message) => WorkerApi.NotExecutable();
    public void SerializeAttachment<T>(T value) => WorkerApi.NotExecutable();
    public T? DeserializeAttachment<T>() => WorkerApi.NotExecutable<T?>();
    public void Close(ushort? code = null, string? reason = null) => WorkerApi.NotExecutable();

    /// <summary>Adds a <c>message</c> event listener; the handler receives the event's data.</summary>
    public void OnMessage(Action<WebSocketMessage> handler) => WorkerApi.NotExecutable();
    public void OnMessage(Func<WebSocketMessage, Task> handler) => WorkerApi.NotExecutable();
    /// <summary>Adds a <c>close</c> event listener.</summary>
    public void OnClose(Action<WebSocketCloseEvent> handler) => WorkerApi.NotExecutable();
    public void OnClose(Func<WebSocketCloseEvent, Task> handler) => WorkerApi.NotExecutable();
    /// <summary>Adds an <c>error</c> event listener.</summary>
    public void OnError(Action<WebSocketErrorEvent> handler) => WorkerApi.NotExecutable();
    public void OnError(Func<WebSocketErrorEvent, Task> handler) => WorkerApi.NotExecutable();
}

/// <summary>Message data: a string for text frames, binary data otherwise.</summary>
public sealed class WebSocketMessage
{
    private WebSocketMessage() { }
    public bool IsText => WorkerApi.NotExecutable<bool>();
    /// <summary>The text of a text frame, or binary data decoded as UTF-8.</summary>
    public string AsText() => WorkerApi.NotExecutable<string>();
    /// <summary>The bytes of a binary frame, or a text frame encoded as UTF-8.</summary>
    public byte[] AsBytes() => WorkerApi.NotExecutable<byte[]>();
}

public sealed class WebSocketCloseEvent
{
    private WebSocketCloseEvent() { }
    public int Code => WorkerApi.NotExecutable<int>();
    public string Reason => WorkerApi.NotExecutable<string>();
    public bool WasClean => WorkerApi.NotExecutable<bool>();
}

public sealed class WebSocketErrorEvent
{
    private WebSocketErrorEvent() { }
    public string Message => WorkerApi.NotExecutable<string>();
}

public sealed record WebSocketAutoResponse(string Request, string Response);
