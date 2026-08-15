namespace Workers;

public sealed class RpcStub : IAsyncDisposable
{
    public Task<JsonElement> InvokeAsync(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<JsonElement>>();
    public Task<TResult?> InvokeAsync<TResult>(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<TResult?>>();
    public Task<RpcStub> InvokeStubAsync(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<RpcStub>>();
    public Task<JsonElement> CallAsync(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<JsonElement>>();
    public Task<TResult?> CallAsync<TResult>(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<TResult?>>();
    public Task<RpcStub> CallStubAsync(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<RpcStub>>();
    public Task<RpcStub> DuplicateAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<RpcStub>>();
    public ValueTask DisposeAsync() => WorkerApi.NotExecutable<ValueTask>();
}
