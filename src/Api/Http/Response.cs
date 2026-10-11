namespace Workers;

/// <summary>
/// A response. Set <c>Status</c>, <c>StatusText</c>, <c>Headers</c>, <c>WebSocket</c>, <c>Cf</c> and <c>EncodeBody</c>
/// in an object initializer, as the <c>ResponseInit</c> of <c>new Response(body, init)</c>.
/// </summary>
public sealed class Response
{
    public Response(BodyInit? body = null) => WorkerApi.NotExecutable();
    /// <summary>Copies another response's status, headers and settings, as <c>new Response(body, response)</c>.
    /// Pass <c>response.Body</c> to forward its body without buffering it.</summary>
    public Response(BodyInit? body, Response init) => WorkerApi.NotExecutable();

    public static Response Json<T>(T data, int status = 200, Headers? headers = null, string? statusText = null) =>
        WorkerApi.NotExecutable<Response>();
    /// <summary>Redirects to an absolute or relative URL. Status must be 301, 302, 303, 307 or 308.</summary>
    public static Response Redirect(string url, int status = 302) => WorkerApi.NotExecutable<Response>();
    /// <summary>A network error response, which makes <c>fetch</c> reject when returned to a caller.</summary>
    public static Response Error() => WorkerApi.NotExecutable<Response>();

    public int Status { get => WorkerApi.NotExecutable<int>(); init => WorkerApi.NotExecutable(); }
    public string StatusText { get => WorkerApi.NotExecutable<string>(); init => WorkerApi.NotExecutable(); }
    public Headers Headers { get => WorkerApi.NotExecutable<Headers>(); init => WorkerApi.NotExecutable(); }
    /// <summary>The client end of a WebSocket pair, for a 101 response that accepts an upgrade.</summary>
    public WebSocket? WebSocket { get => WorkerApi.NotExecutable<WebSocket?>(); init => WorkerApi.NotExecutable(); }
    public object? Cf { get => WorkerApi.NotExecutable<object?>(); init => WorkerApi.NotExecutable(); }
    /// <summary>
    /// Whether the runtime compresses the body per <c>Content-Encoding</c>; use Manual for an already-compressed body.
    /// It is a construction setting only: the runtime does not expose it on the response.
    /// </summary>
    public ResponseBodyEncoding EncodeBody { init => WorkerApi.NotExecutable(); }

    public bool Ok => WorkerApi.NotExecutable<bool>();
    public bool Redirected => WorkerApi.NotExecutable<bool>();
    /// <summary>The final URL of a fetched response, or empty.</summary>
    public string Url => WorkerApi.NotExecutable<string>();
    public ResponseType Type => WorkerApi.NotExecutable<ResponseType>();
    public ReadableStream? Body => WorkerApi.NotExecutable<ReadableStream?>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();
    /// <summary>Appends <c>Set-Cookie</c> headers. Writable only when the headers are, as for responses the Worker constructs.</summary>
    public ResponseCookies Cookies => WorkerApi.NotExecutable<ResponseCookies>();

    /// <summary>Tees the body so the copy and the original can each be read.</summary>
    public Response Clone() => WorkerApi.NotExecutable<Response>();
    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    public Task<FormData> FormDataAsync() => WorkerApi.NotExecutable<Task<FormData>>();
    public Task<Blob> BlobAsync() => WorkerApi.NotExecutable<Task<Blob>>();
}

public enum ResponseBodyEncoding
{
    Automatic,
    Manual
}

public enum ResponseType
{
    Default,
    Error
}
