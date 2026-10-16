namespace Workers.Compiler.Tests;

public sealed class RouterTests
{
    [Fact]
    public void EmitsMiddlewareErrorsAndCorsWithNamedArguments()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router()
                        .Use(Cors.Allow(credentials: true, origins: ["https://app.example"], headers: ["content-type"]))
                        .Use(async (request, route, next) =>
                        {
                            var response = await next();
                            return new Response(response.Body, response) { Headers = { ["x-router"] = "csharp" } };
                        })
                        .OnError((error, request, route) => Response.Json(new { error = error.Message }, status: error.Status))
                        .Post("/json", async (request, route) => Response.Json(await request.JsonAsync<object>()))
                        .HandleAsync(request, env, context);
            }
            """);

        Assert.Contains("$workers$corsAllow(", module);
        Assert.Contains("$workers$routerUse(", module);
        Assert.Contains("$workers$routerOnError(", module);
        Assert.Contains("$workers$notNull($workers$routerAdd($workers$notNull($workers$routerOnError($workers$notNull($workers$routerUse($workers$notNull($workers$routerUse($workers$routerCreate(), (($workers$arg1, $workers$arg2, $workers$arg3) => $workers$corsAllow($workers$arg2, null, $workers$arg1, $workers$arg3))(true, [\"https://app.example\"], [\"content-type\"]))), async (request, route, next) => {\n  let response = await (($workers$receiver) => $workers$notNull($workers$receiver)())(next);\r\n  return (($workers$body, $workers$source) => new Response($workers$body, { status: $workers$source.status, statusText: $workers$source.statusText, headers: $workers$source.headers, webSocket: $workers$source.webSocket, cf: $workers$source.cf, headers: (($workers$value) => { $workers$value.set(\"x-router\", \"csharp\"); return $workers$value; })(new Headers($workers$source.headers)) }))($workers$notNull(response).body, response);\n})), (error, request, route) => (($workers$arg1$2, $workers$arg2$2) => Response.json($workers$arg1$2, { status: $workers$arg2$2 }))({ error: $workers$notNull(error).message }, $workers$notNull(error).status))), \"POST\", $workers$route, async (request, route) => Response.json(await $workers$requestJson($workers$notNull(request)))))", module);
        Assert.Contains("$workers$notNull($workers$routerAdd($workers$notNull($workers$routerOnError($workers$notNull($workers$routerUse($workers$notNull($workers$routerUse($workers$routerCreate(), (($workers$arg1, $workers$arg2, $workers$arg3) => $workers$corsAllow($workers$arg2, null, $workers$arg1, $workers$arg3))(true, [\"https://app.example\"], [\"content-type\"]))), async (request, route, next) => {\n  let response = await (($workers$receiver) => $workers$notNull($workers$receiver)())(next);\r\n  return (($workers$body, $workers$source) => new Response($workers$body, { status: $workers$source.status, statusText: $workers$source.statusText, headers: $workers$source.headers, webSocket: $workers$source.webSocket, cf: $workers$source.cf, headers: (($workers$value) => { $workers$value.set(\"x-router\", \"csharp\"); return $workers$value; })(new Headers($workers$source.headers)) }))($workers$notNull(response).body, response);\n})), (error, request, route) => (($workers$arg1$2, $workers$arg2$2) => Response.json($workers$arg1$2, { status: $workers$arg2$2 }))({ error: $workers$notNull(error).message }, $workers$notNull(error).status))), \"POST\", $workers$route, async (request, route) => Response.json(await $workers$requestJson($workers$notNull(request)))))", module);
        Assert.Contains("status: error.status", module);
        Assert.Contains("[\"https://app.example\"]", module);
    }

    [Fact]
    public void DistinguishesHttpErrorsInCatchesAndPatterns()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    try { throw new HttpError(message: "missing", status: 404); }
                    catch (HttpError error)
                    {
                        Exception exception = error;
                        if (exception is HttpError http) return new Response(http.Message) { Status = http.Status };
                        return new Response { Status = 500 };
                    }
                }
            }
            """);

        Assert.Contains("new $workers$HttpError(", module);
        Assert.Contains("instanceof $workers$HttpError", module);
        Assert.Contains("$workers$notNull(http).status", module);
        Assert.Contains("$workers$notNull(http).message", module);
    }

    [Fact]
    public void EmitsRouteDescriptorsOnceAndRegistersNativeHandlers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router()
                        .Get("/users/{id:int}", (request, route) => new Response(route.Parameter("id")))
                        .Delete("/users/{id:int}", async (request, route) => new Response { Status = 204 })
                        .Any("/files/{*path}", (request, route) => new Response(route.Parameter("path")))
                        .Fallback((request, route) => new Response("missing") { Status = 404 })
                        .HandleAsync(request, env, context);
            }
            """);

        Assert.Contains("const $workers$route = { pattern: \"/users/{id:int}\", key: \"/users/{:int}\", rank: [0, 1], segments: [\"users\", { name: \"id\", test: (value) => { try { $workers$numericParse(value, 0); return true; } catch { return false; } } }] };", module);
        Assert.Contains("const $workers$route$2 = { pattern: \"/files/{*path}\", key: \"/files/{*}\", rank: [0, 3], segments: [\"files\", { name: \"path\", rest: true }] };", module);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(module, "pattern: \"/users/"));
        Assert.Contains("$workers$notNull($workers$routerFallback($workers$notNull($workers$routerAdd($workers$notNull($workers$routerAdd($workers$notNull($workers$routerAdd($workers$routerCreate(), \"GET\", $workers$route, (request, route) => new Response($workers$routerParameter($workers$notNull(route), \"id\")))), \"DELETE\", $workers$route, async (request, route) => new Response(null, { status: 204 }))), null, $workers$route$2, (request, route) => new Response($workers$routerParameter($workers$notNull(route), \"path\")))), (request, route) => new Response(\"missing\", { status: 404 })))", module);
        Assert.Contains("\"DELETE\", $workers$route, async (request, route) =>", module);
        Assert.Contains("null, $workers$route$2, (request, route) =>", module);
        Assert.Contains("return $workers$routerHandle($workers$notNull($workers$routerFallback($workers$notNull($workers$routerAdd($workers$notNull($workers$routerAdd($workers$notNull($workers$routerAdd($workers$routerCreate(), \"GET\", $workers$route, (request, route) => new Response($workers$routerParameter($workers$notNull(route), \"id\")))), \"DELETE\", $workers$route, async (request, route) => new Response(null, { status: 204 }))), null, $workers$route$2, (request, route) => new Response($workers$routerParameter($workers$notNull(route), \"path\")))), (request, route) => new Response(\"missing\", { status: 404 })))", module);
        Assert.Contains("request, env, context);", module);
    }

    [Fact]
    public void SupportsStaticRoutersWithMethodGroupHandlers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                private static readonly Router Routes = new Router()
                    .Get("/", Home)
                    .Post("/items/{id:uuid}", SaveAsync);

                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    Routes.HandleAsync(request, env, context);

                private static Response Home(Request request, RouteContext route) => new Response("home");

                private static async Task<Response> SaveAsync(Request request, RouteContext route)
                {
                    var id = route.Parameter("id").ToLowerInvariant();
                    await route.Env.KV("ITEMS").PutAsync(id, await request.TextAsync());
                    return new Response { Status = 201 };
                }
            }
            """);

        Assert.Contains("rank: [], segments: [] };", module);
        Assert.Contains("[0-9a-f]{8}", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).put($workers$arg1$2, $workers$arg2$2))($workers$notNull($workers$notNull(route).env)[\"ITEMS\"], id, await $workers$notNull(request).text())", module);
        Assert.Contains("$workers$routerHandle(", module);
    }

    [Fact]
    public void LeavesRouterHelpersOutOfWorkersThatDoNotRoute()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) => new Response("ok");
            }
            """);

        Assert.DoesNotContain("router", module);
    }

    [Theory]
    [InlineData("users", "it must start with '/'")]
    [InlineData("/users/", "it contains an empty segment; remove the trailing or repeated '/'")]
    [InlineData("/users?page=1", "it must not contain a query or fragment")]
    [InlineData("/users/{id}.json", "the segment '{id}.json' mixes text and a parameter; a parameter must fill a whole segment, such as '/users/{id}'")]
    [InlineData("/users/{id?}", "optional parameters are not supported; register a second route without the parameter")]
    [InlineData("/users/{id:long}", "the constraint 'long' is not supported; use 'int' or 'uuid'")]
    [InlineData("/users/{id}/{id}", "the parameter 'id' appears more than once")]
    [InlineData("/files/{*path}/edit", "the catch-all parameter 'path' must be the last segment")]
    [InlineData("/files/{*path:int}", "the catch-all parameter 'path' cannot have a constraint")]
    [InlineData("/users/{1d}", "'1d' is not a valid parameter name")]
    public void RejectsInvalidPatternsWhenTheWorkerIsBuilt(string pattern, string reason)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router().Get("{{pattern}}", (request, route) => new Response("ok")).HandleAsync(request, env, context);
            }
            """));

        Assert.Equal($"WRK122: The route pattern '{pattern}' is not valid because {reason}.", error.Message);
    }

    [Fact]
    public void RequiresConstantPatterns()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router().Get(env.Variable("ROUTE"), (request, route) => new Response("ok")).HandleAsync(request, env, context);
            }
            """));

        Assert.Equal("WRK122: Route patterns must be compile-time constants so they can be checked when the Worker is built.", error.Message);
    }

    [Fact]
    public void RejectsParametersTheRouteDoesNotDefine()
    {
        var lambda = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router()
                        .Get("/users/{id}", (request, route) => new Response(route.Parameter("userId")))
                        .HandleAsync(request, env, context);
            }
            """));
        var methodGroup = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router().Get("/", Show).HandleAsync(request, env, context);

                private static Response Show(Request request, RouteContext route) => new Response(route.Parameter("id"));
            }
            """));
        var fallback = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext context) =>
                    new Router().Fallback((request, route) => new Response(route.Parameter("id"))).HandleAsync(request, env, context);
            }
            """));

        Assert.Equal("WRK122: The route '/users/{id}' has no parameter named 'userId'; it defines 'id'.", lambda.Message);
        Assert.Equal("WRK122: The route '/' has no parameter named 'id'.", methodGroup.Message);
        Assert.Equal("WRK122: The fallback handler has no route parameters, so it cannot read 'id'.", fallback.Message);
    }
}
