using System.Text.Json;
using System.Text.Json.Serialization;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed class RouterTests
{
    [Fact]
    public async Task RunsMatchingParameterizedRoute()
    {
        var router = new Router()
            .Get("/users/:id", static (_, context) =>
                Task.FromResult(Response.Text(context.Param("id") ?? "")));

        var response = await router.RunAsync(
            Request.Get("https://example.com/users/123"),
            new Env());

        Assert.Equal(200, response.Status);
        Assert.Equal("123", response.Body.AsText());
    }

    [Fact]
    public async Task RouteParametersProvideSafeAndTypedAccess()
    {
        var router = new Router()
            .Get("/users/:user_id/posts/:slug", static (_, context) =>
            {
                Assert.True(context.TryParam("slug", out var slug));
                Assert.Equal("hello-world", slug);
                Assert.Equal("123", context.RequiredParam("user_id"));
                Assert.False(context.TryParam("missing", out var missing));
                Assert.Null(missing);
                Assert.Throws<WorkersException>(() => context.RequiredParam("missing"));

                var route = context.Params<PostRoute>();
                return Task.FromResult(Response.Text($"{route.UserId}:{route.Slug}"));
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/users/123/posts/hello-world"),
            new Env());

        Assert.Equal("123:hello-world", response.Body.AsText());
    }

    [Fact]
    public async Task RouteParametersRejectInvalidTypedValues()
    {
        var router = new Router()
            .Get("/users/:user_id", static (_, context) =>
            {
                Assert.Throws<WorkersException>(() => context.Params<PostRoute>());
                return Task.FromResult(Response.Empty());
            });

        await router.RunAsync(
            Request.Get("https://example.com/users/nope"),
            new Env());
    }

    [Fact]
    public async Task ReturnsMethodNotAllowedWhenPathMatchesDifferentMethod()
    {
        var router = new Router()
            .Post("/users/:id", static (_, _) => Task.FromResult(Response.Empty()))
            .Put("/users/:id", static (_, _) => Task.FromResult(Response.Empty()));

        var response = await router.RunAsync(
            Request.Get("https://example.com/users/123"),
            new Env());

        Assert.Equal(405, response.Status);
        Assert.Equal("POST, PUT", response.Headers.Get("allow"));
    }

    [Fact]
    public async Task RunsHeadOptionsAndCustomMethodRoutes()
    {
        var router = new Router()
            .Head("/resource", static (_, _) => Task.FromResult(Response.Empty(204)))
            .Options("/resource", static (_, _) => Task.FromResult(Response.Empty(204)))
            .Method("trace", "/resource", static (request, _) => Task.FromResult(Response.Text(request.Method)));

        var head = await router.RunAsync(
            Request.Head("https://example.com/resource"),
            new Env());
        var options = await router.RunAsync(
            Request.Create("https://example.com/resource", "OPTIONS"),
            new Env());
        var trace = await router.RunAsync(
            Request.Create("https://example.com/resource", "TRACE"),
            new Env());

        Assert.Equal(204, head.Status);
        Assert.Equal(204, options.Status);
        Assert.Equal("TRACE", trace.Body.AsText());
    }

    [Fact]
    public async Task RunsCustomMethodNotAllowedHandlerWithAllowedMethodsAndParameters()
    {
        var router = new Router()
            .Get("/users/:id", static (_, _) => Task.FromResult(Response.Empty()))
            .Post("/users/:id", static (_, _) => Task.FromResult(Response.Empty()))
            .MethodNotAllowed(static (_, context) =>
                Task.FromResult(Response.Text($"{context.Param("id")}:{string.Join('|', context.AllowedMethods)}", 405)));

        var response = await router.RunAsync(
            Request.Delete("https://example.com/users/123"),
            new Env());

        Assert.Equal(405, response.Status);
        Assert.Equal("123:GET|POST", response.Body.AsText());
    }

    [Fact]
    public async Task RunsCustomNotFoundHandler()
    {
        var router = new Router()
            .Get("/users/:id", static (_, _) => Task.FromResult(Response.Empty()))
            .NotFound(static (request, context) =>
                Task.FromResult(Response.Text($"{request.Path}:{context.AllowedMethods.Count}", 404)));

        var response = await router.RunAsync(
            Request.Get("https://example.com/missing"),
            new Env());

        Assert.Equal(404, response.Status);
        Assert.Equal("/missing:0", response.Body.AsText());
    }

    [Fact]
    public async Task ProvidesRouterDataToRouteHandlers()
    {
        var router = new Router()
            .WithData(new AppState("Workers"))
            .Get("/state", static (_, context) =>
            {
                var state = context.Data<AppState>();
                return Task.FromResult(Response.Text(state.Name));
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/state"),
            new Env());

        Assert.Equal("Workers", response.Body.AsText());
    }

    [Fact]
    public async Task ProvidesRouterDataToFallbackHandlers()
    {
        var router = new Router()
            .WithData(new AppState("fallback"))
            .Get("/state", static (_, _) => Task.FromResult(Response.Empty()))
            .NotFound(static (_, context) =>
            {
                var hasState = context.TryGetData<AppState>(out var state);
                return Task.FromResult(Response.Text($"{hasState}:{state?.Name}", 404));
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/missing"),
            new Env());

        Assert.Equal(404, response.Status);
        Assert.Equal("True:fallback", response.Body.AsText());
    }

    [Fact]
    public async Task RouterDataAccessFailsWhenMissingOrWrongType()
    {
        var missingDataRouter = new Router()
            .Get("/state", static (_, context) =>
            {
                Assert.False(context.TryGetData<AppState>(out AppState? state));
                Assert.Null(state);
                Assert.Throws<WorkersException>(() => context.Data<AppState>());
                return Task.FromResult(Response.Empty());
            });
        var wrongTypeRouter = new Router()
            .WithData("not-state")
            .Get("/state", static (_, context) =>
            {
                Assert.False(context.TryGetData<AppState>(out AppState? state));
                Assert.Null(state);
                Assert.Throws<WorkersException>(() => context.Data<AppState>());
                return Task.FromResult(Response.Empty());
            });

        await missingDataRouter.RunAsync(
            Request.Get("https://example.com/state"),
            new Env());
        await wrongTypeRouter.RunAsync(
            Request.Get("https://example.com/state"),
            new Env());
    }

    [Fact]
    public async Task RouteContextProvidesEnvironmentBindingShortcuts()
    {
        var router = new Router()
            .Get("/bindings", static (_, context) =>
            {
                var config = context.ObjectVar<AppConfig>("CONFIG");

                Assert.NotNull(context.RawBinding("RAW"));
                Assert.IsAssignableFrom<IKvNamespace>(context.Kv("KV"));
                Assert.IsAssignableFrom<IR2Bucket>(context.Bucket("BUCKET"));
                Assert.IsAssignableFrom<IServiceBinding>(context.Service("API"));
                Assert.IsAssignableFrom<IFetcherBinding>(context.Assets("ASSETS"));
                Assert.IsAssignableFrom<IFetcherBinding>(context.MtlsCertificate("MTLS"));
                Assert.IsAssignableFrom<IDynamicDispatcherBinding>(context.DynamicDispatcher("DISPATCHER"));
                Assert.IsAssignableFrom<IQueueProducer>(context.Queue("QUEUE"));
                Assert.IsAssignableFrom<ID1Database>(context.D1("DB"));
                Assert.IsAssignableFrom<ICache>(context.Cache());
                Assert.IsAssignableFrom<ICache>(context.Cache("named"));
                Assert.IsAssignableFrom<IDurableObjectNamespace>(context.DurableObject("ROOMS"));
                Assert.IsAssignableFrom<IRateLimiter>(context.RateLimiter("LIMITER"));
                Assert.IsAssignableFrom<IAnalyticsEngineDataset>(context.AnalyticsEngine("ANALYTICS"));
                Assert.IsAssignableFrom<ISendEmailBinding>(context.SendEmail("EMAIL"));
                Assert.IsAssignableFrom<IVersionMetadataBinding>(context.VersionMetadata("VERSION"));
                Assert.IsAssignableFrom<IAiBinding>(context.Ai("AI"));
                Assert.IsAssignableFrom<IWorkflowBinding>(context.Workflow("WORKFLOW"));
                Assert.IsAssignableFrom<IImagesBinding>(context.Images("IMAGES"));
                Assert.IsAssignableFrom<IMediaBinding>(context.Media("MEDIA"));
                Assert.IsAssignableFrom<IVectorizeIndex>(context.Vectorize("VECTORIZE"));
                Assert.IsAssignableFrom<ISecretStoreBinding>(context.SecretStore("SECRET_STORE"));
                Assert.IsAssignableFrom<IHyperdriveBinding>(context.Hyperdrive("HYPERDRIVE"));

                return Task.FromResult(Response.Text(
                    $"{context.Var("MESSAGE")}:{context.Secret("TOKEN")}:{config.Enabled}"));
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/bindings"),
            EnvironmentWithInvocation());

        Assert.Equal("hello:secret:True", response.Body.AsText());
    }

    [Fact]
    public async Task RouteContextProvidesExecutionContextShortcuts()
    {
        var background = Task.CompletedTask;
        var executionContext = new ContextEnvelope(
            JsonSerializer.SerializeToElement(new { clientId = "frontend" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .ToExecutionContext();
        var router = new Router()
            .Get("/context", (_, context) =>
            {
                var props = context.Props<RouteProps>();
                context.WaitUntil(background);
                context.PassThroughOnException();
                return Task.FromResult(Response.Text(props.ClientId));
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/context"),
            new Env(),
            executionContext);

        Assert.Equal("frontend", response.Body.AsText());
        Assert.Same(background, Assert.Single(executionContext.PendingTasks));
        Assert.True(executionContext.PassThroughOnExceptionRequested);
    }

    [Fact]
    public async Task RouteContextProvidesRuntimeHelperShortcuts()
    {
        var dispatcher = new CapturingDispatcher(invocation => invocation.Operation switch
        {
            "fetch.global" => JsonSerializer.Serialize(
                ResponseEnvelope.FromResponse(Response.Text("origin")),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "runtime.delay" => "{}",
            "runtime.console" => "{}",
            _ => throw new InvalidOperationException(invocation.Operation)
        });
        using var _ = BindingDispatcher.Use(dispatcher);
        var router = new Router()
            .Get("/runtime", async (_, context) =>
            {
                var fetched = await context.FetchAsync("https://origin.example/value");
                await context.DelayAsync(TimeSpan.FromMilliseconds(1));
                await context.Log().LogAsync("routed");
                Assert.NotNull(context.Crypto());
                return Response.Text(fetched.Text());
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/runtime"),
            EnvironmentWithInvocation());

        Assert.Equal("origin", response.Body.AsText());
        Assert.Equal(["fetch.global", "runtime.delay", "runtime.console"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("invocation-route-context", invocation.InvocationId));
    }

    [Fact]
    public async Task RouteContextProvidesRuntimeOperationShortcuts()
    {
        var dispatcher = new CapturingDispatcher(invocation => invocation.Operation switch
        {
            "socket.connect" => """{"handle":"tcp:route"}""",
            "websocket.createPair" => """{"client":"ws:client","server":"ws:server"}""",
            "websocket.connect" => """{"handle":"ws:connected"}""",
            "abort.create" => """{"handle":"abort:route"}""",
            _ => throw new InvalidOperationException(invocation.Operation)
        });
        using var _ = BindingDispatcher.Use(dispatcher);
        var router = new Router()
            .Get("/runtime-ops", async (_, context) =>
            {
                var socket = await context.ConnectSocketAsync(
                    new SocketAddress("db.example", 5432),
                    new SocketOptions { SecureTransport = SocketSecureTransport.On });
                var textAddressSocket = await context.ConnectSocketAsync("cache.example:6379");
                var pair = await context.WebSocketPairAsync();
                var connected = await context.ConnectWebSocketAsync("wss://echo.example/socket", ["chat", "v2"]);
                var controller = await context.CreateAbortControllerAsync();

                Assert.NotNull(socket);
                Assert.NotNull(textAddressSocket);
                Assert.NotNull(pair.Client);
                Assert.NotNull(pair.Server);
                Assert.NotNull(connected);
                Assert.NotNull(controller.Signal);

                return Response.Text("ok");
            });

        var response = await router.RunAsync(
            Request.Get("https://example.com/runtime-ops"),
            EnvironmentWithInvocation());

        Assert.Equal("ok", response.Body.AsText());
        Assert.Equal(
            ["socket.connect", "socket.connect", "websocket.createPair", "websocket.connect", "abort.create"],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.Equal(
            ["$socket", "$socket", "$websocket", "$websocket", "$abort"],
            dispatcher.Invocations.Select(static call => call.BindingName));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("invocation-route-context", invocation.InvocationId));

        using var addressPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("db.example", addressPayload.RootElement.GetProperty("address").GetProperty("hostname").GetString());
        Assert.Equal(5432, addressPayload.RootElement.GetProperty("address").GetProperty("port").GetInt32());
        Assert.Equal("on", addressPayload.RootElement.GetProperty("options").GetProperty("secureTransport").GetString());

        using var textAddressPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("cache.example:6379", textAddressPayload.RootElement.GetProperty("addressText").GetString());

        using var websocketPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("wss://echo.example/socket", websocketPayload.RootElement.GetProperty("url").GetString());
        Assert.Equal("chat", websocketPayload.RootElement.GetProperty("protocols")[0].GetString());
        Assert.Equal("v2", websocketPayload.RootElement.GetProperty("protocols")[1].GetString());
    }

    [Fact]
    public async Task CapturesTrailingWildcard()
    {
        var router = new Router()
            .Get("/files/*path", static (_, context) =>
                Task.FromResult(Response.Text(context.Param("path") ?? "")));

        var response = await router.RunAsync(
            Request.Get("https://example.com/files/a/b/c.txt"),
            new Env());

        Assert.Equal("a/b/c.txt", response.Body.AsText());
    }

    private sealed record AppState(string Name);

    private sealed record AppConfig(bool Enabled);

    private sealed record RouteProps(string ClientId);

    private sealed record PostRoute([property: JsonPropertyName("user_id")] int UserId, string Slug);

    private static Env EnvironmentWithInvocation()
    {
        var json = JsonSerializer.Serialize(
            new
            {
                invocationId = "invocation-route-context",
                bindings = new Dictionary<string, object?>
                {
                    ["MESSAGE"] = "hello",
                    ["TOKEN"] = "secret",
                    ["CONFIG"] = new { enabled = true }
                }
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return JsonSerializer.Deserialize<EnvEnvelope>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ToEnvironment();
    }

    private sealed class CapturingDispatcher : IBindingDispatcher
    {
        private readonly Func<BindingInvocation, string> _dispatch;

        public CapturingDispatcher(Func<BindingInvocation, string> dispatch)
        {
            _dispatch = dispatch;
        }

        public List<BindingInvocation> Invocations { get; } = [];

        public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(invocation);
            return Task.FromResult(_dispatch(invocation));
        }
    }
}
