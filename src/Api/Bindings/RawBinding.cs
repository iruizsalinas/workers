namespace Workers;

public sealed class RawBinding : IBinding
{
    public Task<JsonElement> GetPropertyAsync(
        string propertyName, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<JsonElement>>();
    public Task<T?> GetPropertyAsync<T>(string propertyName, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<JsonElement> InvokeAsync(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<JsonElement>>();
    public Task<T?> InvokeAsync<T>(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<T?>>();
    public Task InvokeVoidAsync(
        string methodName, IEnumerable<object?>? arguments = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task>();
}
