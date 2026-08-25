namespace Workers.Compiler.Tests;

public sealed class RuntimeApiTests
{
    [Fact]
    public void ExposesNativeRequestAndUrlMetadata()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var url = request.Url;
                    return Response.Json(new
                    {
                        url.Origin,
                        url.Protocol,
                        url.Host,
                        url.Hostname,
                        url.Port,
                        url.Username,
                        url.Password,
                        url.Fragment,
                        request.Redirect,
                        hasSignal = request.Signal is not null
                    });
                }
            }
            """);

        Assert.Contains("let url = new URL(request.url)", module);
        Assert.Contains("origin: url.origin", module);
        Assert.Contains("protocol: url.protocol", module);
        Assert.Contains("hostname: url.hostname", module);
        Assert.Contains("fragment: url.hash", module);
        Assert.Contains("redirect: request.redirect", module);
        Assert.Contains("hasSignal: request.signal != null", module);
    }

    [Fact]
    public void PreservesNamedResponseArgumentMeaning()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Text(status: 404, body: "missing");
            }
            """);

        Assert.Contains("new Response($workers$arg2, { status: $workers$arg1 })", module);
        Assert.Contains(")(404, \"missing\")", module);
    }

    [Fact]
    public void PreservesResponseInitializationOptions()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.FromBody(Body.Text("missing"), 404, "Not Found");
            }
            """);

        Assert.Contains("new Response($workers$body.body ?? $workers$body, { status: 404, statusText: \"Not Found\" ?? undefined })", module);
        Assert.Contains(")(\"missing\")", module);
    }

    [Fact]
    public void TreatsTailEventsAsTheNativeEventArray()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Tail]
                public static void Tail(TailEvent tail, Env env, Context context) =>
                    Console.WriteLine(tail.Events.Count.ToString());
            }
            """);

        Assert.Contains("console.log(String(tail.length))", module);
        Assert.DoesNotContain("tail.events", module);
    }

    [Fact]
    public void AdaptsModuleEventValuesToTheFacadeTypes()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Tail]
                public static void Tail(TailEvent tail) =>
                    Console.WriteLine(tail[0].Event!.Request!.Headers.Get("content-type"));

                [Scheduled]
                public static void Scheduled(ScheduledEvent scheduled) =>
                    Console.WriteLine($"{scheduled.Type}:{scheduled.Schedule}");
            }
            """);

        Assert.Contains("new Headers(tail[0].event.request.headers).get(\"content-type\")", module);
        Assert.Contains("scheduled.scheduledTime", module);
        Assert.DoesNotContain("scheduled.type", module);
    }

    [Fact]
    public void PreservesJsonResponseOptionsAndStatusText()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { ok = true }, 201, new { headers = new { location = "/items/1" } }, "Created");
            }
            """);

        Assert.Contains("...({ headers: { location: \"/items/1\" } } ?? {})", module);
        Assert.Contains("status: 201, statusText: \"Created\" ?? undefined", module);
    }

    [Fact]
    public void PreservesNamedBindingArgumentMeaning()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var socket = WebSocketPair.Create().Server;
                    socket.Close(reason: "done", code: 1000);
                    return Response.Empty();
                }
            }
            """);

        Assert.Contains(".close($workers$arg2, $workers$arg1)", module);
        Assert.Contains(", \"done\", 1000)", module);
    }

    [Fact]
    public void RepresentsCompletedTasksAsPromises()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Scheduled]
                public static Task Run(ScheduledEvent scheduled, Env env, Context context) => Task.CompletedTask;
            }
            """);

        Assert.Contains("return Promise.resolve();", module);
        Assert.DoesNotContain("Task.completedTask", module);
    }

    [Fact]
    public void LowersGlobalHttpAndTcpConnections()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Handle(Request request, Env env, Context context)
                {
                    var response = await Http.FetchAsync("https://example.com");
                    var socket = TcpSocket.Connect("example.com", 443);
                    return response;
                }
            }
            """);

        Assert.Contains("await fetch(\"https://example.com\")", module);
        Assert.Contains("$workers$connectSocket({ hostname: \"example.com\", port: 443 })", module);
    }

    [Fact]
    public void LowersNativeRuntimeIntrinsicsAndIncludesOnlyRequiredHelpers()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    var controller = new AbortController();
                    controller.Abort("done");
                    var random = Crypto.RandomBytes(8);
                    var digest = await Crypto.DigestTextAsync(DigestAlgorithm.Sha256, "value");
                    var stream = request.BodyStream();
                    var read = await stream.ReadAsync();
                    var all = await stream.ReadAllBytesAsync();
                    var pair = WebSocketPair.Create();
                    pair.Server.Accept();
                    var events = pair.Server.Events();
                    var socket = TcpSocket.Connect(new TcpSocketAddress("example.com", 443), new TcpSocketOptions());
                    await socket.WriteTextAsync("hello");
                    return Response.FromBody(Body.Text("ok"));
                }
            }
            """);

        Assert.Contains("import { connect as $workers$connectSocket } from \"cloudflare:sockets\";", module);
        Assert.Contains("new AbortController()", module);
        Assert.Contains("globalThis.crypto.getRandomValues(new Uint8Array(8))", module);
        Assert.Contains("globalThis.crypto.subtle.digest(\"SHA-256\", new TextEncoder().encode(\"value\"))", module);
        Assert.Contains("let stream = request.body", module);
        Assert.Contains("$workers$streamRead(stream)", module);
        Assert.Contains("$workers$streamAll(stream)", module);
        Assert.Contains("pair[1].accept()", module);
        Assert.Contains("$workers$webSocketEvents(pair[1])", module);
        Assert.Contains("$workers$connectSocket({ hostname: \"example.com\", port: 443 }, {  })", module);
        Assert.Contains("$workers$socketWriter(socket).write(new TextEncoder().encode(\"hello\"))", module);
    }

    [Fact]
    public void LowersHtmlRewriterHandlerAndMutationCallbacks()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Handler : HtmlElementHandler
            {
                public override ValueTask ElementAsync(HtmlElement element)
                {
                    element.SetAttribute("data-worker", "csharp");
                    return ValueTask.CompletedTask;
                }
            }
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    return new HtmlRewriter().On("p", new Handler()).Transform(Response.Html("<p>x</p>"));
                }
            }
            """);

        Assert.Contains("class Handler", module);
        Assert.Contains("  element(element)", module);
        Assert.DoesNotContain("async element(element)", module);
        Assert.Contains("value.setAttribute(\"data-worker\", \"csharp\")", module);
        Assert.Contains("new HTMLRewriter()).transform", module);
    }

}
