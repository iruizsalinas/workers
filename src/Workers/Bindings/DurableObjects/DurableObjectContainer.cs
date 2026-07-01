using System.Text.Json;

namespace Workers;

/// <summary>Low-level container runtime attached to a Durable Object instance.</summary>
public sealed class DurableObjectContainer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal DurableObjectContainer(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _invocationId = invocationId;
        _dispatcher = dispatcher;
    }

    /// <summary>Returns whether the container process is currently running.</summary>
    public async Task<bool> GetRunningAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("durable.container.running", new { }, cancellationToken)
            ;

        return JsonSerializer.Deserialize<ContainerRunningEnvelope>(result, JsonOptions)?.Running ?? false;
    }

    /// <summary>Boots the container. This does not wait for the container to become ready.</summary>
    public Task StartAsync(
        ContainerStartOptions? options = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.container.start", new { options }, cancellationToken);

    /// <summary>Stops the container and optionally supplies an error for monitor callbacks.</summary>
    public Task DestroyAsync(string? error = null, CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.container.destroy", new { error }, cancellationToken);

    /// <summary>Sends an IPC signal to the container process.</summary>
    public Task SignalAsync(int signal, CancellationToken cancellationToken = default)
    {
        ValidateSignal(signal);
        return DispatchAsync("durable.container.signal", new { signal }, cancellationToken);
    }

    /// <summary>Waits until the container exits or reports an error.</summary>
    public Task MonitorAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.container.monitor", new { }, cancellationToken);

    /// <summary>Routes matching outbound HTTP requests from the container through a Worker entrypoint.</summary>
    public Task InterceptOutboundHttpAsync(
        string target,
        RpcStub worker,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(worker);

        return DispatchAsync(
            "durable.container.interceptOutboundHttp",
            new { target, workerHandle = worker.Handle },
            cancellationToken);
    }

    /// <summary>Routes all outbound HTTP requests from the container through a Worker entrypoint.</summary>
    public Task InterceptAllOutboundHttpAsync(
        RpcStub worker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        return DispatchAsync(
            "durable.container.interceptAllOutboundHttp",
            new { workerHandle = worker.Handle },
            cancellationToken);
    }

    /// <summary>Routes matching outbound HTTPS requests from the container through a Worker entrypoint.</summary>
    public Task InterceptOutboundHttpsAsync(
        string target,
        RpcStub worker,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(worker);

        return DispatchAsync(
            "durable.container.interceptOutboundHttps",
            new { target, workerHandle = worker.Handle },
            cancellationToken);
    }

    /// <summary>Gets a TCP port exposed by the container for HTTP or TCP communication.</summary>
    public ContainerTcpPort GetTcpPort(int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "TCP port must be between 1 and 65535.");

        return new ContainerTcpPort(_invocationId, port, _dispatcher);
    }

    /// <summary>Starts a process inside a running container.</summary>
    public async Task<ContainerExecProcess> ExecAsync(
        IReadOnlyList<string> command,
        ContainerExecOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Count == 0)
            throw new ArgumentException("At least one command argument is required.", nameof(command));

        foreach (var part in command)
            ArgumentException.ThrowIfNullOrWhiteSpace(part);

        var result = await DispatchAsync(
            "durable.container.exec",
            new { command, options },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<ContainerExecEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object container returned an empty exec result.");

        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.Handle);
        return new ContainerExecProcess(_invocationId, envelope.Handle, envelope.Pid, _dispatcher);
    }

    internal static void ValidateSignal(int signal)
    {
        if (signal is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(signal), signal, "Signal must be between 1 and 64.");
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record ContainerRunningEnvelope(bool Running);

    private sealed record ContainerExecEnvelope(string Handle, int Pid);
}
