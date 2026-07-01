using Workers;

namespace ProxyFetch;

public static class Worker
{
    [FetchEvent]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var response = await environment.FetchAsync("https://example.com");
        return response.WithHeader("x-proxied-by", "Workers");
    }
}
