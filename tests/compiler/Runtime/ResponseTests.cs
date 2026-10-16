namespace Workers.Compiler.Tests;

public sealed class ResponseTests
{
    [Fact]
    public void EmitsCloudflareOptionsWithoutAdditionalRuntimeWrappers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var response = new Response(request.Body)
                    {
                        Status = 200,
                        StatusText = "OK",
                        EncodeBody = ResponseBodyEncoding.Manual,
                        Cf = new { example = true }
                    };
                    var pair = new WebSocketPair();
                    pair.Server.Accept();
                    return new Response { Status = 101, WebSocket = pair.Client };
                }
            }
            """);

        Assert.Contains("new Response($workers$notNull(request).body, { status: 200, statusText: \"OK\", encodeBody: \"manual\", cf: { example: true } })", module);
        Assert.Contains("webSocket: $workers$notNull(pair)[0]", module);
    }

    [Fact]
    public void HeaderInitializersCopyInputsAndMutateTheConstructedResponse()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var headers = new Headers { ["x-example"] = "original" };
                    var response = new Response("Hello")
                    {
                        Status = 201,
                        Headers = new Headers(headers) { ["X-Example"] = "changed", ["x-second"] = "second" }
                    };
                    headers["x-later"] = "later";
                    response.Headers.Append("set-cookie", "first=1");
                    response.Headers.Append("set-cookie", "second=2");
                    var copy = new Headers(response.Headers);
                    copy.Delete("x-second");
                    var assigned = response.Headers["x-assigned"] = "assigned";
                    return Response.Json(new
                    {
                        response.Status,
                        text = await response.TextAsync(),
                        original = headers["x-example"],
                        changed = response.Headers["x-example"],
                        later = response.Headers["x-later"],
                        retained = response.Headers["x-second"],
                        removed = copy["x-second"],
                        cookies = response.Headers.GetSetCookie(), assigned
                    });
                }
            }
            """);

        Assert.Equal(201, result.GetProperty("status").GetInt32());
        Assert.Equal("Hello", result.GetProperty("text").GetString());
        Assert.Equal("original", result.GetProperty("original").GetString());
        Assert.Equal("changed", result.GetProperty("changed").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("later").ValueKind);
        Assert.Equal("second", result.GetProperty("retained").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("removed").ValueKind);
        Assert.Equal(["first=1", "second=2"], result.GetProperty("cookies").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal("assigned", result.GetProperty("assigned").GetString());
    }

    [Fact]
    public void ReconstructionMakesHeadersWritableAndSharesTheBodyWhileCloneTeesIt()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var fetched = await Http.FetchAsync("data:text/plain,fetched");
                    var rejected = false;
                    var cloneRejected = false;
                    try { fetched.Headers["x-example"] = "value"; }
                    catch (Exception) { rejected = true; }
                    try { fetched.Clone().Headers.Set("x-example", "value"); }
                    catch (Exception) { cloneRejected = true; }
                    var writable = new Response(fetched.Body, fetched) { Headers = { ["x-example"] = "value" } };
                    var source = new Response("shared");
                    var independent = source.Clone();
                    var shared = new Response(source.Body, source);
                    var text = await shared.TextAsync();
                    var sourceUsed = source.BodyUsed;
                    return Response.Json(new
                    {
                        rejected, cloneRejected, writable.Status,
                        header = writable.Headers["x-example"],
                        original = fetched.Headers["x-example"],
                        fetchedText = await writable.TextAsync(),
                        text, sourceUsed,
                        clonedText = await independent.TextAsync()
                    });
                }
            }
            """);

        Assert.True(result.GetProperty("rejected").GetBoolean());
        Assert.True(result.GetProperty("cloneRejected").GetBoolean());
        Assert.Equal(200, result.GetProperty("status").GetInt32());
        Assert.Equal("value", result.GetProperty("header").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("original").ValueKind);
        Assert.Equal("fetched", result.GetProperty("fetchedText").GetString());
        Assert.True(result.GetProperty("sourceUsed").GetBoolean());
        Assert.Equal("shared", result.GetProperty("text").GetString());
        Assert.Equal("shared", result.GetProperty("clonedText").GetString());
    }

    [Fact]
    public void ReconstructionKeepsSourceSettingsWithAsyncInitializers()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var calls = new int[1];
                    var first = Create(calls);
                    var plain = new Response(first.Body, first);
                    var second = Create(calls);
                    var initialized = new Response(second.Body, second) { Headers = { ["x-added"] = await ValueAsync() } };
                    return Response.Json(new
                    {
                        calls = calls[0],
                        plain.Status, plainText = await plain.TextAsync(),
                        initializedStatus = initialized.Status,
                        source = initialized.Headers["x-source"],
                        added = initialized.Headers["x-added"]
                    });
                }
                private static Response Create(int[] calls)
                {
                    calls[0] = calls[0] + 1;
                    return new Response("body") { Status = 202, Headers = { ["x-source"] = "kept" } };
                }
                private static async Task<string> ValueAsync() { await Task.CompletedTask; return "added"; }
            }
            """);

        Assert.Equal(2, result.GetProperty("calls").GetInt32());
        Assert.Equal(202, result.GetProperty("status").GetInt32());
        Assert.Equal("body", result.GetProperty("plainText").GetString());
        Assert.Equal(202, result.GetProperty("initializedStatus").GetInt32());
        Assert.Equal("kept", result.GetProperty("source").GetString());
        Assert.Equal("added", result.GetProperty("added").GetString());
    }

    [Fact]
    public void RedirectsAcceptRelativeLocationsAndHaveWritableHeaders()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var redirect = Response.Redirect("/login");
                    redirect.Cookies.Delete("session");
                    var permanent = Response.Redirect(status: 308, url: "https://example.test/");
                    var invalid = false;
                    try { Response.Redirect("/", 200); }
                    catch (Exception) { invalid = true; }
                    return Response.Json(new
                    {
                        redirect.Status,
                        location = redirect.Headers["location"],
                        cookies = redirect.Headers.GetSetCookie(),
                        permanentStatus = permanent.Status,
                        permanentLocation = permanent.Headers["location"],
                        invalid
                    });
                }
            }
            """);

        Assert.Equal(302, result.GetProperty("status").GetInt32());
        Assert.Equal("/login", result.GetProperty("location").GetString());
        Assert.Single(result.GetProperty("cookies").EnumerateArray());
        Assert.Equal(308, result.GetProperty("permanentStatus").GetInt32());
        Assert.Equal("https://example.test/", result.GetProperty("permanentLocation").GetString());
        Assert.True(result.GetProperty("invalid").GetBoolean());
    }

    [Fact]
    public void JsonDefaultsAndBodyInputsFollowNativeConstruction()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    BodyInit text = "hello";
                    BodyInit bytes = new byte[] { 65, 66 };
                    ReadableStream? stream = null;
                    var json = Response.Json(new { ok = true });
                    var custom = Response.Json("hello", status: 202, headers: new Headers { ["content-type"] = "application/custom+json" });
                    var empty = new Response(stream) { Status = 204 };
                    var invalid = false;
                    try { new Response("") { Status = 204 }; }
                    catch (Exception) { invalid = true; }
                    return Response.Json(new
                    {
                        jsonType = json.Headers["content-type"],
                        jsonText = await json.TextAsync(),
                        customType = custom.Headers["content-type"],
                        custom.Status,
                        customText = await custom.TextAsync(),
                        text = await new Response(text).TextAsync(),
                        bytes = await new Response(bytes).TextAsync(),
                        empty = empty.Body is null, invalid,
                        defaultStatus = new Response().Status
                    });
                }
            }
            """);

        Assert.Equal("application/json", result.GetProperty("jsonType").GetString());
        Assert.Equal("{\"ok\":true}", result.GetProperty("jsonText").GetString());
        Assert.Equal("application/custom+json", result.GetProperty("customType").GetString());
        Assert.Equal(202, result.GetProperty("status").GetInt32());
        Assert.Equal("\"hello\"", result.GetProperty("customText").GetString());
        Assert.Equal("hello", result.GetProperty("text").GetString());
        Assert.Equal("AB", result.GetProperty("bytes").GetString());
        Assert.True(result.GetProperty("empty").GetBoolean());
        Assert.True(result.GetProperty("invalid").GetBoolean());
        Assert.Equal(200, result.GetProperty("defaultStatus").GetInt32());
    }

    [Fact]
    public void RouterHeaderInitializerEvaluatesConstructorAndEntriesInOrder()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var events = new List<string>();
                    var router = new Router().Get("/", (req, route) => new Response(Record(events, "body"))
                    {
                        Headers = { [Record(events, "name")] = Record(events, "value"), ["async"] = "ready" },
                        Status = RecordStatus(events)
                    });
                    var response = await router.HandleAsync(request, env, context);
                    var asynchronous = new Response("async") { Headers = { ["x-value"] = await ValueAsync() } };
                    return Response.Json(new { events, response.Status, header = response.Headers["name"], text = await response.TextAsync(), asynchronous = asynchronous.Headers["x-value"] });
                }
                private static string Record(List<string> events, string value) { events.Add(value); return value; }
                private static int RecordStatus(List<string> events) { events.Add("status"); return 201; }
                private static async Task<string> ValueAsync() { await Task.CompletedTask; return "done"; }
            }
            """);

        Assert.Equal(["body", "name", "value", "status"], result.GetProperty("events").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(201, result.GetProperty("status").GetInt32());
        Assert.Equal("value", result.GetProperty("header").GetString());
        Assert.Equal("body", result.GetProperty("text").GetString());
        Assert.Equal("done", result.GetProperty("asynchronous").GetString());
    }

    [Fact]
    public void CopiesSourceSettingsAndEvaluatesNamedArgumentsBeforeAsyncInitializers()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var events = new List<string>();
                    var source = new Response("original")
                    {
                        Status = 202, StatusText = "Accepted",
                        Headers = { ["x-source"] = "original", ["x-retained"] = "retained" }
                    };
                    var response = new Response(init: Source(events, source), body: Record(events, "body"))
                    {
                        Headers = { [Record(events, "x-source")] = await ValueAsync(events) },
                        Status = 201
                    };
                    return Response.Json(new
                    {
                        events, response.Status, response.StatusText,
                        text = await response.TextAsync(),
                        changed = response.Headers["x-source"],
                        retained = response.Headers["x-retained"],
                        original = source.Headers["x-source"], originalStatus = source.Status
                    });
                }
                private static Response Source(List<string> events, Response source) { events.Add("source"); return source; }
                private static string Record(List<string> events, string value) { events.Add(value); return value; }
                private static async Task<string> ValueAsync(List<string> events) { events.Add("value"); await Task.CompletedTask; return "changed"; }
            }
            """);

        Assert.Equal(["source", "body", "x-source", "value"], result.GetProperty("events").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(201, result.GetProperty("status").GetInt32());
        Assert.Equal("Accepted", result.GetProperty("statusText").GetString());
        Assert.Equal("body", result.GetProperty("text").GetString());
        Assert.Equal("changed", result.GetProperty("changed").GetString());
        Assert.Equal("retained", result.GetProperty("retained").GetString());
        Assert.Equal("original", result.GetProperty("original").GetString());
        Assert.Equal(202, result.GetProperty("originalStatus").GetInt32());
    }

    [Fact]
    public void ResponseSettingsAreInitOnly()
    {
        var compilation = global::WorkerCompiler.CreateCompilation([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var response = new Response("Hello") { Status = 201, Headers = { ["x-example"] = "value" } };
                    response.Status = 202;
                    response.StatusText = "Accepted";
                    response.Headers = new Headers();
                    return response;
                }
            }
            """)]);

        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToArray();
        Assert.Equal(3, errors.Length);
        Assert.All(errors, error => Assert.Equal("CS8852", error.Id));
    }
}
