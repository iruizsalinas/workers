using System.Text.Json;

namespace Workers;

/// <summary>A process started inside a Durable Object container.</summary>
public sealed class ContainerExecProcess : IAsyncDisposable
{
    private static readonly DurableObjectContainerJsonContext JsonContext = DurableObjectContainer.JsonContext;

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

        return JsonSerializer.Deserialize(result, JsonContext.DurableContainerExecExitCodeEnvelope)?.ExitCode ?? 0;
    }

    /// <summary>Reads the process buffered output and exit code.</summary>
    public async Task<ContainerExecOutput> OutputAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("durable.container.exec.output", cancellationToken)
            ;
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableContainerExecOutputEnvelope)
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

        return DispatchAsync(
            "durable.container.exec.kill",
            JsonSerializer.Serialize(new DurableContainerExecKillPayload { Handle = _handle, Signal = signal }, JsonContext.DurableContainerExecKillPayload),
            cancellationToken);
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
        DispatchAsync(
            operation,
            JsonSerializer.Serialize(new DurableContainerExecHandlePayload { Handle = _handle }, JsonContext.DurableContainerExecHandlePayload),
            cancellationToken);

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static byte[] FromBase64(string? value) =>
        string.IsNullOrEmpty(value) ? [] : Convert.FromBase64String(value);

}

internal sealed class DurableContainerExecHandlePayload
{
    public string Handle { get; set; } = "";
}

internal sealed class DurableContainerExecKillPayload
{
    public string Handle { get; set; } = "";

    public int? Signal { get; set; }
}

internal sealed class DurableContainerExecExitCodeEnvelope
{
    public int ExitCode { get; set; }
}

internal sealed class DurableContainerExecOutputEnvelope
{
    public string? StdoutBase64 { get; set; }

    public string? StderrBase64 { get; set; }

    public int ExitCode { get; set; }
}
