using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>A pair of WebSockets created by the Workers runtime.</summary>
public sealed class WebSocketPair
{
    internal WebSocketPair(WebSocket client, WebSocket server)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <summary>The client socket, usually returned in a 101 response.</summary>
    public WebSocket Client { get; }

    /// <summary>The server socket, usually accepted and handled by Worker code.</summary>
    public WebSocket Server { get; }
}

/// <summary>A handle to a Workers WebSocket.</summary>
public sealed partial class WebSocket
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly WebSocketJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal WebSocket(string invocationId, string handle, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        Handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The runtime handle for this WebSocket.</summary>
    internal string Handle { get; }

    /// <summary>Accepts the server side of the WebSocket.</summary>
    public Task AcceptAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync(
            "websocket.accept",
            JsonSerializer.Serialize(new WebSocketHandleRequest { Handle = Handle }, JsonContext.WebSocketHandleRequest),
            cancellationToken);

    /// <summary>Sends a text message.</summary>
    public Task SendTextAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return DispatchAsync(
            "websocket.sendText",
            JsonSerializer.Serialize(
                new WebSocketTextSendRequest { Handle = Handle, Message = message },
                JsonContext.WebSocketTextSendRequest),
            cancellationToken);
    }

    /// <summary>Sends a binary message.</summary>
    public Task SendBytesAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default) =>
        DispatchAsync(
            "websocket.sendBytes",
            JsonSerializer.Serialize(
                new WebSocketBytesSendRequest
                {
                    Handle = Handle,
                    BodyBase64 = Convert.ToBase64String(message.Span)
                },
                JsonContext.WebSocketBytesSendRequest),
            cancellationToken);

    /// <summary>Closes the socket.</summary>
    public Task CloseAsync(ushort? code = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        ValidateClose(code, reason);
        return DispatchAsync(
            "websocket.close",
            JsonSerializer.Serialize(
                new WebSocketCloseRequest { Handle = Handle, Code = code, Reason = reason },
                JsonContext.WebSocketCloseRequest),
            cancellationToken);
    }

    /// <summary>Creates an event stream for messages and close events received by this socket.</summary>
    public WebSocketEventStream Events() => new(this);

    /// <summary>Reads the next WebSocket event, or <see langword="null"/> after the stream has ended.</summary>
    public async Task<WebSocketEvent?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
                "websocket.receive",
                JsonSerializer.Serialize(new WebSocketHandleRequest { Handle = Handle }, JsonContext.WebSocketHandleRequest),
                cancellationToken)
            ;

        var envelope = JsonSerializer.Deserialize(result, JsonContext.WebSocketReceiveEnvelope)
            ?? throw new WorkersException("WebSocket receive returned an empty result.");

        return envelope.Event is null
            ? null
            : WebSocketEvent.FromEnvelope(envelope.Event);
    }

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$websocket",
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static void ValidateClose(ushort? code, string? reason)
    {
        if (code is not null && code != 1000 && (code < 3000 || code > 4999))
            throw new ArgumentOutOfRangeException(nameof(code), code, "WebSocket close codes must be 1000 or from 3000 through 4999.");

        if (reason is not null && Encoding.UTF8.GetByteCount(reason) > 123)
            throw new ArgumentException("WebSocket close reasons cannot exceed 123 UTF-8 bytes.", nameof(reason));
    }

    private sealed class WebSocketHandleRequest
    {
        [JsonPropertyName("handle")]
        public string Handle { get; set; } = "";
    }

    private sealed class WebSocketTextSendRequest
    {
        [JsonPropertyName("handle")]
        public string Handle { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    private sealed class WebSocketBytesSendRequest
    {
        [JsonPropertyName("handle")]
        public string Handle { get; set; } = "";

        [JsonPropertyName("bodyBase64")]
        public string BodyBase64 { get; set; } = "";
    }

    private sealed class WebSocketCloseRequest
    {
        [JsonPropertyName("handle")]
        public string Handle { get; set; } = "";

        [JsonPropertyName("code")]
        public ushort? Code { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    private sealed class WebSocketReceiveEnvelope
    {
        [JsonPropertyName("event")]
        public WebSocketEventEnvelope? Event { get; set; }
    }

    internal sealed class WebSocketEventEnvelope
    {
        public WebSocketEventEnvelope()
        {
        }

        internal WebSocketEventEnvelope(
            string kind,
            string? Text,
            string? bodyBase64,
            ushort? Code,
            string? Reason,
            bool? WasClean)
        {
            Kind = kind;
            this.Text = Text;
            BodyBase64 = bodyBase64;
            this.Code = Code;
            this.Reason = Reason;
            this.WasClean = WasClean;
        }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("bodyBase64")]
        public string? BodyBase64 { get; set; }

        [JsonPropertyName("code")]
        public ushort? Code { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("wasClean")]
        public bool? WasClean { get; set; }
    }

    [JsonSerializable(typeof(WebSocketHandleRequest))]
    [JsonSerializable(typeof(WebSocketTextSendRequest))]
    [JsonSerializable(typeof(WebSocketBytesSendRequest))]
    [JsonSerializable(typeof(WebSocketCloseRequest))]
    [JsonSerializable(typeof(WebSocketReceiveEnvelope))]
    [JsonSerializable(typeof(WebSocketEventEnvelope))]
    private sealed partial class WebSocketJsonContext : JsonSerializerContext
    {
    }
}

/// <summary>A text or binary message received by a hibernatable Durable Object WebSocket.</summary>
public sealed class WebSocketMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _bytes;

    internal WebSocketMessage(string? text, byte[] bytes)
    {
        Text = text;
        _bytes = bytes.ToArray();
    }

    /// <summary>The text payload, when this is a text message.</summary>
    public string? Text { get; }

    /// <summary>The binary payload, when this is a binary message.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes.ToArray();

    /// <summary>True when this message contains text.</summary>
    public bool IsText => Text is not null;

    /// <summary>Deserializes a text message payload as JSON.</summary>
    public T? Json<T>(JsonSerializerOptions? options = null)
    {
        if (Text is null)
            throw new InvalidOperationException("Only text WebSocket messages can be deserialized as JSON.");

        return JsonSerializer.Deserialize<T>(Text, options ?? JsonOptions);
    }
}

/// <summary>An error delivered to a hibernatable Durable Object WebSocket.</summary>
public sealed record WebSocketError(string Message);

/// <summary>The kind of event read from a Workers WebSocket.</summary>
public enum WebSocketEventKind
{
    /// <summary>A text or binary message was received.</summary>
    Message,

    /// <summary>The socket was closed.</summary>
    Close
}

/// <summary>A message or close event read from a Workers WebSocket.</summary>
public sealed class WebSocketEvent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _bytes;

    private WebSocketEvent(
        WebSocketEventKind kind,
        string? text,
        byte[] bytes,
        ushort? code,
        string? reason,
        bool? wasClean)
    {
        Kind = kind;
        Text = text;
        _bytes = bytes;
        Code = code;
        Reason = reason;
        WasClean = wasClean;
    }

    /// <summary>The kind of event.</summary>
    public WebSocketEventKind Kind { get; }

    /// <summary>The text message payload, when the event is a text message.</summary>
    public string? Text { get; }

    /// <summary>The binary message payload, when the event is a binary message.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes.ToArray();

    /// <summary>The close code, when the event is a close event.</summary>
    public ushort? Code { get; }

    /// <summary>The close reason, when the event is a close event.</summary>
    public string? Reason { get; }

    /// <summary>Whether the connection closed cleanly, when the event is a close event.</summary>
    public bool? WasClean { get; }

    /// <summary>Deserializes a text message payload as JSON.</summary>
    public T? Json<T>(JsonSerializerOptions? options = null)
    {
        if (Text is null)
            throw new InvalidOperationException("Only text WebSocket messages can be deserialized as JSON.");

        return JsonSerializer.Deserialize<T>(Text, options ?? JsonOptions);
    }

    internal static WebSocketEvent FromEnvelope(
        WebSocket.WebSocketEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Kind switch
        {
            "message" => new WebSocketEvent(
                WebSocketEventKind.Message,
                envelope.Text,
                envelope.BodyBase64 is null ? [] : Convert.FromBase64String(envelope.BodyBase64),
                code: null,
                reason: null,
                wasClean: null),
            "close" => new WebSocketEvent(
                WebSocketEventKind.Close,
                text: null,
                bytes: [],
                envelope.Code,
                envelope.Reason,
                envelope.WasClean),
            _ => throw new WorkersException($"Unsupported WebSocket event kind '{envelope.Kind}'.")
        };
    }
}

/// <summary>A sequential reader for events received by a Workers WebSocket.</summary>
public sealed class WebSocketEventStream
{
    private readonly WebSocket _socket;

    internal WebSocketEventStream(WebSocket socket)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    /// <summary>Reads the next event, or <see langword="null"/> after the stream has ended.</summary>
    public Task<WebSocketEvent?> NextAsync(CancellationToken cancellationToken = default) =>
        _socket.ReceiveAsync(cancellationToken);
}
