using Workers;

namespace RouterSemantics;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
    {
        if (new Url(request.Url).Pathname == "/conflict")
        {
            try
            {
                new Router()
                    .Get("/users/{id}", (request, route) => new Response("first"))
                    .Get("/users/{name}", (request, route) => new Response("second"));
                return new Response("registered");
            }
            catch (Exception exception)
            {
                return new Response(exception.Message) { Status = 500 };
            }
        }

        var prefix = environment.Variable("PREFIX");
        return await new Router()
            .Get("/users/me", (request, route) => new Response($"{prefix}:me"))
            .Get("/users/{id:int}", (request, route) => new Response($"{prefix}:int:{int.Parse(route.Parameter("id")) + 1}"))
            .Get("/users/{id}", (request, route) => new Response($"{prefix}:name:{route.Parameter("id")}"))
            .Delete("/users/{id}", async (request, route) =>
            {
                await Scheduler.WaitAsync(1);
                return new Response($"deleted:{route.Parameter("id")}");
            })
            .Get("/items/{id:uuid}", (request, route) => new Response(route.Parameter("id").ToLowerInvariant()))
            .Any("/proxy/{*path}", (request, route) => new Response($"{request.Method}:{route.Parameter("path")}"))
            .Get("/proxy/status", (request, route) => new Response("status"))
            .Post("/echo", async (request, route) => new Response(await request.TextAsync()) { Status = 201 })
            .Get("/", (request, route) => new Response("root"))
            .Fallback((request, route) => new Response($"fallback:{new Url(request.Url).Pathname}") { Status = 404 })
            .HandleAsync(request, environment, context);
    }
}
