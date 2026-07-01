using System.Text.Json;
using System.Text.Json.Nodes;

namespace Workers;

/// <summary>An inbound Email Routing message that can be inspected, rejected, forwarded, or replied to.</summary>
public sealed class ForwardableEmailMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _handle;
    private readonly IBindingDispatcher _dispatcher;

    internal ForwardableEmailMessage(
        string invocationId,
        string handle,
        string from,
        string to,
        Headers headers,
        long rawSize,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(headers);

        _invocationId = invocationId;
        _handle = handle;
        From = from;
        To = to;
        Headers = headers;
        RawSize = rawSize;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The envelope sender.</summary>
    public string From { get; }

    /// <summary>The envelope recipient.</summary>
    public string To { get; }

    /// <summary>The message headers.</summary>
    public Headers Headers { get; }

    /// <summary>The raw message size reported by the Workers runtime.</summary>
    public long RawSize { get; }

    /// <summary>Reads the raw MIME message body. The Workers raw stream may only be consumed once.</summary>
    public async Task<Body> RawAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchStringAsync("email.rawBytes", PayloadWithHandle(), cancellationToken);
        using var document = JsonDocument.Parse(result);
        var bodyBase64 = document.RootElement.TryGetProperty("bodyBase64", out var bodyElement) &&
            bodyElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? bodyElement.GetString()
            : null;

        return bodyBase64 is null
            ? Body.Empty
            : Body.FromBytes(Convert.FromBase64String(bodyBase64), "message/rfc822");
    }

    /// <summary>Rejects the message with a reason.</summary>
    public Task RejectAsync(string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return DispatchEmptyAsync(
            "email.reject",
            new JsonObject
            {
                ["handle"] = _handle,
                ["reason"] = reason
            },
            cancellationToken);
    }

    /// <summary>Forwards the message to another recipient.</summary>
    public Task<EmailSendResult> ForwardAsync(
        string recipient,
        Headers? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        var payload = new JsonObject
        {
            ["handle"] = _handle,
            ["recipient"] = recipient,
            ["headers"] = HeadersToJson(headers)
        };

        return DispatchEmailResultAsync("email.forward", payload, cancellationToken);
    }

    /// <summary>Replies to the message with a raw MIME message.</summary>
    public Task<EmailSendResult> ReplyRawAsync(
        string from,
        string to,
        string raw,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(raw);

        return DispatchEmailResultAsync(
            "email.replyRaw",
            new JsonObject
            {
                ["handle"] = _handle,
                ["from"] = from,
                ["to"] = to,
                ["raw"] = raw
            },
            cancellationToken);
    }

    private async Task DispatchEmptyAsync(string operation, JsonObject payload, CancellationToken cancellationToken)
    {
        _ = await DispatchStringAsync(operation, payload, cancellationToken);
    }

    private async Task<EmailSendResult> DispatchEmailResultAsync(string operation, JsonObject payload, CancellationToken cancellationToken)
    {
        var result = await DispatchStringAsync(operation, payload, cancellationToken);
        using var document = JsonDocument.Parse(result);

        if (!document.RootElement.TryGetProperty("messageId", out var messageIdElement) ||
            messageIdElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw new WorkersException($"Email operation '{operation}' returned an empty result.");

        return new EmailSendResult(messageIdElement.GetString() ?? "");
    }

    private Task<string> DispatchStringAsync(string operation, JsonObject payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$email",
            operation,
            payload.ToJsonString(JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private JsonObject PayloadWithHandle() =>
        new()
        {
            ["handle"] = _handle
        };

    private static JsonArray? HeadersToJson(Headers? headers)
    {
        if (headers is null)
            return null;

        var array = new JsonArray();
        foreach (var header in headers)
        {
            array.Add(new JsonObject
            {
                ["key"] = header.Key,
                ["value"] = header.Value
            });
        }

        return array;
    }
}
