namespace Workers.Compiler.Tests;

public sealed class RuntimeApiTests
{
    [Fact]
    public void ErasesCompletedTasks()
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

        Assert.Contains("return undefined;", module);
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
        Assert.Contains("async element(element)", module);
        Assert.Contains("value.setAttribute(\"data-worker\", \"csharp\")", module);
        Assert.Contains("new HTMLRewriter()).transform", module);
    }

}
