namespace Workers;

public sealed record ContainerStartOptions
{
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    public IReadOnlyList<string>? Entrypoint { get; init; }
    public bool? EnableInternet { get; init; }
}

public sealed record ContainerExecOptions
{
    public string? Cwd { get; init; }
    public IReadOnlyDictionary<string, string>? Env { get; init; }
    public string? User { get; init; }
    public string? Stdin { get; init; }
}

public sealed class ContainerExecOutput
{
    public byte[] Stdout => WorkerApi.NotExecutable<byte[]>();
    public byte[] Stderr => WorkerApi.NotExecutable<byte[]>();
    public int ExitCode => WorkerApi.NotExecutable<int>();
}

public sealed class DurableObjectContainer
{
    public Task<bool> GetRunningAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<bool>>();
    public Task StartAsync(ContainerStartOptions? options = null, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task DestroyAsync(string? error = null, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task SignalAsync(int signal, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task MonitorAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task InterceptOutboundHttpAsync(
        string target, IServiceBinding worker, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task>();
    public Task InterceptAllOutboundHttpAsync(IServiceBinding worker, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task InterceptOutboundHttpsAsync(IServiceBinding worker, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public ContainerTcpPort GetTcpPort(int port) => WorkerApi.NotExecutable<ContainerTcpPort>();
    public Task<ContainerExecProcess> ExecAsync(
        IEnumerable<string> command, ContainerExecOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<ContainerExecProcess>>();
}

public sealed class ContainerTcpPort
{
    public int Port => WorkerApi.NotExecutable<int>();

    public Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<Response>>();
    public Task<TcpSocket> ConnectAsync(string address, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<TcpSocket>>();
}

public sealed class ContainerExecProcess : IAsyncDisposable
{
    public int Pid => WorkerApi.NotExecutable<int>();

    public Task<int> GetExitCodeAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<int>>();
    public Task<ContainerExecOutput> OutputAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<ContainerExecOutput>>();
    public Task KillAsync(int? signal = null, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public ValueTask DisposeAsync() => WorkerApi.NotExecutable<ValueTask>();
}
