using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Workers;

/// <summary>A Worker-native request abstraction for incoming and outbound HTTP requests.</summary>
public sealed class Request
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Creates a request.</summary>
    public Request(Uri url, string method, Headers? headers = null, Body? body = null)
        : this(url, method, headers, body, cf: null)
    {
    }

    internal Request(Uri url, string method, Headers? headers, Body? body, JsonElement? cf)
        : this(url, method, headers, body, cf, nativeRequestHandle: null)
    {
    }

    internal Request(
        Uri url,
        string method,
        Headers? headers,
        Body? body,
        JsonElement? cf,
        string? nativeRequestHandle,
        string? invocationId = null,
        IBindingDispatcher? bindingDispatcher = null)
    {
        Url = url ?? throw new ArgumentNullException(nameof(url));
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        Method = method.ToUpperInvariant();
        Headers = headers is null ? new Headers() : Headers.From(headers);
        Body = body ?? Body.Empty;
        Cf = cf?.Clone();
        NativeRequestHandle = nativeRequestHandle;
        InvocationId = invocationId;
        BindingDispatcher = bindingDispatcher;
    }

    /// <summary>The absolute request URL.</summary>
    public Uri Url { get; }

    /// <summary>The URL scheme, such as <c>https</c>.</summary>
    public string Scheme => Url.Scheme;

    /// <summary>The URL host name without a port.</summary>
    public string Host => Url.Host;

    /// <summary>The URL authority, including the port when present.</summary>
    public string Authority => Url.Authority;

    /// <summary>The URL origin, including scheme and authority.</summary>
    public string Origin => Url.GetLeftPart(UriPartial.Authority);

    /// <summary>The uppercase HTTP method.</summary>
    public string Method { get; }

    /// <summary>The absolute URL path.</summary>
    public string Path => Url.AbsolutePath;

    /// <summary>The absolute URL path and query string.</summary>
    public string PathAndQuery => Url.PathAndQuery;

    /// <summary>The decoded URL query parameters.</summary>
    public QueryParameters QueryParameters => QueryParameters.Parse(Url);

    /// <summary>The request headers.</summary>
    public Headers Headers { get; }

    /// <summary>The request body.</summary>
    public Body Body { get; }

    /// <summary>Cloudflare edge metadata for an inbound request, when supplied by the runtime.</summary>
    public JsonElement? Cf { get; }

    internal string? NativeRequestHandle { get; }

    internal string? InvocationId { get; }

    internal IBindingDispatcher? BindingDispatcher { get; }

    /// <summary>Gets the native request body stream when this request came from the Workers runtime.</summary>
    public ReadableStream BodyStream()
    {
        if (NativeRequestHandle is null || InvocationId is null || BindingDispatcher is null)
            throw new WorkersException("Native request body stream is only available during a live Worker invocation.");

        return new ReadableStream(InvocationId, NativeStreamSource.Request, NativeRequestHandle, BindingDispatcher);
    }

    /// <summary>Typed Cloudflare edge metadata for an inbound request, when supplied by the runtime.</summary>
    public Cf? CfMetadata => Cf is null ? null : new Cf(Cf.Value);

    /// <summary>Reads the in-memory body as UTF-8 text.</summary>
    public string Text()
    {
        ThrowIfNativeBodyNotMaterialized();
        return Body.AsText();
    }

    /// <summary>Reads the request body as UTF-8 text, using the native Workers request body when available.</summary>
    public async Task<string> TextAsync(CancellationToken cancellationToken = default)
    {
        if (NativeRequestHandle is null)
            return Text();

        var result = await DispatchNativeBodyAsync("native.request.text", cancellationToken);
        return JsonSerializer.Deserialize(result, NativeBodyJsonContext.Default.NativeTextResult)?.Value
            ?? throw new WorkersException("Native request text operation returned an empty result.");
    }

    /// <summary>Reads the in-memory body as bytes.</summary>
    public ReadOnlyMemory<byte> Bytes()
    {
        ThrowIfNativeBodyNotMaterialized();
        return Body.Bytes;
    }

    /// <summary>Reads the request body as bytes, using the native Workers request body when available.</summary>
    public async Task<ReadOnlyMemory<byte>> BytesAsync(CancellationToken cancellationToken = default)
    {
        if (NativeRequestHandle is null)
            return Bytes();

        var result = await DispatchNativeBodyAsync("native.request.bytes", cancellationToken);
        var bodyBase64 = JsonSerializer.Deserialize(result, NativeBodyJsonContext.Default.NativeBytesResult)?.BodyBase64;
        return bodyBase64 is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(bodyBase64);
    }

    /// <summary>Reads the in-memory body as JSON.</summary>
    public T? Json<T>(JsonSerializerOptions? options = null)
    {
        ThrowIfNativeBodyNotMaterialized();
        return Body.AsJson<T>(options);
    }

    /// <summary>Reads the request body as JSON, using the native Workers request body when available.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This convenience API intentionally uses System.Text.Json reflection deserialization; browser-wasm workers keep reflection JSON enabled by default.")]
    public async Task<T?> JsonAsync<T>(JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (NativeRequestHandle is null)
            return Json<T>(options);

        var bytes = await BytesAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(bytes.Span, options ?? JsonOptions);
    }

    /// <summary>Deserializes decoded URL query parameters into a typed object.</summary>
    public T Query<T>(JsonSerializerOptions? options = null) => QueryParameters.As<T>(options);

    /// <summary>Parses the in-memory body as form data.</summary>
    public FormData FormData()
    {
        ThrowIfNativeBodyNotMaterialized();

        var contentType = Headers.Get("content-type") ?? Body.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
            throw new WorkersException("Cannot parse form data without a Content-Type header.");

        return global::Workers.FormData.Parse(Body, contentType);
    }

    /// <summary>Parses the in-memory body as form data and deserializes text fields into a typed object.</summary>
    public T Form<T>(JsonSerializerOptions? options = null) => FormData().As<T>(options);

    /// <summary>Creates an independent request copy with cloned headers.</summary>
    public Request Clone() => new(Url, Method, Headers.Clone(), Body, Cf, NativeRequestHandle, InvocationId, BindingDispatcher);

    /// <summary>Sets a request header and returns this request.</summary>
    public Request WithHeader(string name, string value)
    {
        Headers.Set(name, value);
        return this;
    }

    /// <summary>Appends a request header value and returns this request.</summary>
    public Request AppendHeader(string name, string value)
    {
        Headers.Append(name, value);
        return this;
    }

    /// <summary>Returns a request without the specified header.</summary>
    public Request WithoutHeader(string name)
    {
        var headers = Headers.From(Headers);
        headers.Delete(name);
        return new Request(Url, Method, headers, Body, Cf, NativeRequestHandle, InvocationId, BindingDispatcher);
    }

    /// <summary>Returns a request with a different absolute URL.</summary>
    public Request WithUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return WithUri(new Uri(url, UriKind.Absolute));
    }

    /// <summary>Returns a request with a different absolute URL path while preserving the query string.</summary>
    public Request WithPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!path.StartsWith('/'))
            throw new ArgumentException("URL paths must start with '/'.", nameof(path));

        var builder = new UriBuilder(Url) { Path = path };
        return WithUri(builder.Uri);
    }

    /// <summary>Returns a request with a different query string while preserving the path.</summary>
    public Request WithQuery(string? query)
    {
        var builder = new UriBuilder(Url) { Query = NormalizeQuery(query) };
        return WithUri(builder.Uri);
    }

    /// <summary>Returns a request with a query parameter set, replacing existing values for the same name.</summary>
    public Request WithQueryParameter(string name, string value)
    {
        ValidateQueryPart(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);

        return WithUri(SetQueryParameter(Url, name, value));
    }

    /// <summary>Returns a request with an appended query parameter value.</summary>
    public Request AppendQueryParameter(string name, string value)
    {
        ValidateQueryPart(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);

        return WithUri(AppendQueryParameter(Url, name, value));
    }

    /// <summary>Returns a request with all query parameter values for a name removed.</summary>
    public Request RemoveQueryParameter(string name)
    {
        ValidateQueryPart(name, nameof(name));
        return WithUri(RemoveQueryParameter(Url, name));
    }

    /// <summary>Returns a request with a different path and query string while preserving the origin.</summary>
    public Request WithPathAndQuery(string pathAndQuery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathAndQuery);
        if (!pathAndQuery.StartsWith('/'))
            throw new ArgumentException("URL paths must start with '/'.", nameof(pathAndQuery));

        return WithUri(new Uri(Url, pathAndQuery));
    }

    /// <summary>Returns a request with a different HTTP method.</summary>
    public Request WithMethod(string method) =>
        new(Url, method, Headers.From(Headers), Body, Cf, NativeRequestHandle, InvocationId, BindingDispatcher);

    /// <summary>Returns a request with a replacement header collection.</summary>
    public Request WithHeaders(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var requestHeaders = Headers.From(headers);
        ApplyBodyContentType(requestHeaders, Body);
        return new Request(Url, Method, requestHeaders, Body, Cf, NativeRequestHandle, InvocationId, BindingDispatcher);
    }

    /// <summary>Returns a request with a different body.</summary>
    public Request WithBody(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var headers = Headers.From(Headers);
        ApplyBodyContentType(headers, body, Body);
        return new Request(Url, Method, headers, body, Cf);
    }

    /// <summary>Returns a request with a UTF-8 text body.</summary>
    public Request WithText(string body, string contentType = "text/plain; charset=utf-8") =>
        WithBody(Body.Text(body, contentType));

    /// <summary>Returns a request with a JSON body.</summary>
    public Request WithJson<T>(T body, JsonSerializerOptions? options = null) =>
        WithBody(Body.Json(body, options));

    /// <summary>Returns a request with a binary body.</summary>
    public Request WithBytes(ReadOnlySpan<byte> body, string contentType = "application/octet-stream") =>
        WithBody(Body.FromBytes(body, contentType));

    /// <summary>Deserializes Cloudflare edge metadata for an inbound request.</summary>
    public T CfAs<T>(JsonSerializerOptions? options = null)
    {
        if (Cf is null)
            throw new WorkersException("Request does not contain Cloudflare edge metadata.");

        return Cf.Value.Deserialize<T>(options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new WorkersException($"Request Cloudflare metadata could not be deserialized as '{typeof(T).FullName}'.");
    }

    private async Task<string> DispatchNativeBodyAsync(string operation, CancellationToken cancellationToken)
    {
        if (InvocationId is null || BindingDispatcher is null)
            throw new WorkersException("Native request body is only available during a live Worker invocation.");

        var invocation = new BindingInvocation(
            InvocationId,
            "$request",
            operation,
            JsonSerializer.Serialize(
                new NativeBodyRequest(NativeRequestHandle),
                NativeBodyJsonContext.Default.NativeBodyRequest));

        return await BindingDispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private void ThrowIfNativeBodyNotMaterialized()
    {
        if (NativeRequestHandle is not null && Body.IsEmpty)
            throw new WorkersException("This request body is a native Workers stream. Use TextAsync(), BytesAsync(), JsonAsync(), or BodyStream() to read it.");
    }

    /// <summary>Creates a request.</summary>
    public static Request Create(string url, string method, Body? body = null, Headers? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var requestBody = body ?? Body.Empty;
        var requestHeaders = headers is null ? new Headers() : Headers.From(headers);
        ApplyBodyContentType(requestHeaders, requestBody);

        return new Request(new Uri(url, UriKind.Absolute), method, requestHeaders, requestBody);
    }

    /// <summary>Creates a fluent request builder.</summary>
    public static RequestBuilder Builder(string url) => new(url);

    /// <summary>Creates a GET request.</summary>
    public static Request Get(string url) => Create(url, "GET");

    /// <summary>Creates a HEAD request.</summary>
    public static Request Head(string url) => Create(url, "HEAD");

    /// <summary>Creates a POST request.</summary>
    public static Request Post(string url, Body body, Headers? headers = null) => Create(url, "POST", body, headers);

    /// <summary>Creates a PUT request.</summary>
    public static Request Put(string url, Body body, Headers? headers = null) => Create(url, "PUT", body, headers);

    /// <summary>Creates a PATCH request.</summary>
    public static Request Patch(string url, Body body, Headers? headers = null) => Create(url, "PATCH", body, headers);

    /// <summary>Creates a DELETE request.</summary>
    public static Request Delete(string url, Body? body = null, Headers? headers = null) => Create(url, "DELETE", body, headers);

    private static void ApplyBodyContentType(Headers headers, Body body, Body? previousBody = null)
    {
        var contentType = headers.Get("content-type");
        var contentTypeCameFromPreviousBody = contentType is not null
            && string.Equals(contentType, previousBody?.ContentType, StringComparison.OrdinalIgnoreCase);

        if (body.ContentType is null)
        {
            if (contentTypeCameFromPreviousBody)
                headers.Delete("content-type");

            return;
        }

        if (contentType is null || contentTypeCameFromPreviousBody)
            headers.Set("content-type", body.ContentType);
    }

    private Request WithUri(Uri url) =>
        new(url, Method, Headers.From(Headers), Body, Cf, NativeRequestHandle, InvocationId, BindingDispatcher);

    private static string NormalizeQuery(string? query) =>
        string.IsNullOrEmpty(query) ? "" : query.TrimStart('?');

    internal static Uri SetQueryParameter(Uri url, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(url);
        ValidateQueryPart(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);

        var entries = QueryParameters.Parse(url).Entries
            .Where(entry => !string.Equals(entry.Name, name, StringComparison.Ordinal))
            .Append(new QueryParameter(name, value));
        return WithEncodedQuery(url, entries);
    }

    internal static Uri AppendQueryParameter(Uri url, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(url);
        ValidateQueryPart(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);

        return WithEncodedQuery(
            url,
            QueryParameters.Parse(url).Entries.Append(new QueryParameter(name, value)));
    }

    internal static Uri RemoveQueryParameter(Uri url, string name)
    {
        ArgumentNullException.ThrowIfNull(url);
        ValidateQueryPart(name, nameof(name));

        return WithEncodedQuery(
            url,
            QueryParameters.Parse(url).Entries.Where(entry => !string.Equals(entry.Name, name, StringComparison.Ordinal)));
    }

    private static Uri WithEncodedQuery(Uri url, IEnumerable<QueryParameter> entries)
    {
        var builder = new UriBuilder(url) { Query = EncodeQuery(entries) };
        return builder.Uri;
    }

    private static string EncodeQuery(IEnumerable<QueryParameter> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return string.Join("&", entries.Select(static entry =>
            $"{EncodeQueryPart(entry.Name)}={EncodeQueryPart(entry.Value)}"));
    }

    private static string EncodeQueryPart(string value) =>
        Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);

    private static void ValidateQueryPart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }

}
