using System.Text.Json;

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
        var result = await DispatchAsync<RawEmailResult>("email.rawBytes", new { handle = _handle }, cancellationToken)
            ;

        return result.BodyBase64 is null
            ? Body.Empty
            : Body.FromBytes(Convert.FromBase64String(result.BodyBase64), "message/rfc822");
    }

    /// <summary>Rejects the message with a reason.</summary>
    public Task RejectAsync(string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return DispatchEmptyAsync("email.reject", new { handle = _handle, reason }, cancellationToken);
    }

    /// <summary>Forwards the message to another recipient.</summary>
    public Task<EmailSendResult> ForwardAsync(
        string recipient,
        Headers? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        var payload = new
        {
            handle = _handle,
            recipient,
            headers = headers?.Select(static header => new Header(header.Key, header.Value)).ToArray()
        };

        return DispatchAsync<EmailSendResult>("email.forward", payload, cancellationToken);
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

        return DispatchAsync<EmailSendResult>("email.replyRaw", new { handle = _handle, from, to, raw }, cancellationToken);
    }

    private async Task DispatchEmptyAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        _ = await DispatchStringAsync(operation, payload, cancellationToken);
    }

    private async Task<T> DispatchAsync<T>(string operation, object payload, CancellationToken cancellationToken)
    {
        var result = await DispatchStringAsync(operation, payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(result, JsonOptions)
            ?? throw new WorkersException($"Email operation '{operation}' returned an empty result.");
    }

    private Task<string> DispatchStringAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$email",
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed class RawEmailResult
    {
        public string? BodyBase64 { get; init; }
    }
}
