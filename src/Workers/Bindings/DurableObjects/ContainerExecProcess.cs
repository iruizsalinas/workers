using System.Text.Json;

namespace Workers;

/// <summary>A process started inside a Durable Object container.</summary>
public sealed class ContainerExecProcess : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _handle;
    private readonly IBindingDispatcher _dispatcher;

    private bool _disposed;

    internal ContainerExecProcess(
        string invocationId,
        string handle,
        int pid,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _invocationId = invocationId;
        _handle = handle;
        _dispatcher = dispatcher;
        Pid = pid;
    }

    /// <summary>The runtime process identifier.</summary>
    public int Pid { get; }

    /// <summary>Waits for the process to exit and returns its exit code.</summary>
    public async Task<int> GetExitCodeAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("durable.container.exec.exitCode", cancellationToken)
            ;

        return JsonSerializer.Deserialize<ContainerExecExitCodeEnvelope>(result, JsonOptions)?.ExitCode ?? 0;
    }

    /// <summary>Reads the process buffered output and exit code.</summary>
    public async Task<ContainerExecOutput> OutputAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("durable.container.exec.output", cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize<ContainerExecOutputEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object container returned an empty exec output result.");

        return new ContainerExecOutput
        {
            Stdout = FromBase64(envelope.StdoutBase64),
            Stderr = FromBase64(envelope.StderrBase64),
            ExitCode = envelope.ExitCode
        };
    }

    /// <summary>Sends a signal to this process. When omitted, the runtime default signal is used.</summary>
    public Task KillAsync(int? signal = null, CancellationToken cancellationToken = default)
    {
        if (signal is not null)
            DurableObjectContainer.ValidateSignal(signal.Value);

        return DispatchAsync("durable.container.exec.kill", new { handle = _handle, signal }, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DispatchAsync("durable.container.exec.release", CancellationToken.None);
    }

    private Task<string> DispatchAsync(string operation, CancellationToken cancellationToken) =>
        DispatchAsync(operation, new { handle = _handle }, cancellationToken);

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static byte[] FromBase64(string? value) =>
        string.IsNullOrEmpty(value) ? [] : Convert.FromBase64String(value);

    private sealed record ContainerExecExitCodeEnvelope(int ExitCode);

    private sealed record ContainerExecOutputEnvelope(string? StdoutBase64, string? StderrBase64, int ExitCode);
}
