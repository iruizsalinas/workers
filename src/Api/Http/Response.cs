namespace Workers;

public sealed class Response
{
    /// <summary>Constructs a native response with copied, writable headers and the supplied body.</summary>
    public Response(BodyInit? body = null) => WorkerApi.NotExecutable();
    /// <summary>Copies initialization settings and headers. The supplied body stream is shared, not cloned.</summary>
    public Response(BodyInit? body, Response source) => WorkerApi.NotExecutable();
    /// <summary>Copies a response with writable headers, taking over its body stream. Use Clone() to keep the source readable.</summary>
    public Response(Response source) => WorkerApi.NotExecutable();

    public int Status
    {
        get => WorkerApi.NotExecutable<int>();
        init => WorkerApi.NotExecutable();
    }
    public bool Ok => WorkerApi.NotExecutable<bool>();
    public string StatusText
    {
        get => WorkerApi.NotExecutable<string>();
        init => WorkerApi.NotExecutable();
    }
    public Headers Headers
    {
        get => WorkerApi.NotExecutable<Headers>();
        init => WorkerApi.NotExecutable();
    }
    public ResponseCookies Cookies => WorkerApi.NotExecutable<ResponseCookies>();
    public WebSocket? WebSocket
    {
        get => WorkerApi.NotExecutable<WebSocket?>();
        init => WorkerApi.NotExecutable();
    }
    public object? Cf
    {
        get => WorkerApi.NotExecutable<object?>();
        init => WorkerApi.NotExecutable();
    }
    /// <summary>Controls transmission encoding. Use Manual for an already-compressed body.</summary>
    public ResponseBodyEncoding EncodeBody { init => WorkerApi.NotExecutable(); }
    public ReadableStream? Body => WorkerApi.NotExecutable<ReadableStream?>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();

    public static Response Json<T>(T body, int status = 200, Headers? headers = null, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    /// <summary>Redirects to an absolute or relative location. Status must be 301, 302, 303, 307 or 308; headers are writable.</summary>
    public static Response Redirect(string location, int status = 302) => WorkerApi.NotExecutable<Response>();

    /// <summary>Tees the body for separate consumption, preserving the headers' runtime guard.</summary>
    public Response Clone() => WorkerApi.NotExecutable<Response>();
    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    public Task<FormData> FormDataAsync() => WorkerApi.NotExecutable<Task<FormData>>();
}

public enum ResponseBodyEncoding
{
    Automatic,
    Manual
}
