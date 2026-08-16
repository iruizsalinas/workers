namespace Workers;

public sealed class DurableObjectKvStorage
{
    public T? Get<T>(string key) => WorkerApi.NotExecutable<T?>();
    public void Put<T>(string key, T value) => WorkerApi.NotExecutable();
    public bool Delete(string key) => WorkerApi.NotExecutable<bool>();
    public IReadOnlyDictionary<string, T?> List<T>(DurableObjectKvListOptions? options = null) =>
        WorkerApi.NotExecutable<IReadOnlyDictionary<string, T?>>();
}

public sealed record DurableObjectKvListOptions
{
    public string? Prefix { get; init; }
    public int? Limit { get; init; }
}
