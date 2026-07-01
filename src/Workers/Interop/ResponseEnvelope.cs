using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers.Interop;

/// <summary>A JSON-friendly response shape for JavaScript/.NET interop.</summary>
internal sealed class ResponseEnvelope
{
    /// <summary>Creates a response envelope.</summary>
    [JsonConstructor]
    public ResponseEnvelope(
        int status,
        IReadOnlyList<Header> headers,
        string? bodyBase64,
        string? statusText = null,
        string? webSocketHandle = null,
        IReadOnlyList<string>? waitUntilHandles = null,
        bool passThroughOnException = false,
        JsonElement? cf = null,
        string? encodeBody = null,
        string? nativeResponseHandle = null,
        string? nativeBodyStreamSource = null,
        string? nativeBodyStreamHandle = null,
        string? managedBodyStreamHandle = null)
    {
        if (webSocketHandle is null && (status is < 200 or > 599))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Workers responses must use status codes from 200 through 599.");

        if (webSocketHandle is not null && status != 101)
            throw new ArgumentOutOfRangeException(nameof(status), status, "WebSocket responses must use status code 101.");

        ValidateStatusText(statusText);
        Status = status;
        StatusText = statusText;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        BodyBase64 = bodyBase64;
        WebSocketHandle = webSocketHandle;
        WaitUntilHandles = waitUntilHandles ?? [];
        PassThroughOnException = passThroughOnException;
        Cf = cf?.Clone();
        EncodeBody = NormalizeEncodeBody(encodeBody);
        var bodyHandleCount = CountNonNull(nativeResponseHandle, nativeBodyStreamHandle, managedBodyStreamHandle);
        if (bodyHandleCount > 1)
            throw new ArgumentException("Response envelopes cannot contain more than one native or managed body handle.", nameof(nativeBodyStreamHandle));

        NativeResponseHandle = nativeResponseHandle;
        NativeBodyStreamSource = NormalizeNativeBodyStreamSource(nativeBodyStreamSource, nativeBodyStreamHandle);
        NativeBodyStreamHandle = nativeBodyStreamHandle;
        ManagedBodyStreamHandle = managedBodyStreamHandle;
    }

    /// <summary>The HTTP status code.</summary>
    public int Status { get; }

    /// <summary>The HTTP status text, when explicitly configured.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StatusText { get; }

    /// <summary>The response headers.</summary>
    public IReadOnlyList<Header> Headers { get; }

    /// <summary>The base64-encoded body, or null when the response is empty.</summary>
    public string? BodyBase64 { get; }

    /// <summary>The WebSocket handle, when this is a protocol-switching response.</summary>
    public string? WebSocketHandle { get; }

    /// <summary>Managed background task handles that should be passed to <c>ctx.waitUntil</c>.</summary>
    public IReadOnlyList<string> WaitUntilHandles { get; }

    /// <summary>True when the handler requested fail-open pass-through behavior.</summary>
    public bool PassThroughOnException { get; }

    /// <summary>Cloudflare response metadata and options, when supplied by the runtime or configured by managed code.</summary>
    public JsonElement? Cf { get; }

    /// <summary>Response body encoding mode passed to the Workers runtime.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EncodeBody { get; }

    /// <summary>A runtime handle for a native Workers Response whose body should remain stream-backed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeResponseHandle { get; }

    /// <summary>The source type for a native body stream used as this response body.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeBodyStreamSource { get; }

    /// <summary>The handle for a native body stream used as this response body.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NativeBodyStreamHandle { get; }

    /// <summary>The handle for a C#-produced readable stream used as this response body.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManagedBodyStreamHandle { get; }

