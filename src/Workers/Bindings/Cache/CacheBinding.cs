using System.Text.Json;
using System.Text.Json.Serialization;
using Workers.Interop;

namespace Workers;

/// <summary>The outcome of deleting a cached response.</summary>
public enum CacheDeleteResult
{
    /// <summary>A cached response was found and deleted.</summary>
    Deleted,

    /// <summary>No cached response matched the request.</summary>
    NotFound
}

internal sealed partial class CacheBinding : ICache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CacheBindingJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public CacheBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task PutAsync(string url, Response response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ValidateKeyUrl(url);
        ValidatePut(response);

        return DispatchAsync(
            "cache.put",
            JsonSerializer.Serialize(new CachePutPayload
            {
                Key = new CacheKeyPayload { Url = url },
                Response = ResponseEnvelope.FromResponse(response)
            }, JsonContext.CachePutPayload),
            cancellationToken);
    }

    public Task PutAsync(Request request, Response response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ValidatePut(request, response);

        return DispatchAsync(
            "cache.put",
            JsonSerializer.Serialize(new CachePutPayload
            {
                Key = new CacheKeyPayload { Request = RequestEnvelope.FromRequest(request) },
                Response = ResponseEnvelope.FromResponse(response)
            }, JsonContext.CachePutPayload),
            cancellationToken);
    }

    public Task<Response?> MatchAsync(string url, bool ignoreMethod = false, CancellationToken cancellationToken = default)
    {
        return MatchAsync(url, ToOptions(ignoreMethod), cancellationToken);
    }

    public Task<Response?> MatchAsync(string url, CacheQueryOptions? options, CancellationToken cancellationToken = default)
    {
        ValidateKeyUrl(url);
        return MatchAsync(new CacheKeyPayload { Url = url }, options, cancellationToken);
    }

    public Task<Response?> MatchAsync(Request request, bool ignoreMethod = false, CancellationToken cancellationToken = default)
    {
        return MatchAsync(request, ToOptions(ignoreMethod), cancellationToken);
    }

    public Task<Response?> MatchAsync(Request request, CacheQueryOptions? options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKeyUrl(request.Url);
        return MatchAsync(new CacheKeyPayload { Request = RequestEnvelope.FromRequest(request) }, options, cancellationToken);
    }

    public Task<CacheDeleteResult> DeleteAsync(string url, bool ignoreMethod = false, CancellationToken cancellationToken = default)
    {
        return DeleteAsync(url, ToOptions(ignoreMethod), cancellationToken);
    }

    public Task<CacheDeleteResult> DeleteAsync(string url, CacheQueryOptions? options, CancellationToken cancellationToken = default)
    {
        ValidateKeyUrl(url);
        return DeleteAsync(new CacheKeyPayload { Url = url }, options, cancellationToken);
    }

    public Task<CacheDeleteResult> DeleteAsync(Request request, bool ignoreMethod = false, CancellationToken cancellationToken = default)
    {
        return DeleteAsync(request, ToOptions(ignoreMethod), cancellationToken);
    }

    public Task<CacheDeleteResult> DeleteAsync(Request request, CacheQueryOptions? options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKeyUrl(request.Url);
        return DeleteAsync(new CacheKeyPayload { Request = RequestEnvelope.FromRequest(request) }, options, cancellationToken);
    }

    private async Task<Response?> MatchAsync(
        CacheKeyPayload key,
        CacheQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(
            "cache.match",
            JsonSerializer.Serialize(
                new CacheQueryPayload { Key = key, Options = CacheQueryOptionsEnvelope.From(options) },
                JsonContext.CacheQueryPayload),
            cancellationToken);

        if (string.Equals(result, "null", StringComparison.Ordinal))
            return null;

        return JsonSerializer.Deserialize(result, JsonContext.ResponseEnvelope)?.ToResponse(_invocationId, _dispatcher);
    }

    private async Task<CacheDeleteResult> DeleteAsync(
        CacheKeyPayload key,
        CacheQueryOptions? options,
        CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(
            "cache.delete",
            JsonSerializer.Serialize(
                new CacheQueryPayload { Key = key, Options = CacheQueryOptionsEnvelope.From(options) },
                JsonContext.CacheQueryPayload),
            cancellationToken);

        var deleted = JsonSerializer.Deserialize(result, JsonContext.CacheDeletePayload)?.Deleted ?? false;
        return deleted ? CacheDeleteResult.Deleted : CacheDeleteResult.NotFound;
    }

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static CacheQueryOptions? ToOptions(bool ignoreMethod) =>
        ignoreMethod ? new CacheQueryOptions { IgnoreMethod = true } : null;

    private static void ValidatePut(Request request, Response response)
    {
        ValidateKeyUrl(request.Url);

        if (!string.Equals(request.Method, "GET", StringComparison.Ordinal))
            throw new ArgumentException("The Workers Cache API only accepts GET request keys for cache.put.", nameof(request));

        ValidatePut(response);
    }

    private static void ValidateKeyUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            throw new ArgumentException("The Workers Cache API only accepts absolute HTTP or HTTPS URL keys.", nameof(url));

        ValidateKeyUrl(parsed);
    }

    private static void ValidateKeyUrl(Uri url)
    {
        if (url.Scheme is not ("http" or "https"))
            throw new ArgumentException("The Workers Cache API only accepts absolute HTTP or HTTPS URL keys.", nameof(url));
    }

    private static void ValidatePut(Response response)
    {
        if (response.Status == 206)
            throw new ArgumentException("The Workers Cache API cannot store 206 Partial Content responses.", nameof(response));

        foreach (var vary in response.Headers.GetAll("vary"))
        {
            var fields = vary.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (fields.Any(static field => string.Equals(field, "*", StringComparison.Ordinal)))
                throw new ArgumentException("The Workers Cache API cannot store responses with a 'Vary: *' header.", nameof(response));
        }
    }

    private sealed class CacheKeyPayload
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("request")]
        public RequestEnvelope? Request { get; set; }
    }

    private sealed class CachePutPayload
    {
        [JsonPropertyName("key")]
        public CacheKeyPayload Key { get; set; } = new();

        [JsonPropertyName("response")]
        public ResponseEnvelope Response { get; set; } = null!;
    }

    private sealed class CacheQueryPayload
    {
        [JsonPropertyName("key")]
        public CacheKeyPayload Key { get; set; } = new();

        [JsonPropertyName("options")]
        public CacheQueryOptionsEnvelope? Options { get; set; }
    }

    private sealed class CacheDeletePayload
    {
        [JsonPropertyName("deleted")]
        public bool Deleted { get; set; }
    }

    [JsonSerializable(typeof(CacheKeyPayload))]
    [JsonSerializable(typeof(CachePutPayload))]
    [JsonSerializable(typeof(CacheQueryPayload))]
    [JsonSerializable(typeof(CacheDeletePayload))]
    [JsonSerializable(typeof(CacheQueryOptionsEnvelope))]
    [JsonSerializable(typeof(RequestEnvelope))]
    [JsonSerializable(typeof(ResponseEnvelope))]
    [JsonSerializable(typeof(Header))]
    private sealed partial class CacheBindingJsonContext : JsonSerializerContext
    {
    }
}
