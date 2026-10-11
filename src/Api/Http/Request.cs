namespace Workers;

public sealed class Request
{
    public Request(string url, FetchOptions? options = null) => WorkerApi.NotExecutable();
    public Request(Request request, FetchOptions? options = null) => WorkerApi.NotExecutable();

    public Url Url => WorkerApi.NotExecutable<Url>();
    public string Method => WorkerApi.NotExecutable<string>();
    public string Path => WorkerApi.NotExecutable<string>();
    public string PathAndQuery => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    public ReadableStream? Body => WorkerApi.NotExecutable<ReadableStream?>();
    public RedirectMode Redirect => WorkerApi.NotExecutable<RedirectMode>();
    public AbortSignal Signal => WorkerApi.NotExecutable<AbortSignal>();

    public QueryParameters QueryParameters => WorkerApi.NotExecutable<QueryParameters>();

    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    public Task<FormData> FormDataAsync() => WorkerApi.NotExecutable<Task<FormData>>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();
    public Request Clone() => WorkerApi.NotExecutable<Request>();
    public Request WithUrl(string url) => WorkerApi.NotExecutable<Request>();
    public Request WithUrl(Url url) => WorkerApi.NotExecutable<Request>();
}
