namespace Workers;

/// <summary>The global <c>fetch</c>.</summary>
public static class Http
{
    public static Task<Response> FetchAsync(string input, RequestInit? init = null) => WorkerApi.NotExecutable<Task<Response>>();
    public static Task<Response> FetchAsync(Url input, RequestInit? init = null) => WorkerApi.NotExecutable<Task<Response>>();
    public static Task<Response> FetchAsync(Request input, RequestInit? init = null) => WorkerApi.NotExecutable<Task<Response>>();
}
