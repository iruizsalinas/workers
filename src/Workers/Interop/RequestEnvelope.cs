using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers.Interop;

/// <summary>A JSON-friendly request shape for JavaScript/.NET interop.</summary>
internal sealed class RequestEnvelope
{
    /// <summary>Creates a request envelope.</summary>
    [JsonConstructor]
    public RequestEnvelope(
        string url,
        string method,
        IReadOnlyList<Header> headers,
        string? bodyBase64,
        JsonElement? cf = null,
        string? nativeRequestHandle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Url = url;
        Method = method;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        BodyBase64 = bodyBase64;
        Cf = cf;
        NativeRequestHandle = nativeRequestHandle;
    }

    /// <summary>The absolute request URL.</summary>
    public string Url { get; }

    /// <summary>The HTTP method.</summary>
    public string Method { get; }

    /// <summary>The request headers.</summary>
    public IReadOnlyList<Header> Headers { get; }

    /// <summary>The base64-encoded body, or null when no body was supplied.</summary>
    public string? BodyBase64 { get; }

    /// <summary>Cloudflare edge metadata, when supplied by the runtime.</summary>
    public JsonElement? Cf { get; }

    /// <summary>A runtime handle for a native Workers Request whose body can be forwarded as a stream.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeRequestHandle { get; }

    /// <summary>Creates an envelope from a request.</summary>
    public static RequestEnvelope FromRequest(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RequestEnvelope(
            request.Url.ToString(),
            request.Method,
            request.Headers.Select(static header => new Header(header.Key, header.Value)).ToArray(),
            request.Body.IsEmpty ? null : Convert.ToBase64String(request.Body.InternalBytes.Span),
            request.Cf,
            request.NativeRequestHandle);
    }

    /// <summary>Converts the envelope into a request.</summary>
    public Request ToRequest(string? invocationId = null, IBindingDispatcher? bindingDispatcher = null)
    {
        var bodyBytes = BodyBase64 is null ? [] : Convert.FromBase64String(BodyBase64);
        var body = bodyBytes.Length == 0 ? Body.Empty : Body.FromBytes(bodyBytes);
        return new Request(
            new Uri(Url, UriKind.Absolute),
            Method,
            global::Workers.Headers.From(ToPairs(Headers)),
            body,
            Cf,
            NativeRequestHandle,
            invocationId,
            bindingDispatcher);
    }

    private static IEnumerable<KeyValuePair<string, string>> ToPairs(IEnumerable<Header> headers)
    {
        foreach (var header in headers)
            yield return new KeyValuePair<string, string>(header.Name, header.Value);
    }
}
