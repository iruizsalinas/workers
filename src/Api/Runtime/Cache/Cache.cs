namespace Workers;

public sealed class CacheQueryOptions
{
    public bool IgnoreMethod { get; init; }
}

public static class CacheStorage
{
    public static ICache Default => WorkerApi.NotExecutable<ICache>();

    public static Task<ICache> OpenAsync(
        string name) =>
        WorkerApi.NotExecutable<Task<ICache>>();
}

public interface ICache
{
    Task PutAsync(string url, Response response);
    Task PutAsync(Request request, Response response);
    Task<Response?> MatchAsync(string url, CacheQueryOptions? options = null);
    Task<Response?> MatchAsync(Request request, CacheQueryOptions? options = null);
    Task<bool> DeleteAsync(string url, CacheQueryOptions? options = null);
    Task<bool> DeleteAsync(Request request, CacheQueryOptions? options = null);
}
