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
    public Cookies Cookies => WorkerApi.NotExecutable<Cookies>();

    public Task<string> TextAsync() => WorkerApi.NotExecutable<Task<string>>();
    /// <summary>Reads UTF-8 text, limiting actual body bytes. A positive limit is required; overflow throws HttpError(413).</summary>
    public Task<string> TextAsync(int maxBytes) => WorkerApi.NotExecutable<Task<string>>();
    public Task<byte[]> BytesAsync() => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Reads bytes up to a positive limit; overflow cancels the body and throws HttpError(413).</summary>
    public Task<byte[]> BytesAsync(int maxBytes) => WorkerApi.NotExecutable<Task<byte[]>>();
    /// <summary>Reads JSON. Malformed JSON throws HttpError(400, "Malformed JSON"); application code validates the resulting value.</summary>
    public Task<T?> JsonAsync<T>() => WorkerApi.NotExecutable<Task<T?>>();
    /// <summary>Limits actual body bytes before parsing JSON. Overflow throws HttpError(413), malformed JSON throws HttpError(400).
    /// The limit must be positive. Content type and the parsed value remain application validation.</summary>
    public Task<T?> JsonAsync<T>(int maxBytes) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<FormData> FormDataAsync() => WorkerApi.NotExecutable<Task<FormData>>();
    public bool BodyUsed => WorkerApi.NotExecutable<bool>();
    public Request Clone() => WorkerApi.NotExecutable<Request>();
    public Request WithUrl(string url) => WorkerApi.NotExecutable<Request>();
    public Request WithUrl(Url url) => WorkerApi.NotExecutable<Request>();
}
