namespace Workers;

/// <summary>An outbound TCP socket from <c>connect()</c> in <c>cloudflare:sockets</c>.</summary>
public sealed class Socket
{
    private Socket() { }

    /// <summary>Opens a connection to an address such as <c>example.com:443</c>.</summary>
    public static Socket Connect(string address, SocketOptions? options = null) => WorkerApi.NotExecutable<Socket>();
    public static Socket Connect(SocketAddress address, SocketOptions? options = null) => WorkerApi.NotExecutable<Socket>();

    public ReadableStream Readable => WorkerApi.NotExecutable<ReadableStream>();
    public WritableStream Writable => WorkerApi.NotExecutable<WritableStream>();
    public Task Closed => WorkerApi.NotExecutable<Task>();
    public Task<SocketInfo> Opened => WorkerApi.NotExecutable<Task<SocketInfo>>();
    public bool Upgraded => WorkerApi.NotExecutable<bool>();
    public SecureTransport SecureTransport => WorkerApi.NotExecutable<SecureTransport>();

    public Task CloseAsync() => WorkerApi.NotExecutable<Task>();
    /// <summary>Upgrades a socket opened with <see cref="SecureTransport.StartTls"/> to TLS.</summary>
    public Socket StartTls() => WorkerApi.NotExecutable<Socket>();
}

public sealed class SocketAddress
{
    public required string Hostname { get; init; }
    public required int Port { get; init; }
}

public sealed class SocketOptions
{
    public SecureTransport? SecureTransport { get; init; }
    public bool? AllowHalfOpen { get; init; }
}

public enum SecureTransport
{
    Off,
    On,
    StartTls
}

public sealed class SocketInfo
{
    private SocketInfo() { }

    public string? RemoteAddress => WorkerApi.NotExecutable<string?>();
    public string? LocalAddress => WorkerApi.NotExecutable<string?>();
}
