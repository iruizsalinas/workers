namespace Workers;

public sealed class RpcStub : IDisposable
{
    public Task<JsonValue> InvokeAsync(string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task<JsonValue>>();
    public Task<TResult?> InvokeAsync<TResult>(string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task<TResult?>>();
    public Task<RpcStub> InvokeStubAsync(string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task<RpcStub>>();
    public Task InvokeVoidAsync(string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task>();
    public RpcStub Dup() => WorkerApi.NotExecutable<RpcStub>();
    public void Dispose() => WorkerApi.NotExecutable();
}
