using Workers;

namespace CacheApi;

public static class Worker
{
    [FetchEvent]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var cache = environment.Cache();
        var cached = await cache.MatchAsync(request);
        if (cached is not null)
            return cached.WithHeader("x-cache", "hit");

        var response = Response.Text(DateTimeOffset.UtcNow.ToString("O"))
            .WithHeader("cache-control", "public, max-age=60")
            .WithHeader("x-cache", "miss");

        context.WaitUntil(cache.PutAsync(request, response.Clone()));
        return response;
    }
}
