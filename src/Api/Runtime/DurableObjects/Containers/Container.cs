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
    public bool Running => WorkerApi.NotExecutable<bool>();
    public void Start(ContainerStartOptions? options = null) => WorkerApi.NotExecutable();
    public Task DestroyAsync(string? error = null) => WorkerApi.NotExecutable<Task>();
    public void Signal(int signal) => WorkerApi.NotExecutable();
    public Task MonitorAsync() => WorkerApi.NotExecutable<Task>();
    public Task InterceptOutboundHttpAsync(string target, IServiceBinding worker) => WorkerApi.NotExecutable<Task>();
    public Task InterceptAllOutboundHttpAsync(IServiceBinding worker) => WorkerApi.NotExecutable<Task>();
    public Task InterceptOutboundHttpsAsync(string target, IServiceBinding worker) => WorkerApi.NotExecutable<Task>();
    public ContainerTcpPort GetTcpPort(int port) => WorkerApi.NotExecutable<ContainerTcpPort>();
    public Task<ContainerExecProcess> ExecAsync(IEnumerable<string> command, ContainerExecOptions? options = null) =>
        WorkerApi.NotExecutable<Task<ContainerExecProcess>>();
}

public sealed class ContainerTcpPort
{
    public Task<Response> FetchAsync(string url, FetchOptions? options = null) => WorkerApi.NotExecutable<Task<Response>>();
    public TcpSocket Connect(string address) => WorkerApi.NotExecutable<TcpSocket>();
}

public sealed class ContainerExecProcess
{
    public int Pid => WorkerApi.NotExecutable<int>();
    public Task<int> ExitCode => WorkerApi.NotExecutable<Task<int>>();
    public Task<ContainerExecOutput> OutputAsync() => WorkerApi.NotExecutable<Task<ContainerExecOutput>>();
    public void Kill(int? signal = null) => WorkerApi.NotExecutable();
}
