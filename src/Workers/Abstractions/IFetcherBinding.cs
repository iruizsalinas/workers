namespace Workers;

/// <summary>Represents a binding that can fetch requests.</summary>
public interface IFetcherBinding : IBinding
{
    /// <summary>Sends a GET request through the bound fetcher.</summary>
    Task<Response> FetchAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>Sends a GET request through the bound fetcher.</summary>
    Task<Response> FetchAsync(
        string url,
        FetchOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a request through the bound fetcher.</summary>
    Task<Response> FetchAsync(Request request, CancellationToken cancellationToken = default);

    /// <summary>Sends a request through the bound fetcher.</summary>
    Task<Response> FetchAsync(
        Request request,
        FetchOptions? options,
        CancellationToken cancellationToken = default);
}
