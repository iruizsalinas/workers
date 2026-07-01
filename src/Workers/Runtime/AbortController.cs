using System.Text.Json;

namespace Workers;

/// <summary>Controls cancellation for platform operations that accept a Worker abort signal.</summary>
public sealed class AbortController
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal AbortController(string invocationId, string handle, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        Handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Signal = new AbortSignal(handle);
    }

    /// <summary>The opaque platform handle for this controller.</summary>
    internal string Handle { get; }

    /// <summary>The signal passed to cancellable platform operations.</summary>
    public AbortSignal Signal { get; }

    /// <summary>Aborts operations using this controller's signal.</summary>
    public Task AbortAsync(CancellationToken cancellationToken = default) =>
        AbortAsync(reason: null, cancellationToken);

    /// <summary>Aborts operations using this controller's signal with a reason.</summary>
    public async Task AbortAsync(string? reason, CancellationToken cancellationToken = default)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$abort",
            "abort.abort",
            JsonSerializer.Serialize(new AbortRequest(Handle, reason), JsonOptions));

        await _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record AbortRequest(string Handle, string? Reason);
}

/// <summary>An abort signal that can be passed to cancellable platform operations.</summary>
public sealed class AbortSignal
{
    internal AbortSignal(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        Handle = handle;
    }

    /// <summary>The opaque platform handle for this signal.</summary>
    internal string Handle { get; }
}
