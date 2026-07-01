using System.Text.Json;
using Workers.Interop;

namespace Workers;

/// <summary>A TCP port exposed by a Durable Object container.</summary>
public sealed class ContainerTcpPort
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal ContainerTcpPort(string invocationId, int port, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _invocationId = invocationId;
        _dispatcher = dispatcher;
        Port = port;
    }

    /// <summary>The exposed container TCP port.</summary>
    public int Port { get; }

    /// <summary>Sends a GET request to this container TCP port.</summary>
    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        FetchAsync(url, options: null, cancellationToken);

    /// <summary>Sends a GET request to this container TCP port.</summary>
    public Task<Response> FetchAsync(
        string url,
        FetchOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return FetchAsync(Request.Get(url), options, cancellationToken);
    }

    /// <summary>Sends a request to this container TCP port.</summary>
    public Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default) =>
        FetchAsync(request, options: null, cancellationToken);

    /// <summary>Sends a request to this container TCP port.</summary>
    public async Task<Response> FetchAsync(
        Request request,
        FetchOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await DispatchAsync(
            "durable.container.tcpPort.fetch",
            new TcpPortFetchPayload(Port, FetchBindingRequest.From(request, options)),
            cancellationToken);

        return JsonSerializer.Deserialize<ResponseEnvelope>(result, JsonOptions)?.ToResponse(_invocationId, _dispatcher)
            ?? throw new WorkersException("Durable Object container TCP port returned an empty fetch response.");
    }

    /// <summary>Opens a TCP connection from this container TCP port.</summary>
    public async Task<Socket> ConnectAsync(string address, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var result = await DispatchAsync(
            "durable.container.tcpPort.connect",
            new TcpPortConnectPayload(Port, null, address),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<SocketHandleEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object container TCP port returned an empty socket result.");

        return new Socket(_invocationId, envelope.Handle, _dispatcher);
    }

    /// <summary>Opens a TCP connection from this container TCP port.</summary>
    public Task<Socket> ConnectAsync(
        SocketAddress address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        return ConnectAddressAsync(address, cancellationToken);
    }

    private async Task<Socket> ConnectAddressAsync(
        SocketAddress address,
        CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(
            "durable.container.tcpPort.connect",
            new TcpPortConnectPayload(Port, address, null),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<SocketHandleEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object container TCP port returned an empty socket result.");

        return new Socket(_invocationId, envelope.Handle, _dispatcher);
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

    private sealed record TcpPortFetchPayload(int Port, FetchBindingRequest Fetch);

    private sealed record TcpPortConnectPayload(int Port, SocketAddress? Address, string? AddressText);

    private sealed record SocketHandleEnvelope(string Handle);
}
