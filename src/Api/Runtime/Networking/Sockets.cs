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
public sealed record TcpReadResult(bool Done, ReadOnlyMemory<byte> Bytes);
public sealed class TcpSocket
{
    public static TcpSocket Connect(string address, TcpSocketOptions? options = null) =>
        WorkerApi.NotExecutable<TcpSocket>();
    public static TcpSocket Connect(string hostname, int port, TcpSocketOptions? options = null) =>
        WorkerApi.NotExecutable<TcpSocket>();
    public static TcpSocket Connect(TcpSocketAddress address, TcpSocketOptions? options = null) =>
        WorkerApi.NotExecutable<TcpSocket>();

    public Task<TcpSocketInfo> OpenedAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<TcpSocketInfo>>();
    public Task ClosedAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<TcpReadResult> ReadAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<TcpReadResult>>();
    public Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task WriteTextAsync(string value, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task CloseWritableAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task CloseAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<TcpSocket> StartTlsAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<TcpSocket>>();
}
