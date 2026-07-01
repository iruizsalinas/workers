using System.Text.Json;

namespace Workers;

/// <summary>Connection metadata for a Workers Hyperdrive binding.</summary>
public sealed record HyperdriveConnectionInfo(
    string ConnectionString,
    string Host,
    ushort Port,
    string User,
    string Password,
    string Database);

internal sealed class HyperdriveBinding : IHyperdriveBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public HyperdriveBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<HyperdriveConnectionInfo> GetConnectionInfoAsync(CancellationToken cancellationToken = default)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "hyperdrive.connectionInfo",
            "{}");

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<HyperdriveConnectionInfo>(result, JsonOptions)
            ?? throw new WorkersException("Hyperdrive binding returned an empty result.");
    }

    public async Task<Socket> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "hyperdrive.connect",
            "{}");

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<SocketHandleEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Hyperdrive connect returned an empty result.");

        return new Socket(_invocationId, envelope.Handle, _dispatcher);
    }

    private sealed record SocketHandleEnvelope(string Handle);
}
