namespace Workers;

public static class Http
{
    public static Task<Response> FetchAsync(string url) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(Request request) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(string url, FetchOptions options) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(Request request, FetchOptions options) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(string url, CancellationToken cancellationToken) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(Request request, CancellationToken cancellationToken) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(
        string url,
        FetchOptions options,
        CancellationToken cancellationToken) => WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(
        Request request,
        FetchOptions options,
        CancellationToken cancellationToken) => WorkerApi.NotExecutable<Task<Response>>();
}
