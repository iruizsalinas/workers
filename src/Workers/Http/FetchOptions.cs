namespace Workers;

/// <summary>Options for Worker fetch operations.</summary>
public sealed class FetchOptions
{
    /// <summary>An abort signal for cancelling the platform request.</summary>
    public AbortSignal? Signal { get; init; }

    /// <summary>The request mode for the fetch request.</summary>
    public RequestMode? Mode { get; init; }

    /// <summary>The credentials mode for the fetch request.</summary>
    public RequestCredentials? Credentials { get; init; }

    /// <summary>The referrer for the fetch request.</summary>
    public string? Referrer { get; init; }

    /// <summary>The referrer policy for the fetch request.</summary>
    public ReferrerPolicy? ReferrerPolicy { get; init; }

    /// <summary>The redirect handling mode for the fetch request.</summary>
    public RequestRedirect? Redirect { get; init; }

    /// <summary>The cache mode for the fetch request.</summary>
    public RequestCache? Cache { get; init; }

    /// <summary>Subresource integrity metadata for the fetch request.</summary>
    public string? Integrity { get; init; }

    /// <summary>Sets the Fetch keepalive flag for the request.</summary>
    public bool? KeepAlive { get; init; }

    /// <summary>Cloudflare-specific request options for this fetch.</summary>
    public FetchCfOptions? Cf { get; init; }
}

/// <summary>Request modes for Worker fetch requests.</summary>
public enum RequestMode
{
    /// <summary>Permit cross-origin requests using CORS.</summary>
    Cors,

    /// <summary>Restrict the request to the same origin.</summary>
    SameOrigin,

    /// <summary>Allow a request without CORS.</summary>
    NoCors,

    /// <summary>Use navigation request semantics.</summary>
    Navigate
}

/// <summary>Credentials modes for Worker fetch requests.</summary>
public enum RequestCredentials
{
    /// <summary>Exclude credentials from the request.</summary>
    Omit,

    /// <summary>Include credentials only for same-origin requests.</summary>
    SameOrigin,

    /// <summary>Include credentials with the request.</summary>
    Include
}

/// <summary>Referrer policies for Worker fetch requests.</summary>
public enum ReferrerPolicy
{
    /// <summary>Do not send a referrer.</summary>
    NoReferrer,

    /// <summary>Send a referrer unless navigating from HTTPS to HTTP.</summary>
    NoReferrerWhenDowngrade,

    /// <summary>Send only the origin as the referrer.</summary>
    Origin,

    /// <summary>Send the full referrer to same-origin destinations and only the origin cross-origin.</summary>
    OriginWhenCrossOrigin,

    /// <summary>Send a referrer only for same-origin destinations.</summary>
    SameOrigin,

    /// <summary>Send only the origin, and do not send it from HTTPS to HTTP.</summary>
    StrictOrigin,

    /// <summary>Send the full referrer same-origin, only the origin cross-origin, and nothing from HTTPS to HTTP.</summary>
    StrictOriginWhenCrossOrigin,

    /// <summary>Send the full URL as the referrer.</summary>
    UnsafeUrl
}

/// <summary>Redirect handling modes for Worker fetch requests.</summary>
public enum RequestRedirect
{
    /// <summary>Follow redirects automatically.</summary>
    Follow,

    /// <summary>Reject when the request receives a redirect.</summary>
    Error,

    /// <summary>Return the redirect response without following it.</summary>
    Manual
}

/// <summary>Cache modes for Worker fetch requests.</summary>
public enum RequestCache
{
    /// <summary>Bypass all caches.</summary>
    NoStore,

    /// <summary>Revalidate with the origin before using a cached response.</summary>
    NoCache,

    /// <summary>Reload from the origin and update the cache.</summary>
    Reload,

    /// <summary>Use a cached response when available, otherwise fetch from the origin.</summary>
    ForceCache,

    /// <summary>Only use a cached response. Requires <see cref="FetchOptions.Mode"/> to be <see cref="RequestMode.SameOrigin"/>.</summary>
    OnlyIfCached
}

/// <summary>Cloudflare-specific cache options for Worker fetch requests.</summary>
public sealed class FetchCfOptions
{
    /// <summary>Controls Cloudflare Apps for this request.</summary>
    public bool? Apps { get; init; }

    /// <summary>Forces Cloudflare to cache the response regardless of origin cache headers.</summary>
    public bool? CacheEverything { get; init; }

    /// <summary>Overrides the cache key used for this request.</summary>
    public string? CacheKey { get; init; }

    /// <summary>Overrides the cache TTL, in seconds. Must be zero or positive.</summary>
    public int? CacheTtl { get; init; }

    /// <summary>Overrides cache TTLs by response status code or status-code range.</summary>
    public IReadOnlyDictionary<string, int>? CacheTtlByStatus { get; init; }

    /// <summary>Cloudflare image transformation options for this request.</summary>
    public FetchImageOptions? Image { get; init; }

    /// <summary>Controls Cloudflare Auto Minify for this request.</summary>
    public FetchMinifyOptions? Minify { get; init; }

    /// <summary>Controls Mirage for this request.</summary>
    public bool? Mirage { get; init; }

    /// <summary>Controls Polish for this request.</summary>
    public FetchPolish? Polish { get; init; }

    /// <summary>Overrides DNS resolution for this request within the same Cloudflare zone.</summary>
    public string? ResolveOverride { get; init; }

    /// <summary>Controls Scrape Shield for this request.</summary>
    public bool? ScrapeShield { get; init; }
}

/// <summary>Cloudflare Auto Minify options for fetch requests.</summary>
public sealed class FetchMinifyOptions
{
    /// <summary>Minifies JavaScript responses.</summary>
    public bool Js { get; init; }

    /// <summary>Minifies HTML responses.</summary>
    public bool Html { get; init; }

    /// <summary>Minifies CSS responses.</summary>
    public bool Css { get; init; }
}

/// <summary>Cloudflare Polish modes for fetch requests.</summary>
public enum FetchPolish
{
    /// <summary>Disables Polish.</summary>
    Off,

    /// <summary>Uses lossy optimization.</summary>
    Lossy,

    /// <summary>Uses lossless optimization.</summary>
    Lossless
}
