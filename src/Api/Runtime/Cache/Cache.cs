namespace Workers;

/// <summary>The Cache API's <c>caches</c> object.</summary>
public static class CacheStorage
{
    /// <summary>The zone's default cache, <c>caches.default</c>.</summary>
    public static Cache Default => WorkerApi.NotExecutable<Cache>();
    public static Task<Cache> OpenAsync(string cacheName) => WorkerApi.NotExecutable<Task<Cache>>();
}

/// <summary>A cache of responses keyed by GET request URL. Writes honor the response's caching headers.</summary>
public sealed class Cache
{
    private Cache() { }

    public Task PutAsync(string request, Response response) => WorkerApi.NotExecutable<Task>();
    public Task PutAsync(Request request, Response response) => WorkerApi.NotExecutable<Task>();
    /// <summary>The cached response, or null on a miss.</summary>
    public Task<Response?> MatchAsync(string request, CacheQueryOptions? options = null) => WorkerApi.NotExecutable<Task<Response?>>();
    public Task<Response?> MatchAsync(Request request, CacheQueryOptions? options = null) => WorkerApi.NotExecutable<Task<Response?>>();
    public Task<bool> DeleteAsync(string request, CacheQueryOptions? options = null) => WorkerApi.NotExecutable<Task<bool>>();
    public Task<bool> DeleteAsync(Request request, CacheQueryOptions? options = null) => WorkerApi.NotExecutable<Task<bool>>();
}

public sealed class CacheQueryOptions
{
    /// <summary>Treats a non-GET request as a GET when matching.</summary>
    public bool? IgnoreMethod { get; init; }
}
