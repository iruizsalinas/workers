namespace Workers.Compiler.Tests;

public sealed class PlatformRuntimeTests
{
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
    public void StreamsSocketsWebSocketsAndDigestsMapToRuntimeObjectsWithoutHelpers()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    var random = Crypto.RandomBytes(8);
                    var stream = request.Body!;
                    var reader = stream.GetReader();
                    var chunk = await reader.ReadAsync();
                    reader.ReleaseLock();
                    var size = chunk.Done ? 0 : chunk.Value!.Length;
                    var all = await stream.BytesAsync();
                    var pair = WebSocketPair.Create();
                    pair.Server.Accept();
                    pair.Server.OnMessage(message => pair.Server.SendText(message.IsText ? message.AsText() : "binary"));
                    pair.Server.OnClose(close => Log.Info(close.Code));
                    var socket = TcpSocket.Connect(new TcpSocketAddress("example.com", 443), new TcpSocketOptions());
                    var writer = socket.Writable.GetWriter();
                    await writer.WriteAsync(TextCodec.EncodeUtf8("hello"));
                    var info = await socket.Opened;
                    var digest = Crypto.CreateDigestStream(DigestStreamAlgorithm.Sha256);
                    await stream.PipeToAsync(digest);
                    var hash = TextCodec.EncodeHex(await digest.DigestAsync());
                    return Response.FromBody(Body.Text(hash));
                }
            }
            """);

        Assert.Contains("import { connect as $workers$connectSocket } from \"cloudflare:sockets\";", module);
        Assert.Contains("globalThis.crypto.getRandomValues(new Uint8Array(8))", module);
        Assert.Contains("let reader = stream.getReader();", module);
        Assert.Contains("let chunk = await reader.read();", module);
        Assert.Contains("reader.releaseLock();", module);
        Assert.Contains("chunk.done ? 0 : chunk.value.length", module);
        Assert.Contains("await new Response(stream).bytes()", module);
        Assert.Contains("pair[1].addEventListener(\"message\", event => (message => ", module);
        Assert.Contains("(event.data)", module);
        Assert.Contains("pair[1].addEventListener(\"close\", close => console.info(close.code))", module);
        Assert.Contains("$workers$connectSocket({ hostname: \"example.com\", port: 443 }, {  })", module);
        Assert.Contains("let writer = socket.writable.getWriter();", module);
        Assert.Contains("await writer.write(new TextEncoder().encode(\"hello\"))", module);
        Assert.Contains("await socket.opened", module);
        Assert.Contains("new globalThis.crypto.DigestStream(\"SHA-256\")", module);
        Assert.Contains("await stream.pipeTo(digest)", module);
        Assert.Contains(".toHex()", module);
        Assert.Equal(["$workers$fetch"], System.Text.RegularExpressions.Regex.Matches(module, @"function (\$workers\$\w+)").Select(match => match.Groups[1].Value));
    }

    [Theory]
    [InlineData("Off", "off")]
    [InlineData("On", "on")]
    [InlineData("StartTls", "starttls")]
    public void UsesNativeTlsOptionValues(string option, string native)
    {
        var module = Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var socket = TcpSocket.Connect("example.com", 443,
                        new TcpSocketOptions { SecureTransport = TcpSecureTransport.{{option}} });
                    return Response.Empty();
                }
            }
            """);

        Assert.Contains($"secureTransport: \"{native}\"", module);
    }

    [Fact]
    public void LowersHtmlRewriterHandlerAndMutationCallbacks()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Handler : HtmlElementHandler
            {
                private const string Source = "csharp";
                private readonly string _prefix;
                public Handler(string prefix = "data") => _prefix = prefix;
                private string Attribute(string suffix = "worker") => $"{_prefix}-{suffix}";

                public override ValueTask ElementAsync(HtmlElement element)
                {
                    element.SetAttribute(Attribute(), Source);
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
        Assert.Contains("constructor(prefix = \"data\")", module);
        Assert.Contains("#attribute(suffix = \"worker\")", module);
        Assert.Contains("this.#attribute()", module);
        Assert.Contains("  element(element)", module);
        Assert.DoesNotContain("async element(element)", module);
        Assert.Contains("value.setAttribute(this.#attribute(), \"csharp\")", module);
        Assert.DoesNotContain("this.Source", module);
        Assert.Contains("new HTMLRewriter()).transform", module);
    }

    [Fact]
    public void EmitsHtmlHandlerIteratorMethodsAsGenerators()
    {
        var module = Compile("""
            using Workers;
            public sealed class Handler : HtmlElementHandler
            {
                private IEnumerable<int> Values()
                {
                    yield return 1;
                }

                public override ValueTask ElementAsync(HtmlElement element) => ValueTask.CompletedTask;
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    new HtmlRewriter().On("p", new Handler()).Transform(Response.Html("<p>x</p>"));
            }
            """);

        Assert.Contains("*#values()", module);
    }

}