    /// <summary>Creates an envelope from a response.</summary>
    public static ResponseEnvelope FromResponse(
        Response response,
        IReadOnlyList<string>? waitUntilHandles = null,
        bool passThroughOnException = false)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ResponseEnvelope(
            response.Status,
            response.Headers.Select(static header => new Header(header.Key, header.Value)).ToArray(),
            response.Body.IsEmpty ? null : Convert.ToBase64String(response.Body.InternalBytes.Span),
            response.StatusText,
            response.WebSocket?.Handle,
            waitUntilHandles,
            passThroughOnException,
            response.Cf,
            EncodeBodyName(response.EncodeBody),
            response.NativeResponseHandle,
            response.NativeBodyStream?.Source is NativeStreamSource.Request or NativeStreamSource.Response ? NativeStreamSourceName(response.NativeBodyStream.Source) : null,
            response.NativeBodyStream?.Source is NativeStreamSource.Request or NativeStreamSource.Response ? response.NativeBodyStream.Handle : null,
            response.NativeBodyStream?.Source == NativeStreamSource.Managed ? response.NativeBodyStream.Handle : null);
    }

    /// <summary>Converts the envelope into a response.</summary>
    public Response ToResponse(string? invocationId = null, IBindingDispatcher? bindingDispatcher = null)
    {
        if (WebSocketHandle is not null)
            throw new WorkersException("WebSocket response envelopes cannot be converted to managed responses.");

        var bodyBytes = BodyBase64 is null ? [] : Convert.FromBase64String(BodyBase64);
        var contentType = Headers
            .FirstOrDefault(static header => string.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        var response = bodyBytes.Length == 0
            ? Response.Empty(Status, StatusText)
            : Response.Bytes(bodyBytes, Status, contentType ?? "application/octet-stream", StatusText);

        if (NativeResponseHandle is not null)
        {
            response = new Response(
                Status,
                new Headers(),
                Body.Empty,
                statusText: StatusText,
                nativeResponseHandle: NativeResponseHandle,
                nativeBodyStream: null,
                invocationId: invocationId,
                bindingDispatcher: bindingDispatcher);
        }

        if (NativeBodyStreamSource is not null && NativeBodyStreamHandle is not null)
        {
            response = new Response(
                Status,
                new Headers(),
                Body.Empty,
                statusText: StatusText,
                nativeBodyStream: new ReadableStream(
                    invocationId ?? throw new WorkersException("Native response body stream requires a live Worker invocation."),
                    NativeStreamSourceValue(NativeBodyStreamSource),
                    NativeBodyStreamHandle,
                    bindingDispatcher ?? throw new WorkersException("Native response body stream requires a binding dispatcher.")));
        }

        if (ManagedBodyStreamHandle is not null)
        {
            response = new Response(
                Status,
                new Headers(),
                Body.Empty,
                statusText: StatusText,
                nativeBodyStream: new ReadableStream(ManagedBodyStreamHandle));
        }

        foreach (var header in Headers)
        {
            if (bodyBytes.Length != 0 && string.Equals(header.Name, "content-type", StringComparison.OrdinalIgnoreCase))
                continue;

            response.Headers.Append(header.Name, header.Value);
        }

        if (Cf is not null)
            response = response.WithCf(Cf.Value);

        response = response.WithEncodeBody(EncodeBodyValue(EncodeBody));

        return response;
    }

    private static string? NormalizeEncodeBody(string? encodeBody)
    {
        if (encodeBody is null)
            return null;

        if (string.Equals(encodeBody, "manual", StringComparison.Ordinal))
            return "manual";

        throw new ArgumentException($"Unsupported response encodeBody value '{encodeBody}'.", nameof(encodeBody));
    }

    private static string? NormalizeNativeBodyStreamSource(string? source, string? handle)
    {
        if (source is null && handle is null)
            return null;

        if (source is null || handle is null)
            throw new ArgumentException("Native body stream source and handle must be provided together.", nameof(source));

        if (source is "request" or "response")
            return source;

        throw new ArgumentException($"Unsupported native body stream source '{source}'.", nameof(source));
    }

    private static int CountNonNull(params string?[] values) =>
        values.Count(static value => value is not null);

    private static void ValidateStatusText(string? statusText)
    {
        if (statusText is null)
            return;

        if (statusText.Any(static c => c is '\0' or '\r' or '\n'))
            throw new ArgumentException("Status text cannot contain null, CR, or LF characters.", nameof(statusText));
    }

    private static string? EncodeBodyName(ResponseEncodeBody encodeBody) =>
        encodeBody switch
        {
            ResponseEncodeBody.Automatic => null,
            ResponseEncodeBody.Manual => "manual",
            _ => throw new ArgumentOutOfRangeException(nameof(encodeBody), encodeBody, "Unsupported response encode body mode.")
        };

    private static ResponseEncodeBody EncodeBodyValue(string? encodeBody) =>
        encodeBody switch
        {
            null => ResponseEncodeBody.Automatic,
            "manual" => ResponseEncodeBody.Manual,
            _ => throw new ArgumentOutOfRangeException(nameof(encodeBody), encodeBody, "Unsupported response encodeBody value.")
        };

    private static string NativeStreamSourceName(NativeStreamSource source) =>
        source switch
        {
            NativeStreamSource.Request => "request",
            NativeStreamSource.Response => "response",
            NativeStreamSource.Managed => "managed",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported native stream source.")
        };

    private static NativeStreamSource NativeStreamSourceValue(string source) =>
        source switch
        {
            "request" => NativeStreamSource.Request,
            "response" => NativeStreamSource.Response,
            "managed" => NativeStreamSource.Managed,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported native stream source.")
        };

}
