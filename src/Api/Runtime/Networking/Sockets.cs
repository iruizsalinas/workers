namespace Workers;

public sealed record TcpSocketAddress(string Hostname, int Port);
public sealed class TcpSocketOptions
{
    public TcpSecureTransport? SecureTransport { get; init; }
    public bool? AllowHalfOpen { get; init; }
}

public enum TcpSecureTransport
{
    Off,
    On,
    StartTls
}

public sealed record TcpSocketInfo(string? RemoteAddress, string? LocalAddress);
public sealed class TcpSocket
{
    public static TcpSocket Connect(string address, TcpSocketOptions? options = null) =>
        WorkerApi.NotExecutable<TcpSocket>();
    public static TcpSocket Connect(string hostname, int port, TcpSocketOptions? options = null) =>
        WorkerApi.NotExecutable<TcpSocket>();
    public static TcpSocket Connect(TcpSocketAddress address, TcpSocketOptions? options = null) =>
        WorkerApi.NotExecutable<TcpSocket>();

    public Task<TcpSocketInfo> Opened => WorkerApi.NotExecutable<Task<TcpSocketInfo>>();
    public Task Closed => WorkerApi.NotExecutable<Task>();
    public ReadableStream Readable => WorkerApi.NotExecutable<ReadableStream>();
    public WritableStream Writable => WorkerApi.NotExecutable<WritableStream>();
    public bool Upgraded => WorkerApi.NotExecutable<bool>();
    public Task CloseAsync() => WorkerApi.NotExecutable<Task>();
    public TcpSocket StartTls() => WorkerApi.NotExecutable<TcpSocket>();
}
