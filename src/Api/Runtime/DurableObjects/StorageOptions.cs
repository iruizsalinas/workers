namespace Workers;

public record DurableObjectStorageReadOptions
{
    public bool? AllowConcurrency { get; init; }
    public bool? NoCache { get; init; }
}

public record DurableObjectStorageWriteOptions : DurableObjectStorageReadOptions
{
    public bool? AllowUnconfirmed { get; init; }
}

public sealed record DurableObjectStorageListOptions : DurableObjectStorageReadOptions
{
    public string? Start { get; init; }
    public string? StartAfter { get; init; }
    public string? End { get; init; }
    public string? Prefix { get; init; }
    public bool? Reverse { get; init; }
    public int? Limit { get; init; }
}

public sealed class DurableObjectTransaction
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
    public Task PutAsync<T>(string key, T value, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task>();
    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<bool>>();
    public Task<IReadOnlyDictionary<string, T>> ListAsync<T>(
        DurableObjectStorageListOptions? options = null, CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T>>>();
    public void Rollback() => WorkerApi.NotExecutable();
}
