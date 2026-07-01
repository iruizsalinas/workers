using System.Text;
using System.Text.Json;

namespace Workers;

/// <summary>A host and TCP port accepted by the Workers socket runtime.</summary>
public sealed record SocketAddress
{
    /// <summary>Creates a socket address.</summary>
    public SocketAddress(string hostname, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), port, "Socket port must be between 1 and 65535.");

        Hostname = hostname;
        Port = port;
    }

    /// <summary>The remote hostname.</summary>
    public string Hostname { get; }

    /// <summary>The remote TCP port.</summary>
    public int Port { get; }
}

/// <summary>Options used when opening a Workers TCP socket.</summary>
public sealed class SocketOptions
{
    /// <summary>Controls whether the socket starts with TLS, starts without TLS, or may later use StartTLS.</summary>
    public SocketSecureTransport? SecureTransport { get; init; }

    /// <summary>Controls whether the writable side remains open after readable EOF.</summary>
    public bool? AllowHalfOpen { get; init; }
}

/// <summary>Secure transport modes for Workers TCP sockets.</summary>
public enum SocketSecureTransport
{
    /// <summary>Do not use TLS.</summary>
    Off,

    /// <summary>Use TLS immediately.</summary>
    On,

    /// <summary>Start without TLS and allow upgrading with StartTLS.</summary>
    StartTls
}

/// <summary>Connection details returned when a Workers TCP socket opens.</summary>
public sealed record SocketInfo(string? RemoteAddress, string? LocalAddress);

/// <summary>A chunk read from a Workers TCP socket.</summary>
public sealed class SocketReadResult
{
    private readonly byte[] _bytes;

    private SocketReadResult(bool done, byte[] bytes)
    {
        Done = done;
        _bytes = bytes;
    }

    /// <summary>True when the socket readable side reached end-of-stream.</summary>
    public bool Done { get; }

    /// <summary>The bytes read from the socket. Empty when <see cref="Done"/> is true.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes.ToArray();

    /// <summary>Creates an end-of-stream result.</summary>
    public static SocketReadResult Completed { get; } = new(done: true, []);

    /// <summary>Creates a result containing bytes.</summary>
    public static SocketReadResult FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new SocketReadResult(done: false, bytes.ToArray());
    }
}

/// <summary>A handle to a Workers outbound TCP socket.</summary>
public sealed class Socket
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal Socket(string invocationId, string handle, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        Handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>The opaque platform handle for this socket.</summary>
    internal string Handle { get; }

    /// <summary>Waits until the socket connection is established.</summary>
    public async Task<SocketInfo> OpenedAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("socket.opened", new SocketHandleRequest(Handle), cancellationToken)
            ;

        return JsonSerializer.Deserialize<SocketInfo>(result, JsonOptions)
            ?? throw new WorkersException("Socket opened promise returned an empty result.");
    }

    /// <summary>Waits until the socket closes.</summary>
    public Task ClosedAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("socket.closed", new SocketHandleRequest(Handle), cancellationToken);

    /// <summary>Reads the next chunk from the socket readable stream.</summary>
    public async Task<SocketReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("socket.read", new SocketHandleRequest(Handle), cancellationToken)
            ;

        var envelope = JsonSerializer.Deserialize<SocketReadEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Socket read returned an empty result.");

        if (envelope.Done)
            return SocketReadResult.Completed;

        var bytes = envelope.BodyBase64 is null ? [] : Convert.FromBase64String(envelope.BodyBase64);
        return SocketReadResult.FromBytes(bytes);
    }

    /// <summary>Writes bytes to the socket writable stream.</summary>
    public Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
        DispatchAsync("socket.write", new SocketWriteRequest(Handle, Convert.ToBase64String(bytes.Span)), cancellationToken);

    /// <summary>Writes UTF-8 text to the socket writable stream.</summary>
    public Task WriteTextAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WriteAsync(Encoding.UTF8.GetBytes(value), cancellationToken);
    }

    /// <summary>Closes the writable side of the socket.</summary>
    public Task CloseWritableAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("socket.closeWritable", new SocketHandleRequest(Handle), cancellationToken);

    /// <summary>Closes the socket.</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("socket.close", new SocketHandleRequest(Handle), cancellationToken);

    /// <summary>Upgrades a StartTLS socket and returns the secure socket handle.</summary>
    public async Task<Socket> StartTlsAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync("socket.startTls", new SocketHandleRequest(Handle), cancellationToken)
            ;

        var envelope = JsonSerializer.Deserialize<SocketHandleEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Socket StartTLS returned an empty result.");

        return new Socket(_invocationId, envelope.Handle, _dispatcher);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            "$socket",
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private sealed record SocketHandleRequest(string Handle);

    private sealed record SocketWriteRequest(string Handle, string BodyBase64);

    private sealed record SocketReadEnvelope(bool Done, string? BodyBase64);

    private sealed record SocketHandleEnvelope(string Handle);
}

internal static class SocketFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task<Socket> ConnectAsync(
        string invocationId,
        string address,
        SocketOptions? options,
        IBindingDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var payload = new SocketConnectRequest(null, address, SocketOptionsEnvelope.From(options));
        return ConnectCoreAsync(invocationId, payload, dispatcher, cancellationToken);
    }

    public static async Task<Socket> ConnectAsync(
        string invocationId,
        SocketAddress address,
        SocketOptions? options,
        IBindingDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(dispatcher);

        var payload = new SocketConnectRequest(address, null, SocketOptionsEnvelope.From(options));
        return await ConnectCoreAsync(invocationId, payload, dispatcher, cancellationToken);
    }

    private static async Task<Socket> ConnectCoreAsync(
        string invocationId,
        SocketConnectRequest payload,
        IBindingDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            invocationId,
            "$socket",
            "socket.connect",
            JsonSerializer.Serialize(payload, JsonOptions));

        var result = await dispatcher.DispatchAsync(invocation, cancellationToken);
        var envelope = JsonSerializer.Deserialize<SocketHandleEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Socket connect returned an empty result.");

        return new Socket(invocationId, envelope.Handle, dispatcher);
    }

    private sealed record SocketConnectRequest(
        SocketAddress? Address,
        string? AddressText,
        SocketOptionsEnvelope? Options);

    private sealed record SocketOptionsEnvelope(string? SecureTransport, bool? AllowHalfOpen)
    {
        public static SocketOptionsEnvelope? From(SocketOptions? options)
        {
            if (options is null)
                return null;

            var envelope = new SocketOptionsEnvelope(SecureTransportName(options.SecureTransport), options.AllowHalfOpen);
            return envelope.SecureTransport is null && envelope.AllowHalfOpen is null ? null : envelope;
        }

        private static string? SecureTransportName(SocketSecureTransport? secureTransport) =>
            secureTransport switch
            {
                null => null,
                SocketSecureTransport.Off => "off",
                SocketSecureTransport.On => "on",
                SocketSecureTransport.StartTls => "starttls",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(secureTransport),
                    secureTransport,
                    "Unsupported socket secure transport mode.")
            };
    }

    private sealed record SocketHandleEnvelope(string Handle);
}
