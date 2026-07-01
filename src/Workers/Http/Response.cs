using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Workers;

/// <summary>A Worker-native response abstraction convertible to the platform Response object by generated glue.</summary>
public sealed class Response
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal Response(
        int status,
        Headers headers,
        Body body,
        WebSocket? webSocket = null,
        JsonElement? cf = null,
        ResponseEncodeBody encodeBody = ResponseEncodeBody.Automatic,
        string? statusText = null,
        string? nativeResponseHandle = null,
        ReadableStream? nativeBodyStream = null,
        string? invocationId = null,
        IBindingDispatcher? bindingDispatcher = null)
    {
        if (webSocket is null && (status is < 200 or > 599))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Workers responses must use status codes from 200 through 599.");

        if (webSocket is not null && status != 101)
            throw new ArgumentOutOfRangeException(nameof(status), status, "WebSocket responses must use status code 101.");

        if (IsNullBodyStatus(status) && (!body.IsEmpty || nativeBodyStream is not null))
            throw new ArgumentException($"Responses with status code {status} cannot have a body.", nameof(body));

        ValidateStatusText(statusText);
        Status = status;
        StatusText = statusText;
        Headers = headers;
        Body = body;
        WebSocket = webSocket;
        Cf = cf?.Clone();
        EncodeBody = encodeBody;
        NativeResponseHandle = nativeResponseHandle;
        NativeBodyStream = nativeBodyStream;
        InvocationId = invocationId;
        BindingDispatcher = bindingDispatcher;
    }

    /// <summary>The HTTP status code.</summary>
    public int Status { get; }

    /// <summary>The HTTP status text, when explicitly configured.</summary>
    public string? StatusText { get; }

    /// <summary>The response headers.</summary>
    public Headers Headers { get; }

    /// <summary>The response body.</summary>
    public Body Body { get; }

    /// <summary>The WebSocket returned with a protocol-switching response, if any.</summary>
    public WebSocket? WebSocket { get; }

    /// <summary>Cloudflare response metadata and options, when supplied by the runtime or configured for outbound responses.</summary>
    public JsonElement? Cf { get; }

    /// <summary>Controls how the Workers runtime encodes the response body before sending it.</summary>
    public ResponseEncodeBody EncodeBody { get; }

    internal string? NativeResponseHandle { get; }

    internal ReadableStream? NativeBodyStream { get; }

    internal string? InvocationId { get; }

    internal IBindingDispatcher? BindingDispatcher { get; }

    /// <summary>Gets the native response body stream when this response is backed by a Workers runtime response.</summary>
    public ReadableStream BodyStream()
    {
        if (NativeBodyStream is not null)
            return NativeBodyStream;

        if (NativeResponseHandle is null || InvocationId is null || BindingDispatcher is null)
            throw new WorkersException("Native response body stream is only available during a live Worker invocation.");

        return new ReadableStream(InvocationId, NativeStreamSource.Response, NativeResponseHandle, BindingDispatcher);
    }

    /// <summary>Creates a fluent response builder.</summary>
    public static ResponseBuilder Builder(int status = 200) => new(status);

    /// <summary>Sets a response header and returns this response.</summary>
    public Response WithHeader(string name, string value)
    {
        Headers.Set(name, value);
        return this;
    }

    /// <summary>Appends a response header value and returns this response.</summary>
    public Response AppendHeader(string name, string value)
    {
        Headers.Append(name, value);
        return this;
    }

    /// <summary>Returns a response without the specified header.</summary>
    public Response WithoutHeader(string name)
    {
        var headers = Headers.From(Headers);
        headers.Delete(name);
        return new Response(Status, headers, Body, WebSocket, Cf, EncodeBody, StatusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);
    }

    /// <summary>Returns a response with a different status code.</summary>
    public Response WithStatus(int status) =>
        new(status, Headers.From(Headers), Body, WebSocket, Cf, EncodeBody, StatusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);

    /// <summary>Returns a response with a different status code and status text.</summary>
    public Response WithStatus(int status, string? statusText) =>
        new(status, Headers.From(Headers), Body, WebSocket, Cf, EncodeBody, statusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);

    /// <summary>Returns a response with a different status text.</summary>
    public Response WithStatusText(string? statusText) =>
        new(Status, Headers.From(Headers), Body, WebSocket, Cf, EncodeBody, statusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);

    /// <summary>Returns a response with a replacement header collection.</summary>
    public Response WithHeaders(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return new Response(Status, Headers.From(headers), Body, WebSocket, Cf, EncodeBody, StatusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);
    }

    /// <summary>Returns a response with a different body.</summary>
    public Response WithBody(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var headers = Headers.From(Headers);
        ApplyBodyContentType(headers, body, Body);
        return new Response(Status, headers, body, WebSocket, Cf, EncodeBody, StatusText);
    }

    /// <summary>Returns a response with a UTF-8 text body.</summary>
    public Response WithText(string body, string contentType = "text/plain; charset=utf-8") =>
        WithBody(Body.Text(body, contentType));

    /// <summary>Returns a response with an HTML body.</summary>
    public Response WithHtml(string body) =>
        WithBody(Body.Text(body, "text/html; charset=utf-8"));

    /// <summary>Returns a response with a JSON body.</summary>
    public Response WithJson<T>(T body, JsonSerializerOptions? options = null) =>
        WithBody(Body.Json(body, options));

    /// <summary>Returns a response with a binary body.</summary>
    public Response WithBytes(ReadOnlySpan<byte> body, string contentType = "application/octet-stream") =>
        WithBody(Body.FromBytes(body, contentType));

    /// <summary>Returns a response with Cloudflare response metadata or options attached.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This convenience API intentionally uses System.Text.Json reflection serialization for arbitrary Cloudflare cf metadata/options.")]
    public Response WithCf<T>(T cf, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cf);
        var element = JsonSerializer.SerializeToElement(cf, options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new Response(Status, Headers.From(Headers), Body, WebSocket, element, EncodeBody, StatusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);
    }

    /// <summary>Returns a response with explicit body-encoding behavior.</summary>
    public Response WithEncodeBody(ResponseEncodeBody encodeBody) =>
        new(Status, Headers.From(Headers), Body, WebSocket, Cf, encodeBody, StatusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);

    /// <summary>Applies CORS headers to this response and returns it.</summary>
    public Response WithCors(Cors cors)
    {
        ArgumentNullException.ThrowIfNull(cors);
        return cors.ApplyTo(this);
    }

    /// <summary>Deserializes Cloudflare response metadata.</summary>
    public T CfAs<T>(JsonSerializerOptions? options = null)
    {
        if (Cf is null)
            throw new WorkersException("Response does not contain Cloudflare metadata.");

        return Cf.Value.Deserialize<T>(options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new WorkersException($"Response Cloudflare metadata could not be deserialized as '{typeof(T).FullName}'.");
    }

    /// <summary>Reads the in-memory body as UTF-8 text.</summary>
    public string Text()
    {
        ThrowIfNativeBodyNotMaterialized();
        return Body.AsText();
    }

    /// <summary>Reads the response body as UTF-8 text, using the native Workers response body when available.</summary>
    public async Task<string> TextAsync(CancellationToken cancellationToken = default)
    {
        if (NativeResponseHandle is null)
            return Text();

        var result = await DispatchNativeBodyAsync("native.response.text", cancellationToken);
        return JsonSerializer.Deserialize(result, NativeBodyJsonContext.Default.NativeTextResult)?.Value
            ?? throw new WorkersException("Native response text operation returned an empty result.");
    }

    /// <summary>Reads the in-memory body as bytes.</summary>
    public ReadOnlyMemory<byte> Bytes()
    {
        ThrowIfNativeBodyNotMaterialized();
        return Body.Bytes;
    }

    /// <summary>Reads the response body as bytes, using the native Workers response body when available.</summary>
    public async Task<ReadOnlyMemory<byte>> BytesAsync(CancellationToken cancellationToken = default)
    {
        if (NativeResponseHandle is null)
            return Bytes();

        var result = await DispatchNativeBodyAsync("native.response.bytes", cancellationToken);
        var bodyBase64 = JsonSerializer.Deserialize(result, NativeBodyJsonContext.Default.NativeBytesResult)?.BodyBase64;
        return bodyBase64 is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(bodyBase64);
    }

    /// <summary>Reads the in-memory body as JSON.</summary>
    public T? Json<T>(JsonSerializerOptions? options = null)
    {
        ThrowIfNativeBodyNotMaterialized();
        return Body.AsJson<T>(options);
    }

    /// <summary>Reads the response body as JSON, using the native Workers response body when available.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This convenience API intentionally uses System.Text.Json reflection deserialization; browser-wasm workers keep reflection JSON enabled by default.")]
    public async Task<T?> JsonAsync<T>(JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (NativeResponseHandle is null)
            return Json<T>(options);

        var bytes = await BytesAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(bytes.Span, options ?? JsonOptions);
    }

    /// <summary>Creates an independent response copy with cloned headers.</summary>
    public Response Clone() =>
        new(Status, Headers.Clone(), Body, WebSocket, Cf, EncodeBody, StatusText, NativeResponseHandle, NativeBodyStream, InvocationId, BindingDispatcher);

    /// <summary>Creates an empty response.</summary>
    public static Response Empty(int status = 200, string? statusText = null) =>
        new(status, new Headers(), Body.Empty, statusText: statusText);

    /// <summary>Creates a WebSocket protocol-switching response.</summary>
    public static Response FromWebSocket(WebSocket webSocket)
    {
        ArgumentNullException.ThrowIfNull(webSocket);
        return new Response(101, new Headers(), Body.Empty, webSocket);
    }

    /// <summary>Creates a response from a body.</summary>
    public static Response FromBody(Body body, int status = 200, string? statusText = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new Response(status, ContentHeaders(body), body, statusText: statusText);
    }

    /// <summary>Creates a response backed by a native readable stream.</summary>
    public static Response FromStream(ReadableStream stream, int status = 200, Headers? headers = null, string? statusText = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new Response(status, headers is null ? new Headers() : Headers.From(headers), Body.Empty, statusText: statusText, nativeBodyStream: stream);
    }

    /// <summary>Creates a UTF-8 text response.</summary>
    public static Response Text(string body, int status = 200, string? statusText = null)
    {
        var responseBody = Body.Text(body);
        return new Response(status, ContentHeaders(responseBody), responseBody, statusText: statusText);
    }

    /// <summary>Creates an HTML response.</summary>
    public static Response Html(string body, int status = 200, string? statusText = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        var responseBody = Body.Text(body, "text/html; charset=utf-8");
        return new Response(status, ContentHeaders(responseBody), responseBody, statusText: statusText);
    }

    /// <summary>Creates a JSON response.</summary>
    public static Response Json<T>(T body, int status = 200, JsonSerializerOptions? options = null, string? statusText = null)
    {
        var responseBody = Body.Json(body, options);
        return new Response(status, ContentHeaders(responseBody), responseBody, statusText: statusText);
    }

    /// <summary>Creates a binary response.</summary>
    public static Response Bytes(
        ReadOnlySpan<byte> body,
        int status = 200,
        string contentType = "application/octet-stream",
        string? statusText = null)
    {
        var responseBody = Body.FromBytes(body, contentType);
        return new Response(status, ContentHeaders(responseBody), responseBody, statusText: statusText);
    }

    /// <summary>Creates a 4xx or 5xx error response.</summary>
    public static Response Error(string message, int status, string? statusText = null)
    {
        if (status is < 400 or > 599)
            throw new ArgumentOutOfRangeException(nameof(status), status, "Error responses must use status codes from 400 through 599.");

        return Text(message, status, statusText);
    }

    /// <summary>Creates a redirect response.</summary>
    public static Response Redirect(string location, int status = 302, string? statusText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        if (status is < 300 or > 399)
            throw new ArgumentOutOfRangeException(nameof(status), status, "Redirect responses must use status codes from 300 through 399.");

        return Empty(status, statusText).WithHeader("location", location);
    }

    private static Headers ContentHeaders(Body body)
    {
        var headers = new Headers();
        if (body.ContentType is not null)
            headers.Set("content-type", body.ContentType);

        return headers;
    }

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

    private static void ValidateStatusText(string? statusText)
    {
        if (statusText is null)
            return;

        if (statusText.Any(static c => c is '\0' or '\r' or '\n'))
            throw new ArgumentException("Status text cannot contain null, CR, or LF characters.", nameof(statusText));
    }

    private static bool IsNullBodyStatus(int status) =>
        status is 204 or 205 or 304;

    private async Task<string> DispatchNativeBodyAsync(string operation, CancellationToken cancellationToken)
    {
        if (InvocationId is null || BindingDispatcher is null)
            throw new WorkersException("Native response body is only available during a live Worker invocation.");

        var invocation = new BindingInvocation(
            InvocationId,
            "$response",
            operation,
            JsonSerializer.Serialize(
                new NativeBodyRequest(NativeResponseHandle),
                NativeBodyJsonContext.Default.NativeBodyRequest));

        return await BindingDispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private void ThrowIfNativeBodyNotMaterialized()
    {
        if (NativeResponseHandle is not null && Body.IsEmpty)
            throw new WorkersException("This response body is a native Workers stream. Use a materialized response body before reading it from managed code.");

        if (NativeBodyStream is not null && Body.IsEmpty)
            throw new WorkersException("This response body is a native Workers stream. Use BodyStream() to read it or return it directly.");
    }

}

/// <summary>Controls Workers response body encoding.</summary>
public enum ResponseEncodeBody
{
    /// <summary>Let the Workers runtime encode the response body automatically.</summary>
    Automatic,

    /// <summary>Return the response body as-is, for example when sending pre-compressed bytes.</summary>
    Manual
}
