using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed class ResponseTests
{
    [Fact]
    public void TextResponseSetsContentType()
    {
        var response = Response.Text("hello");

        Assert.Equal(200, response.Status);
        Assert.Equal("text/plain; charset=utf-8", response.Headers.Get("content-type"));
        Assert.Equal("hello", response.Body.AsText());
    }

    [Fact]
    public void ErrorRequiresErrorStatusCode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Response.Error("nope", 200));
    }

    [Fact]
    public void RedirectRequiresRedirectStatusCode()
    {
        var response = Response.Redirect("https://example.com", 307);

        Assert.Equal(307, response.Status);
        Assert.Equal("https://example.com", response.Headers.Get("location"));
    }

    [Fact]
    public void ResponseCanSetStatusText()
    {
        var response = Response.Text("created", 201, "Created by Worker")
            .WithStatusText("Stored")
            .WithStatus(202, "Accepted by Edge");

        Assert.Equal(202, response.Status);
        Assert.Equal("Accepted by Edge", response.StatusText);
        Assert.Equal("created", response.Text());
    }

    [Fact]
    public void ResponseCanCarryCloudflareMetadata()
    {
        var response = Response.Text("hello")
            .WithCf(new { cacheTtl = 60, cacheEverything = true });

        var cf = response.CfAs<ResponseCf>();

        Assert.Equal(60, cf.CacheTtl);
        Assert.True(cf.CacheEverything);
    }

    [Fact]
    public void ResponseCanSetManualBodyEncoding()
    {
        var response = Response.Bytes([31, 139], contentType: "application/gzip")
            .WithHeader("content-encoding", "gzip")
            .WithEncodeBody(ResponseEncodeBody.Manual);

        Assert.Equal(ResponseEncodeBody.Manual, response.EncodeBody);
        Assert.Equal("gzip", response.Headers.Get("content-encoding"));
    }

    [Fact]
    public void HeaderHelpersAppendAndRemoveResponseHeaders()
    {
        var original = Response.Json(new { ok = true }, 202)
            .WithHeader("x-test", "one")
            .AppendHeader("set-cookie", "a=1")
            .AppendHeader("set-cookie", "b=2")
            .WithCf(new { cacheTtl = 60 })
            .WithEncodeBody(ResponseEncodeBody.Manual);

        var withoutCookies = original.WithoutHeader("set-cookie");

        Assert.Equal(["a=1", "b=2"], original.Headers.GetAll("set-cookie"));
        Assert.Equal(202, withoutCookies.Status);
        Assert.Equal("one", withoutCookies.Headers.Get("x-test"));
        Assert.Null(withoutCookies.Headers.Get("set-cookie"));
        Assert.True(withoutCookies.Json<JsonElement>().GetProperty("ok").GetBoolean());
        Assert.Equal(60, withoutCookies.CfAs<ResponseCf>().CacheTtl);
        Assert.Equal(ResponseEncodeBody.Manual, withoutCookies.EncodeBody);
        Assert.Equal(["a=1", "b=2"], original.Headers.GetAll("set-cookie"));
    }

    [Fact]
    public void ResponseWithoutCloudflareMetadataCannotDeserializeIt()
    {
        var response = Response.Empty();

        Assert.Throws<WorkersException>(() => response.CfAs<JsonElement>());
    }

    [Fact]
    public void ResponseReadsBodyAsTextJsonAndBytes()
    {
        var json = Response.Json(new { ok = true });
        var bytes = Response.Bytes([1, 2, 3]);

        Assert.True(json.Json<JsonElement>().GetProperty("ok").GetBoolean());
        Assert.Equal("""{"ok":true}""", json.Text());
        Assert.Equal([1, 2, 3], bytes.Bytes().ToArray());
    }

    [Fact]
    public void CloneCreatesIndependentResponseHeaders()
    {
        var original = Response.Text("hello", 202, "Accepted")
            .WithHeader("x-test", "one")
            .WithCf(new { cacheTtl = 60 })
            .WithEncodeBody(ResponseEncodeBody.Manual);

        var clone = original.Clone();
        original.WithHeader("x-test", "changed");
        clone.WithHeader("x-clone", "yes");

        Assert.Equal("changed", original.Headers.Get("x-test"));
        Assert.Null(original.Headers.Get("x-clone"));
        Assert.Equal("one", clone.Headers.Get("x-test"));
        Assert.Equal("yes", clone.Headers.Get("x-clone"));
        Assert.Equal(202, clone.Status);
        Assert.Equal("Accepted", clone.StatusText);
        Assert.Equal("hello", clone.Text());
        Assert.Equal(60, clone.CfAs<ResponseCf>().CacheTtl);
        Assert.Equal(ResponseEncodeBody.Manual, clone.EncodeBody);
    }

    [Fact]
    public void ResponseJsonUsesWebDefaults()
    {
        var response = Response.Json(new JsonPayload { ClientId = "frontend" });

        Assert.Equal("""{"clientId":"frontend"}""", response.Text());
        Assert.Equal("frontend", response.Json<JsonPayload>()!.ClientId);
    }

    [Fact]
    public void ResponseCanReturnModifiedCopies()
    {
        var original = Response.Text("hello")
            .WithHeader("x-original", "yes");
        var headers = new Headers().Set("x-replacement", "ok");

        var changedStatus = original.WithStatus(201);
        var changedHeaders = original.WithHeaders(headers);
        var changedBody = Response.Empty()
            .WithHeader("x-body", "yes")
            .WithBody(Body.Json(new { ok = true }));

        headers.Set("x-replacement", "changed");
        original.Headers.Set("x-original", "changed");

        Assert.Equal(201, changedStatus.Status);
        Assert.Equal("hello", changedStatus.Text());
        Assert.Equal("yes", changedStatus.Headers.Get("x-original"));

        Assert.Equal("ok", changedHeaders.Headers.Get("x-replacement"));
        Assert.Null(changedHeaders.Headers.Get("x-original"));

        Assert.True(changedBody.Json<JsonElement>().GetProperty("ok").GetBoolean());
        Assert.Equal("application/json", changedBody.Headers.Get("content-type"));
        Assert.Equal("yes", changedBody.Headers.Get("x-body"));
    }

    [Fact]
    public void WithBodyConveniencesUpdateBodyOwnedContentType()
    {
        var json = Response.Text("old")
            .WithJson(new JsonPayload { ClientId = "frontend" });
        var html = json.WithHtml("<p>ok</p>");
        var bytes = html.WithBytes([1, 2, 3], "application/custom");
        var empty = bytes.WithBody(Body.Empty);

        Assert.Equal("application/json", json.Headers.Get("content-type"));
        Assert.Equal("""{"clientId":"frontend"}""", json.Text());
        Assert.Equal("text/html; charset=utf-8", html.Headers.Get("content-type"));
        Assert.Equal("<p>ok</p>", html.Text());
        Assert.Equal("application/custom", bytes.Headers.Get("content-type"));
        Assert.Equal([1, 2, 3], bytes.Bytes().ToArray());
        Assert.True(empty.Body.IsEmpty);
        Assert.False(empty.Headers.Contains("content-type"));
    }

    [Fact]
    public void WithBodyConveniencesPreserveCustomContentType()
    {
        var response = Response.Json(new { ok = true })
            .WithHeader("content-type", "application/problem+json")
            .WithText("bad request")
            .WithJson(new JsonPayload { ClientId = "frontend" });

        Assert.Equal("application/problem+json", response.Headers.Get("content-type"));
        Assert.Equal("""{"clientId":"frontend"}""", response.Text());
    }

    [Fact]
    public void ResponseFromBodyUsesBodyContentType()
    {
        var response = Response.FromBody(Body.Json(new { ok = true }), 202);

        Assert.Equal(202, response.Status);
        Assert.Equal("application/json", response.Headers.Get("content-type"));
        Assert.True(response.Json<JsonElement>().GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void ResponseBuilderCreatesConfiguredResponse()
    {
        var response = Response.Builder()
            .WithStatus(202, "Accepted by Worker")
            .WithHeader("x-test", "one")
            .AppendHeader("set-cookie", "a=1")
            .AppendHeader("set-cookie", "b=2")
            .WithJson(new { ok = true })
            .WithCf(new { cacheTtl = 60 })
            .WithEncodeBody(ResponseEncodeBody.Manual)
            .Build();

        Assert.Equal(202, response.Status);
        Assert.Equal("Accepted by Worker", response.StatusText);
        Assert.Equal("one", response.Headers.Get("x-test"));
        Assert.Equal(["a=1", "b=2"], response.Headers.GetAll("set-cookie"));
        Assert.Equal("application/json", response.Headers.Get("content-type"));
        Assert.True(response.Body.AsJson<JsonElement>().GetProperty("ok").GetBoolean());
        Assert.Equal(60, response.CfAs<ResponseCf>().CacheTtl);
        Assert.Equal(ResponseEncodeBody.Manual, response.EncodeBody);
    }

    [Fact]
    public void ResponseBuilderPreservesExplicitContentType()
    {
        var response = Response.Builder()
            .WithHeader("content-type", "application/problem+json")
            .WithJson(new { error = "bad" })
            .Build();

        Assert.Equal("application/problem+json", response.Headers.Get("content-type"));
    }

    [Fact]
    public void ResponseBuilderUpdatesBodyContentTypeWhenBodyChanges()
    {
        var response = Response.Builder()
            .WithText("hello")
            .WithJson(new { ok = true })
            .Build();

        Assert.Equal("application/json", response.Headers.Get("content-type"));
        Assert.True(response.Body.AsJson<JsonElement>().GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void ResponseBuilderRemovesAutomaticContentTypeForEmptyBody()
    {
        var response = Response.Builder()
            .WithText("hello")
            .WithBody(Body.Empty)
            .Build();

        Assert.True(response.Body.IsEmpty);
        Assert.False(response.Headers.Contains("content-type"));
    }

    [Fact]
    public void ResponseBuilderUsesResponseStatusValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Response.Builder(99).Build());
        Assert.Throws<ArgumentException>(() => Response.Empty(statusText: "bad\r\ntext"));
    }

    [Fact]
    public async Task WebSocketResponseUsesSwitchingProtocolsStatus()
    {
        var dispatcher = new CapturingDispatcher("""{"client":"ws:client","server":"ws:server"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-response");
        var pair = await environment.WebSocketPairAsync();

        var response = Response.FromWebSocket(pair.Client);

        Assert.Equal(101, response.Status);
        Assert.Same(pair.Client, response.WebSocket);
        Assert.True(response.Body.IsEmpty);
    }

    private static Env EnvironmentWithInvocation(string invocationId)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                invocationId,
                bindings = new Dictionary<string, object?>()
            },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        return System.Text.Json.JsonSerializer.Deserialize<Workers.Interop.EnvEnvelope>(
            json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.ToEnvironment();
    }

    private sealed class CapturingDispatcher : IBindingDispatcher
    {
        private readonly string _result;

        public CapturingDispatcher(string result)
        {
            _result = result;
        }

        public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }
    }

    private sealed class ResponseCf
    {
        public int CacheTtl { get; init; }

        public bool CacheEverything { get; init; }
    }

    private sealed class JsonPayload
    {
        public string ClientId { get; init; } = "";
    }
}
