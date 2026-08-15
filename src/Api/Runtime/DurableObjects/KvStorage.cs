namespace Workers;

public sealed class DurableObjectKvStorage
{
    public Task<T?> GetJsonAsync<T>(string key, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
    public Task PutJsonAsync<T>(string key, T value, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<bool>>();
    public Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectKvListOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T?>>>();
}

public sealed record DurableObjectKvListOptions
{
    public string? Prefix { get; init; }
    public int? Limit { get; init; }
}
