using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task DurableObjectStateDispatchesBlockConcurrencyWhile()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var state = new DurableObjectState(
            "invocation-do-block",
            new DurableObjectId("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"),
            dispatcher);

        state.WaitUntil(Task.CompletedTask);
        await state.BlockConcurrencyWhileAsync(() => Task.CompletedTask);

        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation-do-block", invocation.InvocationId);
        Assert.Equal("$durableObjectState", invocation.BindingName);
        Assert.Equal("durable.state.blockConcurrencyWhile", invocation.Operation);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.StartsWith("do-state:", payload.RootElement.GetProperty("handle").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DurableObjectStateDispatchesHibernatableWebSocketOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"client":"ws:client","server":"ws:server"}""",
            "{}",
            """{"handles":["ws:accepted"]}""",
            """{"tags":["room:1","admin"]}""",
            "{}",
            """{"pair":{"request":"ping","response":"pong"}}""",
            """{"timestamp":1704067200123}""",
            "{}",
            """{"timeoutMilliseconds":2500}""",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-do-hibernation");
        var state = new DurableObjectState(
            "invocation-do-hibernation",
            new DurableObjectId("abababababababababababababababababababababababababababababababab"),
            dispatcher);

        var pair = await environment.WebSocketPairAsync();
        await state.AcceptWebSocketAsync(pair.Server, ["room:1", "admin"]);
        var acceptedSockets = await state.GetWebSocketsAsync("room:1");
        var tags = await state.GetTagsAsync(pair.Server);
        await state.SetWebSocketAutoResponseAsync(new WebSocketAutoResponse("ping", "pong"));
        var autoResponse = await state.GetWebSocketAutoResponseAsync();
        var timestamp = await state.GetWebSocketAutoResponseTimestampAsync(pair.Server);
        await state.SetHibernatableWebSocketEventTimeoutAsync(TimeSpan.FromMilliseconds(2500));
        var timeout = await state.GetHibernatableWebSocketEventTimeoutAsync();
        await acceptedSockets.Single().SendTextAsync("hello");

        Assert.Equal(["room:1", "admin"], tags);
        Assert.NotNull(autoResponse);
        Assert.Equal("ping", autoResponse.Request);
        Assert.Equal("pong", autoResponse.Response);
        Assert.Equal(1704067200123, timestamp?.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.FromMilliseconds(2500), timeout);
        Assert.Equal(
            [
                "websocket.createPair",
                "durable.state.acceptWebSocket",
                "durable.state.getWebSockets",
                "durable.state.getTags",
                "durable.state.setWebSocketAutoResponse",
                "durable.state.getWebSocketAutoResponse",
                "durable.state.getWebSocketAutoResponseTimestamp",
                "durable.state.setHibernatableWebSocketEventTimeout",
                "durable.state.getHibernatableWebSocketEventTimeout",
                "websocket.sendText"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));

        Assert.Equal("$websocket", dispatcher.Invocations[0].BindingName);
        Assert.Equal("$websocket", dispatcher.Invocations[9].BindingName);
        Assert.All(dispatcher.Invocations.Skip(1).Take(8), call => Assert.Equal("$durableObjectState", call.BindingName));

        using var acceptPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("ws:server", acceptPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("room:1", acceptPayload.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("admin", acceptPayload.RootElement.GetProperty("tags")[1].GetString());

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("room:1", getPayload.RootElement.GetProperty("tag").GetString());

        using var autoResponsePayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal("ping", autoResponsePayload.RootElement.GetProperty("pair").GetProperty("request").GetString());
        Assert.Equal("pong", autoResponsePayload.RootElement.GetProperty("pair").GetProperty("response").GetString());

        using var timeoutPayload = JsonDocument.Parse(dispatcher.Invocations[7].PayloadJson);
        Assert.Equal(2500, timeoutPayload.RootElement.GetProperty("timeoutMilliseconds").GetInt64());

        using var sendPayload = JsonDocument.Parse(dispatcher.Invocations[9].PayloadJson);
        Assert.Equal("ws:accepted", sendPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("hello", sendPayload.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DurableObjectStateRejectsInvalidHibernatableWebSocketTimeout()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var state = new DurableObjectState(
            "invocation-do-timeout",
            new DurableObjectId("cdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcdcd"),
            dispatcher);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            state.SetHibernatableWebSocketEventTimeoutAsync(TimeSpan.FromMilliseconds(-1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            state.SetHibernatableWebSocketEventTimeoutAsync(TimeSpan.FromDays(7).Add(TimeSpan.FromMilliseconds(1))));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task GlobalFetchDispatchesRequestEnvelope()
    {
        var response = ResponseEnvelope.FromResponse(
            Response.Json(new { ok = true }, 207, statusText: "Multi-Status"));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch");

        var result = await environment.FetchAsync(Request.Post(
            "https://origin.example/api",
            Body.Json(new { name = "Ada" })));

        Assert.Equal(207, result.Status);
        Assert.Equal("Multi-Status", result.StatusText);
        Assert.True(result.Body.AsJson<JsonElement>().GetProperty("ok").GetBoolean());
        Assert.Equal("fetch.global", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$fetch", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("POST", payload.RootElement.GetProperty("request").GetProperty("method").GetString());
        Assert.Equal("https://origin.example/api", payload.RootElement.GetProperty("request").GetProperty("url").GetString());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("options").ValueKind);
    }

    [Fact]
    public async Task GlobalFetchCanUseAbortSignal()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("abortable"));
        var dispatcher = new CapturingDispatcher(
            """{"handle":"abort:1"}""",
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-abort");

        var controller = await environment.CreateAbortControllerAsync();
        var result = await environment.FetchAsync(
            "https://origin.example/slow",
            new FetchOptions { Signal = controller.Signal });
        await controller.AbortAsync("done");

        Assert.Equal("abortable", result.Body.AsText());
        Assert.Equal(["abort.create", "fetch.global", "abort.abort"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.Equal(["$abort", "$fetch", "$abort"], dispatcher.Invocations.Select(static call => call.BindingName));

        using var fetchPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("abort:1", fetchPayload.RootElement.GetProperty("options").GetProperty("signalHandle").GetString());
        Assert.Equal("https://origin.example/slow", fetchPayload.RootElement.GetProperty("request").GetProperty("url").GetString());

        using var abortPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("abort:1", abortPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("done", abortPayload.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GlobalFetchDispatchesRequestInitOptions()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("init"));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-options");

        var result = await environment.FetchAsync(
            "https://origin.example/cache",
            new FetchOptions
            {
                Mode = RequestMode.Cors,
                Credentials = RequestCredentials.Include,
                Referrer = "https://referrer.example/page",
                ReferrerPolicy = ReferrerPolicy.StrictOriginWhenCrossOrigin,
                Redirect = RequestRedirect.Manual,
                Cache = RequestCache.Reload,
                Integrity = "sha256-test",
                KeepAlive = true
            });

        Assert.Equal("init", result.Body.AsText());
        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        var options = payload.RootElement.GetProperty("options");
        Assert.Equal("cors", options.GetProperty("mode").GetString());
        Assert.Equal("include", options.GetProperty("credentials").GetString());
        Assert.Equal("https://referrer.example/page", options.GetProperty("referrer").GetString());
        Assert.Equal("strict-origin-when-cross-origin", options.GetProperty("referrerPolicy").GetString());
        Assert.Equal("manual", options.GetProperty("redirect").GetString());
        Assert.Equal("reload", options.GetProperty("cache").GetString());
        Assert.Equal("sha256-test", options.GetProperty("integrity").GetString());
        Assert.True(options.GetProperty("keepAlive").GetBoolean());
    }

    [Fact]
    public async Task GlobalFetchDispatchesForceCacheRequestCacheMode()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("cache"));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-cache");

        await environment.FetchAsync(
            "https://origin.example/cache",
            new FetchOptions { Cache = RequestCache.ForceCache });

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("force-cache", payload.RootElement.GetProperty("options").GetProperty("cache").GetString());
    }

    [Fact]
    public async Task GlobalFetchDispatchesOnlyIfCachedWithSameOriginMode()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("cache"));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-only-if-cached");

        await environment.FetchAsync(
            "https://origin.example/cache",
            new FetchOptions
            {
                Mode = RequestMode.SameOrigin,
                Cache = RequestCache.OnlyIfCached
            });

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        var options = payload.RootElement.GetProperty("options");
        Assert.Equal("same-origin", options.GetProperty("mode").GetString());
        Assert.Equal("only-if-cached", options.GetProperty("cache").GetString());
    }

    [Fact]
    public async Task FetchRejectsOnlyIfCachedWithoutSameOriginMode()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-only-if-cached-invalid");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            environment.FetchAsync(
                "https://origin.example/cache",
                new FetchOptions { Cache = RequestCache.OnlyIfCached }));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task FetchRejectsUnsupportedRequestInitOptions()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-init-invalid");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            environment.FetchAsync(
                "https://origin.example/value",
                new FetchOptions { Mode = RequestMode.Navigate }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            environment.FetchAsync(
                "https://origin.example/value",
                new FetchOptions { Redirect = RequestRedirect.Error }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            environment.FetchAsync(
                "https://origin.example/value",
                new FetchOptions { Referrer = "not a url" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            environment.FetchAsync(
                "https://origin.example/value",
                new FetchOptions { Referrer = "/relative" }));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task GlobalFetchAllowsSupportedReferrerValues()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("referrer"));
        var dispatcher = new CapturingDispatcher(
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-fetch-referrer");

        await environment.FetchAsync(
            "https://origin.example/value",
            new FetchOptions { Referrer = "about:client" });
        await environment.FetchAsync(
            "https://origin.example/value",
            new FetchOptions { Referrer = "data:text/plain,x" });

        Assert.Equal(2, dispatcher.Invocations.Count);
        using var aboutPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        using var dataPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("about:client", aboutPayload.RootElement.GetProperty("options").GetProperty("referrer").GetString());
        Assert.Equal("data:text/plain,x", dataPayload.RootElement.GetProperty("options").GetProperty("referrer").GetString());
    }
}
