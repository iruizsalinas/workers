using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Workers;

/// <summary>Options for sending messages to a Workers Queue.</summary>
public sealed record QueueSendOptions
{
    /// <summary>The number of seconds to delay the message before delivery.</summary>
    public int? DelaySeconds { get; init; }
}

/// <summary>The content type used by Workers Queues for message previews and decoding.</summary>
public enum QueueContentType
{
    /// <summary>A JSON-serializable message.</summary>
    Json,

    /// <summary>A text message.</summary>
    Text,

    /// <summary>A binary message.</summary>
    Bytes
}

/// <summary>A single message request used in a queue batch send.</summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Queue send request payloads are intentionally serialized as JSON values for the Workers queue binding.")]
public sealed class QueueSendRequest
{
    private QueueSendRequest(JsonElement? body, QueueContentType contentType, int? delaySeconds, string? bodyBase64)
    {
        Body = body;
        ContentType = contentType;
        DelaySeconds = delaySeconds;
        BodyBase64 = bodyBase64;
    }

    internal JsonElement? Body { get; }

    internal QueueContentType ContentType { get; }

    internal int? DelaySeconds { get; }

    internal string? BodyBase64 { get; }

    /// <summary>Creates a JSON message request.</summary>
    public static QueueSendRequest Json<T>(T body, int? delaySeconds = null) =>
        new(JsonSerializer.SerializeToElement(body, QueueProducerBinding.JsonOptions), QueueContentType.Json, delaySeconds, null);

    /// <summary>Creates a text message request.</summary>
    public static QueueSendRequest Text(string body, int? delaySeconds = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new(JsonSerializer.SerializeToElement(body, QueueProducerBinding.JsonOptions), QueueContentType.Text, delaySeconds, null);
    }

    /// <summary>Creates a binary message request.</summary>
    public static QueueSendRequest Bytes(ReadOnlyMemory<byte> body, int? delaySeconds = null) =>
        new(null, QueueContentType.Bytes, delaySeconds, Convert.ToBase64String(body.Span));
}

/// <summary>Realtime metrics for a Workers Queue.</summary>
public sealed record QueueMetrics(
    long BacklogCount,
    long BacklogBytes,
    long OldestMessageTimestamp);

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Queue binding proxy payloads and envelopes are SDK-defined JSON shapes; browser-wasm workers keep reflection JSON enabled by default.")]
internal sealed class QueueProducerBinding : IQueueProducer
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public QueueProducerBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task SendJsonAsync<T>(T message, QueueSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["body"] = JsonNode.Parse(JsonSerializer.Serialize(message, JsonOptions)),
            ["contentType"] = "json",
            ["delaySeconds"] = options?.DelaySeconds
        };

        return DispatchAsync("queue.send", payload, cancellationToken);
    }

    public Task SendTextAsync(string message, QueueSendOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = new JsonObject
        {
            ["body"] = JsonSerializer.SerializeToNode(message, JsonOptions),
            ["contentType"] = "text",
            ["delaySeconds"] = options?.DelaySeconds
        };

        return DispatchAsync("queue.send", payload, cancellationToken);
    }

    public Task SendBytesAsync(
        ReadOnlyMemory<byte> message,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["contentType"] = "bytes",
            ["delaySeconds"] = options?.DelaySeconds,
            ["bodyBase64"] = Convert.ToBase64String(message.Span)
        };

        return DispatchAsync("queue.send", payload, cancellationToken);
    }

    public Task SendJsonBatchAsync<T>(
        IEnumerable<T> messages,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = new JsonObject
        {
            ["messages"] = new JsonArray(messages
                .Select(static message => new JsonObject
                {
                    ["body"] = JsonSerializer.SerializeToNode(message, JsonOptions),
                    ["contentType"] = "json"
                })
                .Cast<JsonNode?>()
                .ToArray()),
            ["delaySeconds"] = options?.DelaySeconds
        };

        return DispatchAsync("queue.sendBatch", payload, cancellationToken);
    }

    public Task SendBatchAsync(
        IEnumerable<QueueSendRequest> messages,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = new JsonObject
        {
            ["messages"] = new JsonArray(messages
                .Select(ToPayloadNode)
                .Cast<JsonNode?>()
                .ToArray()),
            ["delaySeconds"] = options?.DelaySeconds
        };

        return DispatchAsync("queue.sendBatch", payload, cancellationToken);
    }

    public Task SendTextBatchAsync(
        IEnumerable<string> messages,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = new JsonObject
        {
            ["messages"] = new JsonArray(messages
                .Select(static message =>
                {
                    ArgumentNullException.ThrowIfNull(message);
                    return new JsonObject
                    {
                        ["body"] = JsonSerializer.SerializeToNode(message, JsonOptions),
                        ["contentType"] = "text"
                    };
                })
                .Cast<JsonNode?>()
                .ToArray()),
            ["delaySeconds"] = options?.DelaySeconds
        };

        return DispatchAsync("queue.sendBatch", payload, cancellationToken);
    }

    public Task SendBytesBatchAsync(
        IEnumerable<ReadOnlyMemory<byte>> messages,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var payload = new JsonObject
        {
            ["messages"] = new JsonArray(messages
                .Select(static message => new JsonObject
                {
                    ["contentType"] = "bytes",
                    ["bodyBase64"] = Convert.ToBase64String(message.Span)
                })
                .Cast<JsonNode?>()
                .ToArray()),
            ["delaySeconds"] = options?.DelaySeconds
        };

        return DispatchAsync("queue.sendBatch", payload, cancellationToken);
    }

    public async Task<QueueMetrics> MetricsAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("queue.metrics", new { }, cancellationToken);
        return JsonSerializer.Deserialize<QueueMetrics>(result, JsonOptions)
            ?? throw new WorkersException("Queue metrics returned an empty result.");
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            payload is JsonNode node ? node.ToJsonString(JsonOptions) : JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static string ContentTypeName(QueueContentType contentType) =>
        contentType switch
        {
            QueueContentType.Json => "json",
            QueueContentType.Text => "text",
            QueueContentType.Bytes => "bytes",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported queue content type.")
        };

    private static JsonObject ToPayloadNode(QueueSendRequest message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var node = new JsonObject
        {
            ["contentType"] = ContentTypeName(message.ContentType),
            ["delaySeconds"] = message.DelaySeconds,
            ["bodyBase64"] = message.BodyBase64
        };

        if (message.Body is { } body)
            node["body"] = JsonNode.Parse(body.GetRawText());

        return node;
    }
}
