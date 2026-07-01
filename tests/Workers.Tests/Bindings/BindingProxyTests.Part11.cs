using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task WebSocketProxyDispatchesConnect()
    {
        var dispatcher = new CapturingDispatcher("""{"handle":"ws:connected"}""", "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-websocket-connect");

        var socket = await environment.ConnectWebSocketAsync(
            "wss://echo.example/socket",
            ["chat", "v2"]);
        await socket.SendTextAsync("hello");

        Assert.Equal(["websocket.connect", "websocket.sendText"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$websocket", call.BindingName));

        using var connectPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("wss://echo.example/socket", connectPayload.RootElement.GetProperty("url").GetString());
        Assert.Equal("chat", connectPayload.RootElement.GetProperty("protocols")[0].GetString());
        Assert.Equal("v2", connectPayload.RootElement.GetProperty("protocols")[1].GetString());

        using var sendPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("ws:connected", sendPayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task WebSocketProxyDispatchesReceiveEvents()
    {
        var dispatcher = new CapturingDispatcher(
            """{"client":"ws:1","server":"ws:2"}""",
            """{"event":{"kind":"message","text":"hello","bodyBase64":null,"code":null,"reason":null,"wasClean":null}}""",
            "{\"event\":{\"kind\":\"message\",\"text\":null,\"bodyBase64\":\""
                + Convert.ToBase64String([1, 2, 3])
                + "\",\"code\":null,\"reason\":null,\"wasClean\":null}}",
            """{"event":{"kind":"close","text":null,"bodyBase64":null,"code":1000,"reason":"done","wasClean":true}}""",
            """{"event":null}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-websocket-receive");

        var pair = await environment.WebSocketPairAsync();
        var events = pair.Server.Events();

        var text = await events.NextAsync();
        var bytes = await pair.Server.ReceiveAsync();
        var close = await events.NextAsync();
        var ended = await events.NextAsync();

        Assert.NotNull(text);
        Assert.Equal(WebSocketEventKind.Message, text.Kind);
        Assert.Equal("hello", text.Text);
        Assert.Empty(text.Bytes.ToArray());

        Assert.NotNull(bytes);
        Assert.Equal(WebSocketEventKind.Message, bytes.Kind);
        Assert.Null(bytes.Text);
        Assert.Equal([1, 2, 3], bytes.Bytes.ToArray());

        Assert.NotNull(close);
        Assert.Equal(WebSocketEventKind.Close, close.Kind);
        Assert.Equal((ushort)1000, close.Code);
        Assert.Equal("done", close.Reason);
        Assert.True(close.WasClean);

        Assert.Null(ended);
        Assert.Equal(
            ["websocket.createPair", "websocket.receive", "websocket.receive", "websocket.receive", "websocket.receive"],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$websocket", call.BindingName));

        using var receivePayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("ws:2", receivePayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task RawBindingDispatchesPropertyAndMethodAccess()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":{"ok":true,"count":8}}""",
            """{"value":{"ok":true,"count":9}}""",
            """{"value":null}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-raw-binding");

        var property = await environment.RawBinding("EXPERIMENTAL").GetPropertyAsync<RoomStatus>("state");
        var method = await environment.RawBinding("EXPERIMENTAL").InvokeAsync<RoomStatus>(
            "compute",
            [7, "compact"]);
        await environment.RawBinding("EXPERIMENTAL").InvokeVoidAsync(
            "touch",
            [new { ttl = 45 }]);

        Assert.NotNull(property);
        Assert.True(property.Ok);
        Assert.Equal(8, property.Count);
        Assert.NotNull(method);
        Assert.True(method.Ok);
        Assert.Equal(9, method.Count);
        Assert.Equal(["binding.getProperty", "binding.invoke", "binding.invoke"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("EXPERIMENTAL", call.BindingName));

        using var propertyPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("state", propertyPayload.RootElement.GetProperty("propertyName").GetString());

        using var methodPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("compute", methodPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(7, methodPayload.RootElement.GetProperty("arguments")[0].GetInt32());
        Assert.Equal("compact", methodPayload.RootElement.GetProperty("arguments")[1].GetString());

        using var voidPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("touch", voidPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(45, voidPayload.RootElement.GetProperty("arguments")[0].GetProperty("ttl").GetInt32());
    }

    [Fact]
    public async Task LogDispatchesRuntimeMessages()
    {
        var dispatcher = new CapturingDispatcher("{}", "{}", "{}", "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-log");

        await environment.Log().LogAsync("hello");
        await environment.Log().DebugAsync("details");
        await environment.Log().WarnAsync("careful");
        await environment.Log().ErrorAsync("broken");

        Assert.Equal(["runtime.console", "runtime.console", "runtime.console", "runtime.console"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$runtime", call.BindingName));

        using var logPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("log", logPayload.RootElement.GetProperty("level").GetString());
        Assert.Equal("hello", logPayload.RootElement.GetProperty("message").GetString());

        using var debugPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("debug", debugPayload.RootElement.GetProperty("level").GetString());

        using var warnPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("warn", warnPayload.RootElement.GetProperty("level").GetString());

        using var errorPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("error", errorPayload.RootElement.GetProperty("level").GetString());
    }

    [Fact]
    public async Task PlatformBindingsRequireLiveInvocation()
    {
        var environment = new Env();

        Assert.Throws<WorkersException>(() => environment.Kv("CACHE"));
        Assert.Throws<WorkersException>(() => environment.Queue("JOBS"));
        Assert.Throws<WorkersException>(() => environment.D1("DB"));
        Assert.Throws<WorkersException>(() => environment.Cache());
        Assert.Throws<WorkersException>(() => environment.DurableObject("ROOMS"));
        Assert.Throws<WorkersException>(() => environment.RateLimiter("LOGIN_LIMIT"));
        Assert.Throws<WorkersException>(() => environment.AnalyticsEngine("HTTP_ANALYTICS"));
        Assert.Throws<WorkersException>(() => environment.SendEmail("EMAIL"));
        Assert.Throws<WorkersException>(() => environment.VersionMetadata("CF_VERSION_METADATA"));
        Assert.Throws<WorkersException>(() => environment.Ai("AI"));
        Assert.Throws<WorkersException>(() => environment.Workflow("BILLING"));
        Assert.Throws<WorkersException>(() => environment.Vectorize("DOCS"));
        Assert.Throws<WorkersException>(() => environment.Images("IMAGES"));
        Assert.Throws<WorkersException>(() => environment.Media("MEDIA"));
        Assert.Throws<WorkersException>(() => environment.Assets("ASSETS"));
        Assert.Throws<WorkersException>(() => environment.MtlsCertificate("MY_CERT"));
        Assert.Throws<WorkersException>(() => environment.SecretStore("API_KEY"));
        Assert.Throws<WorkersException>(() => environment.Hyperdrive("DB"));
        Assert.Throws<WorkersException>(() => environment.Crypto());
        Assert.Throws<WorkersException>(() => environment.Log());
        Assert.Throws<WorkersException>(() => environment.DynamicDispatcher("DISPATCHER"));
        Assert.Throws<WorkersException>(() => environment.RawBinding("EXPERIMENTAL"));
        await Assert.ThrowsAsync<WorkersException>(() => environment.DelayAsync(TimeSpan.Zero));
        await Assert.ThrowsAsync<WorkersException>(() => environment.CreateAbortControllerAsync());
        await Assert.ThrowsAsync<WorkersException>(() => environment.FetchAsync("https://example.com"));
        await Assert.ThrowsAsync<WorkersException>(() => environment.WebSocketPairAsync());
        await Assert.ThrowsAsync<WorkersException>(() => environment.ConnectWebSocketAsync("wss://example.com"));
    }
}
