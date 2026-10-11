using Workers;

namespace ProxyFetch;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var response = await Http.FetchAsync("https://example.com");
        return new Response(response) { Headers = { ["x-proxied-by"] = "Workers" } };
    }
}
