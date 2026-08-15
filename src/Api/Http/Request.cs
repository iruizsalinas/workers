namespace Workers;

public sealed class Request
{
    public Uri Url => WorkerApi.NotExecutable<Uri>();
    public string Method => WorkerApi.NotExecutable<string>();
    public string Path => WorkerApi.NotExecutable<string>();
    public string PathAndQuery => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    public Body Body => WorkerApi.NotExecutable<Body>();

    public ReadableStream? BodyStream() => WorkerApi.NotExecutable<ReadableStream?>();
    public QueryParameters QueryParameters => WorkerApi.NotExecutable<QueryParameters>();

    public Task<string> TextAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>(CancellationToken cancellationToken = default) => WorkerApi.NotExecutable<Task<T?>>();
    public Request Clone() => WorkerApi.NotExecutable<Request>();
}
