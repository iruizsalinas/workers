namespace Workers;

public sealed class Request
{
    public Request(string input, RequestInit? init = null) => WorkerApi.NotExecutable();
    public Request(Url input, RequestInit? init = null) => WorkerApi.NotExecutable();
    public Request(Request input, RequestInit? init = null) => WorkerApi.NotExecutable();
    /// <summary>Copies a request to a new URL, as <c>new Request(url, request)</c>.</summary>
    public Request(string input, Request init) => WorkerApi.NotExecutable();
    /// <summary>Copies a request to a new URL, as <c>new Request(url, request)</c>.</summary>
    public Request(Url input, Request init) => WorkerApi.NotExecutable();

    public string Method => WorkerApi.NotExecutable<string>();
    /// <summary>The absolute request URL. Parse it with <c>new Url(request.Url)</c> to read its parts.</summary>
    public string Url => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    public RedirectMode Redirect => WorkerApi.NotExecutable<RedirectMode>();
    public AbortSignal Signal => WorkerApi.NotExecutable<AbortSignal>();
    /// <summary>Cloudflare's properties of an incoming request; null for requests the Worker constructs.</summary>
    public IncomingRequestCfProperties? Cf => WorkerApi.NotExecutable<IncomingRequestCfProperties?>();
    public ReadableStream? Body => WorkerApi.NotExecutable<ReadableStream?>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();
    /// <summary>The cookies in the <c>Cookie</c> header.</summary>
    public Cookies Cookies => WorkerApi.NotExecutable<Cookies>();

    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    /// <summary>Reads UTF-8 text, limiting actual body bytes. A positive limit is required; overflow throws HttpError(413).</summary>
    public Task<string> TextAsync(int maxBytes) => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Reads bytes up to a positive limit; overflow cancels the body and throws HttpError(413).</summary>
    public Task<byte[]> BytesAsync(int maxBytes) => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Reads JSON. Malformed JSON throws HttpError(400, "Malformed JSON"); application code validates the resulting value.</summary>
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    /// <summary>Limits actual body bytes before parsing JSON. Overflow throws HttpError(413), malformed JSON throws HttpError(400).
    /// The limit must be positive. Content type and the parsed value remain application validation.</summary>
    public Task<T?> JsonAsync<T>(int maxBytes) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<FormData> FormDataAsync() => WorkerApi.NotExecutable<Task<FormData>>();
    public Task<Blob> BlobAsync() => WorkerApi.NotExecutable<Task<Blob>>();
    public Request Clone() => WorkerApi.NotExecutable<Request>();
}

public sealed class RequestInit
{
    public string? Method { get; init; }
    public Headers? Headers { get; init; }
    public BodyInit? Body { get; init; }
    public RedirectMode? Redirect { get; init; }
    public AbortSignal? Signal { get; init; }
    public RequestInitCfProperties? Cf { get; init; }
    public RequestCache? Cache { get; init; }
    /// <summary>A subresource integrity hash the response body must match.</summary>
    public string? Integrity { get; init; }
}

public enum RedirectMode
{
    Follow,
    Error,
    Manual
}

public enum RequestCache
{
    NoStore,
    NoCache
}

/// <summary>Cloudflare features applied to an outgoing subrequest.</summary>
public sealed class RequestInitCfProperties
{
    public bool? CacheEverything { get; init; }
    public string? CacheKey { get; init; }
    public IReadOnlyList<string>? CacheTags { get; init; }
    /// <summary>Seconds to cache the response, overriding origin headers; negative values bypass the cache.</summary>
    public int? CacheTtl { get; init; }
    /// <summary>Cache TTLs by status code or range, such as <c>"200-299"</c>.</summary>
    public IReadOnlyDictionary<string, int>? CacheTtlByStatus { get; init; }
    public string? ResolveOverride { get; init; }
    public bool? ScrapeShield { get; init; }
    public bool? Apps { get; init; }
    public bool? Mirage { get; init; }
    /// <summary><c>lossy</c>, <c>lossless</c> or <c>off</c>.</summary>
    public string? Polish { get; init; }
    /// <summary>Image Resizing options, such as <c>new { width = 800, fit = "scale-down" }</c>.</summary>
    public object? Image { get; init; }
}

/// <summary>Cloudflare's properties of an incoming request. Geolocation fields are null when unknown.</summary>
public sealed class IncomingRequestCfProperties
{
    private IncomingRequestCfProperties() { }

    public int? Asn => WorkerApi.NotExecutable<int?>();
    public string? AsOrganization => WorkerApi.NotExecutable<string?>();
    public string Colo => WorkerApi.NotExecutable<string>();
    public string? Country => WorkerApi.NotExecutable<string?>();
    public string? IsEUCountry => WorkerApi.NotExecutable<string?>();
    public string? Continent => WorkerApi.NotExecutable<string?>();
    public string? City => WorkerApi.NotExecutable<string?>();
    public string? PostalCode => WorkerApi.NotExecutable<string?>();
    public string? Latitude => WorkerApi.NotExecutable<string?>();
    public string? Longitude => WorkerApi.NotExecutable<string?>();
    public string? Timezone => WorkerApi.NotExecutable<string?>();
    public string? Region => WorkerApi.NotExecutable<string?>();
    public string? RegionCode => WorkerApi.NotExecutable<string?>();
    public string? MetroCode => WorkerApi.NotExecutable<string?>();
    public string HttpProtocol => WorkerApi.NotExecutable<string>();
    public string TlsVersion => WorkerApi.NotExecutable<string>();
    public string TlsCipher => WorkerApi.NotExecutable<string>();
    public int? ClientTcpRtt => WorkerApi.NotExecutable<int?>();
    public string RequestPriority => WorkerApi.NotExecutable<string>();
    /// <summary>Bot Management signals; null unless Bot Management is enabled for the zone.</summary>
    public IncomingRequestCfPropertiesBotManagementBase? BotManagement => WorkerApi.NotExecutable<IncomingRequestCfPropertiesBotManagementBase?>();
}

public sealed class IncomingRequestCfPropertiesBotManagementBase
{
    private IncomingRequestCfPropertiesBotManagementBase() { }

    public int Score => WorkerApi.NotExecutable<int>();
    public bool VerifiedBot => WorkerApi.NotExecutable<bool>();
    public bool CorporateProxy => WorkerApi.NotExecutable<bool>();
    public bool StaticResource => WorkerApi.NotExecutable<bool>();
    public IReadOnlyList<long> DetectionIds => WorkerApi.NotExecutable<IReadOnlyList<long>>();
}
