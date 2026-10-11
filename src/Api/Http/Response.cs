namespace Workers;

public sealed class Response
{
    public int Status => WorkerApi.NotExecutable<int>();
    public bool Ok => WorkerApi.NotExecutable<bool>();
    public string StatusText => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    public ReadableStream? Body => WorkerApi.NotExecutable<ReadableStream?>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();

    public static Response Empty(int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Text(string body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Html(string body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Json<T>(T body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Redirect(string location, int status = 302, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response FromBody(Body body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response FromStream(ReadableStream stream, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response FromStream(ReadableStream stream, Headers headers, int status = 200) => WorkerApi.NotExecutable<Response>();
    public static Response WebSocket(WebSocket socket) => WorkerApi.NotExecutable<Response>();
    public Response WithHeader(string name, string value) => WorkerApi.NotExecutable<Response>();
    public Response AppendHeader(string name, string value) => WorkerApi.NotExecutable<Response>();
    public Response WithoutHeader(string name) => WorkerApi.NotExecutable<Response>();
    public Response WithCookie(string name, string value, CookieOptions? options = null) => WorkerApi.NotExecutable<Response>();
    /// <summary>Expires the cookie. Path, domain and prefix attributes must match the ones it was set with.</summary>
    public Response WithoutCookie(string name, CookieOptions? options = null) => WorkerApi.NotExecutable<Response>();
    public Response Clone() => WorkerApi.NotExecutable<Response>();
    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    public Task<FormData> FormDataAsync() => WorkerApi.NotExecutable<Task<FormData>>();
}
