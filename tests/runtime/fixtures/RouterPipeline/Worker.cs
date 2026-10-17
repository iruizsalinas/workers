using Workers;

namespace RouterPipeline;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env env, ExecutionContext context)
    {
        var path = new Url(request.Url).Pathname;
        if (path.StartsWith("/config/"))
        {
            try
            {
                switch (path)
                {
                    case "/config/credentials": Cors.Allow(["*"], credentials: true); break;
                    case "/config/origin": Cors.Allow(["https://app.example/path"]); break;
                    case "/config/null": Cors.Allow(["null"]); break;
                    case "/config/headers": Cors.Allow(["https://app.example"], headers: ["x-good\r\nx-bad"]); break;
                    case "/config/trailing-newline": Cors.Allow(["https://app.example"], exposedHeaders: ["*\n"]); break;
                    case "/config/header-wildcard": Cors.Allow(["https://app.example"], headers: ["*"]); break;
                    case "/config/methods": Cors.Allow(["https://app.example"], methods: ["GET\r\nPOST"]); break;
                    case "/config/exposed": Cors.Allow(["https://app.example"], exposedHeaders: ["*"]); break;
                    case "/config/age": Cors.Allow(["https://app.example"], maxAge: -1); break;
                    case "/config/status": throw new HttpError(200, "invalid");
                    case "/config/status-high": throw new HttpError(600, "invalid");
                    case "/config/duplicate": new Router().OnError(FormatError).OnError(FormatError); break;
                }
                return new Response("accepted");
            }
            catch (Exception exception) { return new Response(exception.Message) { Status = 400 }; }
        }

        var trace = "";
        var hits = 0;
        var router = new Router()
            .Use(Cors.Allow(origins: path == "/wildcard" ? ["*"] : ["https://app.example", "https://second.example"],
                methods: ["GET", "HEAD", "POST"], credentials: path != "/wildcard",
                headers: ["Content-Type", "Authorization"], exposedHeaders: ["x-trace"], maxAge: 600))
            .Use(async (request, route, next) =>
            {
                trace += "outer>";
                var response = await next();
                trace += "<outer";
                return new Response(response.Body, response) { Headers = { ["x-trace"] = trace, ["x-hits"] = hits.ToString() }};
            })
            .Use(async (request, route, next) =>
            {
                trace += "inner>";
                if (path == "/private" && request.Headers.Get("authorization") != "Bearer good")
                    throw new HttpError(401, "Unauthorized");
                if (path == "/short") return new Response("short") { Status = 202 };
                var response = await next();
                if (path == "/double-next") await next();
                trace += "<inner";
                return response;
            })
            .Get("/ok/{id}", (request, route) =>
            {
                trace += route.Parameter("id");
                return new Response("ok") { Headers = { ["vary"] = "Accept-Encoding, origin" } };
            })
            .Get("/wildcard", (request, route) => new Response("wildcard"))
            .Get("/private", (request, route) => new Response("private"))
            .Get("/double-next", (request, route) => { hits++; return new Response("once"); })
            .Get("/http-error", Fail)
            .Get("/unexpected", Fail)
            .Get("/custom", Fail)
            .Get("/custom-unexpected", Fail)
            .Get("/broken-error-handler", Fail)
            .Get("/async-error-handler", Fail)
            .Post("/json", async (request, route) => Response.Json(await request.JsonAsync<Input>()))
            .Post("/consumed", async (request, route) =>
            {
                await request.TextAsync();
                return Response.Json(await request.JsonAsync<Input>());
            })
            .Get("/response-json", async (request, route) => Response.Json(await new Response("{").JsonAsync<Input>()))
            .Get("/internal-json", (request, route) => Response.Json(Json.Parse<Input>("{")))
            .Get("/caught", (request, route) =>
            {
                try { throw new HttpError(418, "teapot"); }
                catch (HttpError error) { return new Response(error.Message) { Status = error.Status }; }
            })
            .Get("/pattern", (request, route) =>
            {
                Exception error = new HttpError(418, "teapot");
                return error is HttpError http ? new Response(http.Message) { Status = http.Status } : new Response { Status = 500 };
            })
            .Get("/downstream-cors", (request, route) => new Response("ok") { Headers = { ["access-control-allow-origin"] = "*", ["access-control-allow-credentials"] = "true" }})
            .Get("/stream", (request, route) => new Response(ReadableStream.From(Chunks())))
            .Get("/head-tee", (request, route) => new Response(new Response("hello").Body!.Tee()[0]) { Status = 201 })
            .Get("/head-cancel-fails", (request, route) => new Response(ReadableStream.From(ChunksWithFailingCleanup())) { Status = 201 })
            .Options("/options", (request, route) => new Response("ordinary options"));

        if (path is "/custom" or "/custom-unexpected") router.OnError(FormatError);
        if (path == "/broken-error-handler") router.OnError(BrokenFormatter);
        if (path == "/async-error-handler") router.OnError(async (error, request, route) =>
        {
            await Scheduler.WaitAsync(1);
            return Response.Json(new { error = error.Message }, status: error.Status, headers: new Headers { ["x-async"] = "true" });
        });
        return await router.HandleAsync(request, env, context);
    }

    private static Response FormatError(RouterError error, Request request, RouteContext route)
    {
        Log.Error(error.Exception.Message);
        return Response.Json(new { error = error.Message, path = new Url(request.Url).Pathname }, status: error.Status);
    }

    private static Response Fail(Request request, RouteContext route) => new Url(request.Url).Pathname switch
    {
        "/http-error" => throw new HttpError(404, "Note not found"),
        "/custom" => throw new HttpError(409, "Conflict"),
        "/async-error-handler" => throw new HttpError(422, "Invalid input"),
        "/broken-error-handler" => throw new HttpError(404, "missing"),
        _ => throw new Exception("secret database password")
    };

    private static Response BrokenFormatter(RouterError error, Request request, RouteContext route) =>
        throw new Exception("secret formatter");

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
    {
        yield return new TextEncoder().Encode("one");
        await Scheduler.WaitAsync(1);
        yield return new TextEncoder().Encode("two");
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ChunksWithFailingCleanup()
    {
        try { yield return new byte[] { 1 }; }
        finally { throw new Exception("cleanup failed"); }
    }
}

public sealed record Input(string Text);
