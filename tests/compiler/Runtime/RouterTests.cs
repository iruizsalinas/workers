namespace Workers.Compiler.Tests;

public sealed class RouterTests
{
    [Fact]
    public void EmitsRouteDescriptorsOnceAndRegistersNativeHandlers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    new Router()
                        .Get("/users/{id:int}", (request, route) => Response.Text(route.Parameter("id")))
                        .Delete("/users/{id:int}", async (request, route) => Response.Empty(204))
                        .Any("/files/{*path}", (request, route) => Response.Text(route.Parameter("path")))
                        .Fallback((request, route) => Response.Text("missing", 404))
                        .HandleAsync(request, env, context);
            }
            """);

        Assert.Contains("const $workers$route = { pattern: \"/users/{id:int}\", key: \"/users/{:int}\", rank: [0, 1], segments: [\"users\", { name: \"id\", test: (value) => { try { $workers$numericParse(value, 0); return true; } catch { return false; } } }] };", module);
        Assert.Contains("const $workers$route$2 = { pattern: \"/files/{*path}\", key: \"/files/{*}\", rank: [0, 3], segments: [\"files\", { name: \"path\", rest: true }] };", module);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(module, "pattern: \"/users/"));
        Assert.Contains("$workers$routerAdd($workers$routerCreate(), \"GET\", $workers$route, (request, route) => new Response($workers$routerParameter(route, \"id\")))", module);
        Assert.Contains("\"DELETE\", $workers$route, async (request, route) =>", module);
        Assert.Contains("null, $workers$route$2, (request, route) =>", module);
        Assert.Contains("return $workers$routerHandle($workers$routerFallback(", module);
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
                    .Post("/items/{id:guid}", SaveAsync);

                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    Routes.HandleAsync(request, env, context);

                private static Response Home(Request request, RouteContext route) => Response.Text("home");

                private static async Task<Response> SaveAsync(Request request, RouteContext route)
                {
                    var id = Guid.Parse(route.Parameter("id"));
                    await route.Env.Kv("ITEMS").PutTextAsync(id.ToString(), await request.TextAsync());
                    return Response.Empty(201);
                }
            }
            """);

        Assert.Contains("rank: [], segments: [] };", module);
        Assert.Contains("$workers$guidParse(value)", module);
        Assert.Contains("route.env[\"ITEMS\"]", module);
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
                public static Response Fetch(Request request, Env env, Context context) => Response.Text("ok");
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
    [InlineData("/users/{id:long}", "the constraint 'long' is not supported; use 'guid' or 'int'")]
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
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    new Router().Get("{{pattern}}", (request, route) => Response.Text("ok")).HandleAsync(request, env, context);
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
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    new Router().Get(env.Variable("ROUTE"), (request, route) => Response.Text("ok")).HandleAsync(request, env, context);
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
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    new Router()
                        .Get("/users/{id}", (request, route) => Response.Text(route.Parameter("userId")))
                        .HandleAsync(request, env, context);
            }
            """));
        var methodGroup = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    new Router().Get("/", Show).HandleAsync(request, env, context);

                private static Response Show(Request request, RouteContext route) => Response.Text(route.Parameter("id"));
            }
            """));
        var fallback = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, Context context) =>
                    new Router().Fallback((request, route) => Response.Text(route.Parameter("id"))).HandleAsync(request, env, context);
            }
            """));

        Assert.Equal("WRK122: The route '/users/{id}' has no parameter named 'userId'; it defines 'id'.", lambda.Message);
        Assert.Equal("WRK122: The route '/' has no parameter named 'id'.", methodGroup.Message);
        Assert.Equal("WRK122: The fallback handler has no route parameters, so it cannot read 'id'.", fallback.Message);
    }
}
