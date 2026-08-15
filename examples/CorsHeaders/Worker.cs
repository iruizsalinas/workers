using Workers;

namespace CorsHeaders;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Method == "OPTIONS")
            return Task.FromResult(Response.Empty(204)
                .WithHeader("access-control-allow-origin", "*")
                .WithHeader("access-control-allow-methods", "GET, POST, OPTIONS")
                .WithHeader("access-control-allow-headers", "content-type"));

        return Task.FromResult(
            Response.Json(new { ok = true })
                .WithHeader("x-example", "cors")
                .WithHeader("access-control-allow-origin", "*"));
    }
}
