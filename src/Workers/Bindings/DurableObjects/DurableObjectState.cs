using System.Text.Json;
using System.Runtime.InteropServices.JavaScript;
using Workers.Interop;

namespace Workers;

/// <summary>Runtime state supplied to a Durable Object instance.</summary>
public sealed class DurableObjectState
{
    private static readonly TimeSpan MaxHibernatableWebSocketEventTimeout = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableObjectStorageJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;
    private readonly JSObject? _nativeState;

    internal DurableObjectState(
        string invocationId,
        DurableObjectId id,
        IBindingDispatcher dispatcher,
        JSObject? nativeState = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _invocationId = invocationId;
        _dispatcher = dispatcher;
        _nativeState = nativeState;
        Id = id;
        Container = new DurableObjectContainer(invocationId, dispatcher);
        Storage = new DurableObjectStorage(invocationId, dispatcher, nativeState);
    }

    /// <summary>The Durable Object ID for this instance.</summary>
    public DurableObjectId Id { get; }

    /// <summary>The container attached to this Durable Object instance, when the class has a container binding.</summary>
    public DurableObjectContainer Container { get; }

    /// <summary>The persistent storage for this Durable Object instance.</summary>
    public DurableObjectStorage Storage { get; }

    /// <summary>
    /// Accepts a task for Durable Object State API compatibility.
    /// Durable Objects remain active while work is pending, so this does not extend object lifetime.
    /// </summary>
    public void WaitUntil(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);
    }

    /// <summary>Runs a callback while the Durable Object runtime blocks delivery of other events.</summary>
    public async Task BlockConcurrencyWhileAsync(
        Func<Task> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_nativeState is not null && OperatingSystem.IsBrowser())
        {
            await callback();
            return;
        }

        var handle = DurableObjectStateCallbackRegistry.Retain(callback);
        try
        {
            await DispatchAsync(
                "durable.state.blockConcurrencyWhile",
                JsonSerializer.Serialize(new DurableStateCallbackPayload { Handle = handle }, JsonContext.DurableStateCallbackPayload),
                cancellationToken);
        }
        finally
        {
            DurableObjectStateCallbackRegistry.Release(handle);
        }
    }

    /// <summary>Runs a callback while the Durable Object runtime blocks delivery of other events and returns its result.</summary>
    public async Task<TResult> BlockConcurrencyWhileAsync<TResult>(
        Func<Task<TResult>> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var result = default(TResult);
        var completed = false;
        await BlockConcurrencyWhileAsync(
            async () =>
            {
                result = await callback();
                completed = true;
            },
            cancellationToken);

        return completed
            ? result!
            : throw new WorkersException("Durable Object blockConcurrencyWhile callback did not complete.");
    }

    /// <summary>Forcibly resets this Durable Object instance.</summary>
    public Task AbortAsync(string? reason = null, CancellationToken cancellationToken = default)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            "durable.state.abort",
            JsonSerializer.Serialize(new DurableStateAbortPayload { Reason = reason }, JsonContext.DurableStateAbortPayload));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    /// <summary>Accepts a WebSocket using the Durable Object WebSocket Hibernation API.</summary>
    public Task AcceptWebSocketAsync(
        WebSocket socket,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);
        var tagArray = tags?.Select(RequireTag).ToArray() ?? [];

        return DispatchAsync(
            "durable.state.acceptWebSocket",
            JsonSerializer.Serialize(new DurableStateWebSocketAcceptPayload { Handle = socket.Handle, Tags = tagArray }, JsonContext.DurableStateWebSocketAcceptPayload),
            cancellationToken);
    }

    /// <summary>Gets hibernatable WebSockets attached to this Durable Object, optionally filtered by tag.</summary>
    public async Task<IReadOnlyList<WebSocket>> GetWebSocketsAsync(
        string? tag = null,
        CancellationToken cancellationToken = default)
    {
        if (tag is not null)
            _ = RequireTag(tag);

        var result = await DispatchAsync(
            "durable.state.getWebSockets",
            JsonSerializer.Serialize(new DurableStateWebSocketTagPayload { Tag = tag }, JsonContext.DurableStateWebSocketTagPayload),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStateWebSocketHandlesEnvelope)
            ?? throw new WorkersException("Durable Object state returned an empty WebSocket list result.");

        return envelope.Handles
            .Select(handle => new WebSocket(_invocationId, handle, _dispatcher))
            .ToArray();
    }

    /// <summary>Gets tags associated with a hibernatable WebSocket attached to this Durable Object.</summary>
    public async Task<IReadOnlyList<string>> GetTagsAsync(
        WebSocket socket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var result = await DispatchAsync(
            "durable.state.getTags",
            JsonSerializer.Serialize(new DurableStateWebSocketHandlePayload { Handle = socket.Handle }, JsonContext.DurableStateWebSocketHandlePayload),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStateWebSocketTagsEnvelope)
            ?? throw new WorkersException("Durable Object state returned an empty WebSocket tags result.");

        return envelope.Tags;
    }

    /// <summary>Sets or clears the automatic response used for hibernatable WebSockets.</summary>
    public Task SetWebSocketAutoResponseAsync(
        WebSocketAutoResponse? pair,
        CancellationToken cancellationToken = default)
    {
        if (pair is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Request);
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Response);
        }

        return DispatchAsync(
            "durable.state.setWebSocketAutoResponse",
            JsonSerializer.Serialize(new DurableStateWebSocketAutoResponsePayload { Pair = pair }, JsonContext.DurableStateWebSocketAutoResponsePayload),
            cancellationToken);
    }

    /// <summary>Gets the automatic response configured for hibernatable WebSockets, when one is set.</summary>
    public async Task<WebSocketAutoResponse?> GetWebSocketAutoResponseAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.state.getWebSocketAutoResponse",
            EmptyPayload(),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableStateWebSocketAutoResponseEnvelope)
            ?? throw new WorkersException("Durable Object state returned an empty WebSocket auto-response result.");

        return envelope.Pair;
    }

    /// <summary>Gets the most recent automatic response timestamp for a hibernatable WebSocket.</summary>
    public async Task<DateTimeOffset?> GetWebSocketAutoResponseTimestampAsync(
        WebSocket socket,
        CancellationToken cancellationToken = default)
    {
        var milliseconds = await GetWebSocketAutoResponseUnixTimeMillisecondsAsync(socket, cancellationToken)
            ;
        return milliseconds is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value);
    }

    /// <summary>Gets the most recent automatic response timestamp as Unix epoch milliseconds.</summary>
    public async Task<long?> GetWebSocketAutoResponseUnixTimeMillisecondsAsync(
        WebSocket socket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var result = await DispatchAsync(
            "durable.state.getWebSocketAutoResponseTimestamp",
            JsonSerializer.Serialize(new DurableStateWebSocketHandlePayload { Handle = socket.Handle }, JsonContext.DurableStateWebSocketHandlePayload),
            cancellationToken);
        return JsonSerializer.Deserialize(result, JsonContext.DurableStateWebSocketAutoResponseTimestampEnvelope)?.Timestamp;
    }

    /// <summary>Sets or clears the maximum runtime for a hibernatable WebSocket event.</summary>
    public Task SetHibernatableWebSocketEventTimeoutAsync(
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        double? timeoutMilliseconds = null;
        if (timeout is not null)
        {
            if (timeout.Value < TimeSpan.Zero || timeout.Value > MaxHibernatableWebSocketEventTimeout)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be between zero and seven days.");

            timeoutMilliseconds = timeout.Value.TotalMilliseconds;
        }

        return DispatchAsync(
            "durable.state.setHibernatableWebSocketEventTimeout",
            JsonSerializer.Serialize(new DurableStateWebSocketEventTimeoutPayload { TimeoutMilliseconds = timeoutMilliseconds }, JsonContext.DurableStateWebSocketEventTimeoutPayload),
            cancellationToken);
    }

    /// <summary>Gets the maximum runtime configured for a hibernatable WebSocket event.</summary>
    public async Task<TimeSpan?> GetHibernatableWebSocketEventTimeoutAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.state.getHibernatableWebSocketEventTimeout",
            EmptyPayload(),
            cancellationToken);
        var milliseconds = JsonSerializer.Deserialize(result, JsonContext.DurableStateWebSocketEventTimeoutEnvelope)
            ?.TimeoutMilliseconds;

        return milliseconds is null ? null : TimeSpan.FromMilliseconds(milliseconds.Value);
    }

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static string RequireTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return tag;
    }

    private static string EmptyPayload() =>
        JsonSerializer.Serialize(new DurableStorageEmptyPayload(), JsonContext.DurableStorageEmptyPayload);
}

