using Workers;

namespace HelloWorld;

public static class Worker
{
    [FetchEvent]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        var router = new Router()
            .Get("/", static (_, _) =>
                Task.FromResult(Response.Text("Hello from C# on Cloudflare Workers.")))
            .Get("/hello/:name", static (_, route) =>
            {
                var name = route.Param("name") ?? "worker";
                return Task.FromResult(Response.Json(new { message = $"Hello, {name}." }));
            });

        return router.RunAsync(request, environment, context);
    }
}
