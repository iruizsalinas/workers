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

/// <summary>Purges the zone's cache, as <c>ctx.cache</c>. Available only when deployed on a zone.</summary>
public sealed class CacheContext
{
    private CacheContext() { }

    /// <summary>Removes matching content from the cache.</summary>
    public Task<CachePurgeResult> PurgeAsync(CachePurgeOptions options) => WorkerApi.NotExecutable<Task<CachePurgeResult>>();
    /// <summary>Marks matching content stale, so it is revalidated with the origin on its next request.</summary>
    public Task<CachePurgeResult> InvalidateAsync(CachePurgeOptions options) => WorkerApi.NotExecutable<Task<CachePurgeResult>>();
}

/// <summary>What to purge: cache tags, path prefixes, or everything.</summary>
public sealed class CachePurgeOptions
{
    public IReadOnlyList<string>? Tags { get; init; }
    /// <summary>Path prefixes, such as <c>/images/</c>.</summary>
    public IReadOnlyList<string>? PathPrefixes { get; init; }
    public bool? PurgeEverything { get; init; }
}

public sealed class CachePurgeResult
{
    private CachePurgeResult() { }

    public bool Success => WorkerApi.NotExecutable<bool>();
    public IReadOnlyList<CachePurgeError> Errors => WorkerApi.NotExecutable<IReadOnlyList<CachePurgeError>>();
}

public sealed class CachePurgeError
{
    private CachePurgeError() { }

    public int Code => WorkerApi.NotExecutable<int>();
    public string Message => WorkerApi.NotExecutable<string>();
}