internal sealed class DurableStateCallbackPayload
{
    public string Handle { get; set; } = "";
}

internal sealed class DurableStateAbortPayload
{
    public string? Reason { get; set; }
}

internal sealed class DurableStateWebSocketHandlePayload
{
    public string Handle { get; set; } = "";
}

internal sealed class DurableStateWebSocketAcceptPayload
{
    public string Handle { get; set; } = "";

    public IReadOnlyList<string> Tags { get; set; } = [];
}

internal sealed class DurableStateWebSocketTagPayload
{
    public string? Tag { get; set; }
}

internal sealed class DurableStateWebSocketAutoResponsePayload
{
    public WebSocketAutoResponse? Pair { get; set; }
}

internal sealed class DurableStateWebSocketEventTimeoutPayload
{
    public double? TimeoutMilliseconds { get; set; }
}

internal sealed class DurableStateWebSocketHandlesEnvelope
{
    public IReadOnlyList<string> Handles { get; set; } = [];
}

internal sealed class DurableStateWebSocketTagsEnvelope
{
    public IReadOnlyList<string> Tags { get; set; } = [];
}

internal sealed class DurableStateWebSocketAutoResponseEnvelope
{
    public WebSocketAutoResponse? Pair { get; set; }
}

internal sealed class DurableStateWebSocketAutoResponseTimestampEnvelope
{
    public long? Timestamp { get; set; }
}

internal sealed class DurableStateWebSocketEventTimeoutEnvelope
{
    public double? TimeoutMilliseconds { get; set; }
}
