namespace Workers;

/// <summary>
/// A binding that receives requests: a service binding, an assets binding, an mTLS certificate or a Durable Object stub.
/// RPC methods are called by name; arguments and results are structured clones.
/// </summary>
public class Fetcher
{
    private protected Fetcher() { }

    public Task<Response> FetchAsync(string input, RequestInit? init = null) => WorkerApi.NotExecutable<Task<Response>>();
    public Task<Response> FetchAsync(Url input, RequestInit? init = null) => WorkerApi.NotExecutable<Task<Response>>();
    public Task<Response> FetchAsync(Request input, RequestInit? init = null) => WorkerApi.NotExecutable<Task<Response>>();
    public Socket Connect(string address, SocketOptions? options = null) => WorkerApi.NotExecutable<Socket>();
    public Socket Connect(SocketAddress address, SocketOptions? options = null) => WorkerApi.NotExecutable<Socket>();

    public Task<JsonValue> InvokeAsync(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<JsonValue>>();
    /// <summary>Calls an RPC method and maps its result to a C# class or record.</summary>
    public Task<TResult?> InvokeAsync<TResult>(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<TResult?>>();
    /// <summary>Calls an RPC method that returns a stub, such as an RpcTarget or a function.</summary>
    public Task<RpcStub> InvokeStubAsync(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<RpcStub>>();
}

/// <summary>A Workers for Platforms dispatch namespace.</summary>
public sealed class DispatchNamespace
{
    private DispatchNamespace() { }

    public Fetcher Get(string name) => WorkerApi.NotExecutable<Fetcher>();
}
