using Workers;

namespace ProxyFetch;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env env,
        ExecutionContext ctx)
    {
        var response = await Http.FetchAsync("https://example.com");
        return new Response(response.Body, response) { Headers = { ["x-proxied-by"] = "Workers" } };
    }
}
