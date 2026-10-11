namespace Workers;

public sealed class RawBinding : IBinding
{
    public Task<JsonValue> GetPropertyAsync(
        string propertyName) =>
        WorkerApi.NotExecutable<Task<JsonValue>>();
    public Task<T?> GetPropertyAsync<T>(string propertyName) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<JsonValue> InvokeAsync(
        string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task<JsonValue>>();
    public Task<T?> InvokeAsync<T>(
        string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task<T?>>();
    public Task InvokeVoidAsync(
        string methodName, IEnumerable<object?>? arguments = null) =>
        WorkerApi.NotExecutable<Task>();
}
