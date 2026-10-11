namespace Workers;

public static class Http
{
    public static Task<Response> FetchAsync(string url, FetchOptions? options = null) =>
        WorkerApi.NotExecutable<Task<Response>>();

    public static Task<Response> FetchAsync(Request request, FetchOptions? options = null) =>
        WorkerApi.NotExecutable<Task<Response>>();
}
