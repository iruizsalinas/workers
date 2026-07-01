using System.Text.Json;
using System.Text.Json.Serialization;
using Workers.Interop;

namespace Workers;

/// <summary>Low-level container runtime attached to a Durable Object instance.</summary>
public sealed class DurableObjectContainer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static readonly DurableObjectContainerJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

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
        var result = await DispatchAsync("durable.container.running", EmptyPayload(), cancellationToken)
            ;

        return JsonSerializer.Deserialize(result, JsonContext.DurableContainerRunningEnvelope)?.Running ?? false;
    }

    /// <summary>Boots the container. This does not wait for the container to become ready.</summary>
    public Task StartAsync(
        ContainerStartOptions? options = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(
            "durable.container.start",
            JsonSerializer.Serialize(new DurableContainerStartPayload { Options = options }, JsonContext.DurableContainerStartPayload),
            cancellationToken);

    /// <summary>Stops the container and optionally supplies an error for monitor callbacks.</summary>
    public Task DestroyAsync(string? error = null, CancellationToken cancellationToken = default) =>
        DispatchAsync(
            "durable.container.destroy",
            JsonSerializer.Serialize(new DurableContainerDestroyPayload { Error = error }, JsonContext.DurableContainerDestroyPayload),
            cancellationToken);

    /// <summary>Sends an IPC signal to the container process.</summary>
    public Task SignalAsync(int signal, CancellationToken cancellationToken = default)
    {
        ValidateSignal(signal);
        return DispatchAsync(
            "durable.container.signal",
            JsonSerializer.Serialize(new DurableContainerSignalPayload { Signal = signal }, JsonContext.DurableContainerSignalPayload),
            cancellationToken);
    }

    /// <summary>Waits until the container exits or reports an error.</summary>
    public Task MonitorAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.container.monitor", EmptyPayload(), cancellationToken);

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
            JsonSerializer.Serialize(
                new DurableContainerInterceptPayload
                {
                    Target = target,
                    WorkerHandle = worker.Handle
                },
                JsonContext.DurableContainerInterceptPayload),
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
            JsonSerializer.Serialize(new DurableContainerWorkerPayload { WorkerHandle = worker.Handle }, JsonContext.DurableContainerWorkerPayload),
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
            JsonSerializer.Serialize(
                new DurableContainerInterceptPayload
                {
                    Target = target,
                    WorkerHandle = worker.Handle
                },
                JsonContext.DurableContainerInterceptPayload),
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
            JsonSerializer.Serialize(
                new DurableContainerExecPayload
                {
                    Command = command,
                    Options = options
                },
                JsonContext.DurableContainerExecPayload),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableContainerExecEnvelope)
            ?? throw new WorkersException("Durable Object container returned an empty exec result.");

        ArgumentException.ThrowIfNullOrWhiteSpace(envelope.Handle);
        return new ContainerExecProcess(_invocationId, envelope.Handle, envelope.Pid, _dispatcher);
    }

    internal static void ValidateSignal(int signal)
    {
        if (signal is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(signal), signal, "Signal must be between 1 and 64.");
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

    private static string EmptyPayload() =>
        JsonSerializer.Serialize(new DurableStorageEmptyPayload(), JsonContext.DurableStorageEmptyPayload);
}

internal sealed class DurableContainerStartPayload
{
    public ContainerStartOptions? Options { get; set; }
}

internal sealed class DurableContainerDestroyPayload
{
    public string? Error { get; set; }
}

internal sealed class DurableContainerSignalPayload
{
    public int Signal { get; set; }
}

internal sealed class DurableContainerInterceptPayload
{
    public string Target { get; set; } = "";

    public string WorkerHandle { get; set; } = "";
}

internal sealed class DurableContainerWorkerPayload
{
    public string WorkerHandle { get; set; } = "";
}

internal sealed class DurableContainerExecPayload
{
    public IReadOnlyList<string> Command { get; set; } = [];

    public ContainerExecOptions? Options { get; set; }
}

internal sealed class DurableContainerRunningEnvelope
{
    public bool Running { get; set; }
}

internal sealed class DurableContainerExecEnvelope
{
    public string Handle { get; set; } = "";

    public int Pid { get; set; }
}

[JsonSerializable(typeof(DurableStorageEmptyPayload))]
[JsonSerializable(typeof(DurableContainerStartPayload))]
[JsonSerializable(typeof(DurableContainerDestroyPayload))]
[JsonSerializable(typeof(DurableContainerSignalPayload))]
[JsonSerializable(typeof(DurableContainerInterceptPayload))]
[JsonSerializable(typeof(DurableContainerWorkerPayload))]
[JsonSerializable(typeof(DurableContainerExecPayload))]
[JsonSerializable(typeof(DurableContainerRunningEnvelope))]
[JsonSerializable(typeof(DurableContainerExecEnvelope))]
[JsonSerializable(typeof(ContainerStartOptions))]
[JsonSerializable(typeof(ContainerExecOptions))]
[JsonSerializable(typeof(DurableContainerExecHandlePayload))]
[JsonSerializable(typeof(DurableContainerExecKillPayload))]
[JsonSerializable(typeof(DurableContainerExecExitCodeEnvelope))]
[JsonSerializable(typeof(DurableContainerExecOutputEnvelope))]
[JsonSerializable(typeof(DurableContainerTcpPortFetchPayload))]
[JsonSerializable(typeof(DurableContainerTcpPortConnectPayload))]
[JsonSerializable(typeof(SocketHandleEnvelope))]
[JsonSerializable(typeof(FetchBindingRequest))]
[JsonSerializable(typeof(FetchOptions))]
[JsonSerializable(typeof(ResponseEnvelope))]
[JsonSerializable(typeof(Header))]
[JsonSerializable(typeof(SocketAddress))]
internal sealed partial class DurableObjectContainerJsonContext : JsonSerializerContext
{
}
