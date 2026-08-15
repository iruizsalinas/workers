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
}
