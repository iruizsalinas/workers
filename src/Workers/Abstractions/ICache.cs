namespace Workers;

/// <summary>Represents a Workers Cache instance.</summary>
public interface ICache : IBinding
{
    /// <summary>Stores a response in cache by URL.</summary>
    Task PutAsync(string url, Response response, CancellationToken cancellationToken = default);

    /// <summary>Stores a response in cache by request.</summary>
    Task PutAsync(Request request, Response response, CancellationToken cancellationToken = default);

    /// <summary>Looks up a cached response by URL.</summary>
    Task<Response?> MatchAsync(string url, bool ignoreMethod = false, CancellationToken cancellationToken = default);

    /// <summary>Looks up a cached response by URL.</summary>
    Task<Response?> MatchAsync(string url, CacheQueryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Looks up a cached response by request.</summary>
    Task<Response?> MatchAsync(Request request, bool ignoreMethod = false, CancellationToken cancellationToken = default);

    /// <summary>Looks up a cached response by request.</summary>
    Task<Response?> MatchAsync(Request request, CacheQueryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Deletes a cached response by URL.</summary>
    Task<CacheDeleteResult> DeleteAsync(string url, bool ignoreMethod = false, CancellationToken cancellationToken = default);

    /// <summary>Deletes a cached response by URL.</summary>
    Task<CacheDeleteResult> DeleteAsync(string url, CacheQueryOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Deletes a cached response by request.</summary>
    Task<CacheDeleteResult> DeleteAsync(Request request, bool ignoreMethod = false, CancellationToken cancellationToken = default);

    /// <summary>Deletes a cached response by request.</summary>
    Task<CacheDeleteResult> DeleteAsync(Request request, CacheQueryOptions? options, CancellationToken cancellationToken = default);
}
