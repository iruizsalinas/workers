using Workers;

namespace CorsHeaders;

public static class Worker
{
    private static readonly Router Routes = new Router()
        .Use(Cors.Allow(origins: ["*"], methods: ["GET", "POST"], headers: ["content-type"]))
        .Any("/{*path}", (request, route) => Response.Json(new { ok = true }).WithHeader("x-example", "cors"));

    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
        => Routes.HandleAsync(request, environment, context);
}
