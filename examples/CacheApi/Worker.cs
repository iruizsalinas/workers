using Workers;

namespace CacheApi;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var cache = CacheStorage.Default;
        var cached = await cache.MatchAsync(request);
        if (cached is not null)
            return new Response(cached) { Headers = { ["x-cache"] = "hit" } };

        var response = new Response(new Date().ToIsoString()) { Headers = { ["cache-control"] = "public, max-age=60", ["x-cache"] = "miss" } };

        context.WaitUntil(cache.PutAsync(request, response.Clone()));
        return response;
    }
}
