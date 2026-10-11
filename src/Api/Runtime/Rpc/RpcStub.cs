namespace Workers;

/// <summary>A stub for an RPC target returned by another Worker. Dispose it when it is no longer needed.</summary>
public sealed class RpcStub : IDisposable
{
    private RpcStub() { }

    public Task<JsonValue> InvokeAsync(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<JsonValue>>();
    /// <summary>Calls an RPC method and maps its result to a C# class or record.</summary>
    public Task<TResult?> InvokeAsync<TResult>(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<TResult?>>();
    public Task<RpcStub> InvokeStubAsync(string methodName, params object?[] arguments) => WorkerApi.NotExecutable<Task<RpcStub>>();
    public RpcStub Dup() => WorkerApi.NotExecutable<RpcStub>();
    public void Dispose() => WorkerApi.NotExecutable();
}
