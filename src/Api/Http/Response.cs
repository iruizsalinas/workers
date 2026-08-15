namespace Workers;

public sealed class Response
{
    public int Status => WorkerApi.NotExecutable<int>();
    public string StatusText => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    public Body Body => WorkerApi.NotExecutable<Body>();

    public ReadableStream BodyStream() => WorkerApi.NotExecutable<ReadableStream>();
    public static Response Empty(int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Text(string body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Html(string body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Json<T>(T body, int status = 200, object? options = null, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response Redirect(string location, int status = 302, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public static Response FromBody(Body body, int status = 200, string? statusText = null) => WorkerApi.NotExecutable<Response>();
    public Response WithHeader(string name, string value) => WorkerApi.NotExecutable<Response>();
    public Response AppendHeader(string name, string value) => WorkerApi.NotExecutable<Response>();
    public Response WithoutHeader(string name) => WorkerApi.NotExecutable<Response>();
    public Response Clone() => WorkerApi.NotExecutable<Response>();
    public Task<string> TextAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<string>>();
    public Task<ReadOnlyMemory<byte>> BytesAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<ReadOnlyMemory<byte>>>();
    public Task<T?> JsonAsync<T>(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
}

public enum ResponseEncodeBody
{
    Automatic,
    Manual
}
