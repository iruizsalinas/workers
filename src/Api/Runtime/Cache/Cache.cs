namespace Workers;

public sealed class CacheQueryOptions
{
    public bool IgnoreMethod { get; init; }
}

public enum CacheDeleteResult
{
    NotFound,
    Deleted
}

public static class CacheStorage
{
    public static ICache Default => WorkerApi.NotExecutable<ICache>();

    public static Task<ICache> OpenAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        WorkerApi.NotExecutable<Task<ICache>>();
}

public interface ICache
{
    Task PutAsync(string url, Response response, CancellationToken cancellationToken = default);
    Task PutAsync(Request request, Response response, CancellationToken cancellationToken = default);
    Task<Response?> MatchAsync(string url, CacheQueryOptions? options = null, CancellationToken cancellationToken = default);
    Task<Response?> MatchAsync(Request request, CacheQueryOptions? options = null, CancellationToken cancellationToken = default);
    Task<Response?> MatchAsync(string url, bool ignoreMethod, CancellationToken cancellationToken = default);
    Task<Response?> MatchAsync(Request request, bool ignoreMethod, CancellationToken cancellationToken = default);
    Task<CacheDeleteResult> DeleteAsync(string url, CacheQueryOptions? options = null, CancellationToken cancellationToken = default);
    Task<CacheDeleteResult> DeleteAsync(Request request, CacheQueryOptions? options = null, CancellationToken cancellationToken = default);
}
