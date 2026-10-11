using Workers;

namespace CorsHeaders;

public static class Worker
{
    private static readonly Router Routes = new Router()
        .Use(Cors.Allow(origins: ["*"], methods: ["GET", "POST"], headers: ["content-type"]))
        .Any("/{*path}", (request, route) => Response.Json(new { ok = true }, headers: new Headers { ["x-example"] = "cors" }));

    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env env,
        ExecutionContext ctx)
        => Routes.HandleAsync(request, env, ctx);
}
