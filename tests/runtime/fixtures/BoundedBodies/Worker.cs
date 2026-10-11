using Workers;

namespace BoundedBodies;

public static class Worker
{
    private static readonly Router Routes = new Router()
        .Use(Cors.Allow(["https://app.example"], methods: ["POST"], headers: ["content-type"]))
        .Use(async (request, route, next) =>
        {
            var response = await next();
            return new Response(response) { Headers = { ["x-body-policy"] = "bounded" } };
        })
        .Post("/json/{limit:int}", async (request, route) =>
            Response.Json(await request.JsonAsync<Input>(maxBytes: int.Parse(route.Parameter("limit")))))
        .Post("/text/{limit:int}", async (request, route) =>
            new Response(await request.TextAsync(maxBytes: int.Parse(route.Parameter("limit")))))
        .Post("/bytes/{limit:int}", async (request, route) =>
            new Response(await request.BytesAsync(int.Parse(route.Parameter("limit")))))
        .Post("/invalid/{kind}/{limit:int}", InvalidLimitAsync)
        .Post("/twice/{kind}/{limit:int}", TwiceAsync);

    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env env, Context context) =>
        Routes.HandleAsync(request, env, context);

    private static async Task<Response> ReadAsync(Request request, string kind, int limit) => kind switch
    {
        "json" => Response.Json(await request.JsonAsync<Input>(limit)),
        "text" => new Response(await request.TextAsync(limit)),
        _ => new Response(await request.BytesAsync(maxBytes: limit))
    };

    private static async Task<Response> InvalidLimitAsync(Request request, RouteContext route)
    {
        try { return await ReadAsync(request, route.Parameter("kind"), int.Parse(route.Parameter("limit"))); }
        catch (Exception error)
        {
            var used = request.BodyUsed;
            return Response.Json(new { error = error.Message, used, text = await request.TextAsync() });
        }
    }

    private static async Task<Response> TwiceAsync(Request request, RouteContext route)
    {
        var kind = route.Parameter("kind");
        var limit = int.Parse(route.Parameter("limit"));
        await ReadAsync(request, kind, limit);
        return await ReadAsync(request, kind, limit);
    }
}

public sealed record Input(string Text);
