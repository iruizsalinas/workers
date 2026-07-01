using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed class HtmlRewriterTests
{
    [Fact]
    public async Task TransformDispatchesNativeHtmlRewriterOperation()
    {
        var dispatcher = new CapturingDispatcher("""{"status":200,"headers":[{"name":"content-type","value":"text/html"}],"bodyBase64":null,"nativeResponseHandle":"response:rewritten"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = new Env(bindings: null, invocationId: "invocation-html", bindingDispatcher: dispatcher);
        var response = Response.Html("<p>hello</p>");

        var rewritten = await environment.HtmlRewriter()
            .On("p", new ReplaceElementHandler())
            .TransformAsync(response);

        Assert.Equal("response:rewritten", rewritten.NativeResponseHandle);
        Assert.Equal("htmlRewriter.transform", dispatcher.Invocations.Single().Operation);
        Assert.Equal("$htmlRewriter", dispatcher.Invocations.Single().BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("html:", payload.RootElement.GetProperty("registryId").GetString()![..5]);
        Assert.Equal("p", payload.RootElement.GetProperty("selectors")[0].GetProperty("selector").GetString());
        Assert.Equal(200, payload.RootElement.GetProperty("response").GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ManagedCallbackRecordsElementMutations()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("a[href]", new LinkElementHandler())],
            documentHandler: null);

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                registryId = registration.Id,
                handlerId = registration.Selectors[0].HandlerId,
                kind = "element",
                snapshot = new
                {
                    tagName = "a",
                    namespaceUri = (string?)null,
                    removed = false,
                    attributes = new[] { new { name = "href", value = "/old" } }
                }
            });

            var result = await HtmlRewriterRegistry.InvokeCallbackAsync(payload);

            using var actions = JsonDocument.Parse(result);
            Assert.Equal("setAttribute", actions.RootElement[0].GetProperty("type").GetString());
            Assert.Equal("href", actions.RootElement[0].GetProperty("name").GetString());
            Assert.Equal("/new", actions.RootElement[0].GetProperty("value").GetString());
            Assert.Equal("append", actions.RootElement[1].GetProperty("type").GetString());
            Assert.Equal("!", actions.RootElement[1].GetProperty("content").GetString());
            Assert.False(actions.RootElement[1].GetProperty("html").GetBoolean());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackKeepsRawHtmlExplicit()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("#target", new RawHtmlElementHandler())],
            documentHandler: null);

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                registryId = registration.Id,
                handlerId = registration.Selectors[0].HandlerId,
                kind = "element",
                snapshot = new
                {
                    tagName = "div",
                    namespaceUri = (string?)null,
                    removed = false,
                    attributes = Array.Empty<object>()
                }
            });

            var result = await HtmlRewriterRegistry.InvokeCallbackAsync(payload);

            using var actions = JsonDocument.Parse(result);
            Assert.False(actions.RootElement[0].GetProperty("html").GetBoolean());
            Assert.True(actions.RootElement[1].GetProperty("html").GetBoolean());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackSupportsDocumentTextCommentsAndEnd()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [],
            new DocumentHandler());

        try
        {
            var text = await InvokeAsync(registration, "text", new { text = "hello", lastInTextNode = true, removed = false });
            var comment = await InvokeAsync(registration, "comments", new { text = "remove", removed = false });
            var end = await InvokeAsync(registration, "end", new { });

            using var textActions = JsonDocument.Parse(text);
            using var commentActions = JsonDocument.Parse(comment);
            using var endActions = JsonDocument.Parse(end);

            Assert.Equal("replace", textActions.RootElement[0].GetProperty("type").GetString());
            Assert.Equal("HELLO", textActions.RootElement[0].GetProperty("content").GetString());
            Assert.Equal("remove", commentActions.RootElement[0].GetProperty("type").GetString());
            Assert.Equal("append", endActions.RootElement[0].GetProperty("type").GetString());
            Assert.True(endActions.RootElement[0].GetProperty("html").GetBoolean());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackSupportsEndTagHandlers()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("span", new EndTagElementHandler())],
            documentHandler: null);

        try
        {
            var elementPayload = JsonSerializer.Serialize(new
            {
                registryId = registration.Id,
                handlerId = registration.Selectors[0].HandlerId,
                kind = "element",
                snapshot = new
                {
                    tagName = "span",
                    namespaceUri = (string?)null,
                    removed = false,
                    attributes = Array.Empty<object>()
                }
            });

            var elementResult = await HtmlRewriterRegistry.InvokeCallbackAsync(elementPayload);
            using var elementActions = JsonDocument.Parse(elementResult);
            var endTagHandlerId = elementActions.RootElement[0].GetProperty("handlerId").GetString()!;

            var endTagPayload = JsonSerializer.Serialize(new
            {
                registryId = registration.Id,
                handlerId = endTagHandlerId,
                kind = "endTag",
                snapshot = new { name = "span" }
            });

            var endTagResult = await HtmlRewriterRegistry.InvokeCallbackAsync(endTagPayload);
            using var endTagActions = JsonDocument.Parse(endTagResult);

            Assert.Equal("before", endTagActions.RootElement[0].GetProperty("type").GetString());
            Assert.Equal("done", endTagActions.RootElement[0].GetProperty("content").GetString());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackSupportsResponseContent()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("main", new ResponseContentElementHandler())],
            documentHandler: null);

        try
        {
            var result = await InvokeElementAsync(registration, "main");

            using var actions = JsonDocument.Parse(result);
            var action = actions.RootElement[0];
            Assert.Equal("append", action.GetProperty("type").GetString());
            Assert.False(action.TryGetProperty("content", out _));
            Assert.False(action.TryGetProperty("hasContentOptions", out _));
            Assert.Equal(200, action.GetProperty("response").GetProperty("status").GetInt32());
            Assert.Equal("text/html; charset=utf-8", action.GetProperty("response").GetProperty("headers")[0].GetProperty("value").GetString());

            var body = Convert.FromBase64String(action.GetProperty("response").GetProperty("bodyBase64").GetString()!);
            Assert.Equal("<strong>ok</strong>", System.Text.Encoding.UTF8.GetString(body));
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackSupportsStreamContent()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("main", new StreamContentElementHandler())],
            documentHandler: null);

        try
        {
            var result = await InvokeElementAsync(registration, "main");

            using var actions = JsonDocument.Parse(result);
            var action = actions.RootElement[0];
            Assert.Equal("append", action.GetProperty("type").GetString());
            Assert.False(action.TryGetProperty("content", out _));
            Assert.False(action.TryGetProperty("hasContentOptions", out _));
            Assert.Equal("request", action.GetProperty("streamSource").GetString());
            Assert.Equal("request:1", action.GetProperty("streamHandle").GetString());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackSupportsContentOptionsForEveryContentTypeAndChaining()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("main", new ChainedContentOptionsHandler())],
            documentHandler: null);

        try
        {
            var result = await InvokeElementAsync(registration, "main");

            using var actions = JsonDocument.Parse(result);

            Assert.Equal("setAttribute", actions.RootElement[0].GetProperty("type").GetString());
            Assert.Equal("append", actions.RootElement[1].GetProperty("type").GetString());
            Assert.True(actions.RootElement[1].GetProperty("html").GetBoolean());
            Assert.True(actions.RootElement[1].GetProperty("hasContentOptions").GetBoolean());

            Assert.Equal("prepend", actions.RootElement[2].GetProperty("type").GetString());
            Assert.True(actions.RootElement[2].GetProperty("html").GetBoolean());
            Assert.True(actions.RootElement[2].GetProperty("hasContentOptions").GetBoolean());
            Assert.True(actions.RootElement[2].TryGetProperty("response", out _));

            Assert.Equal("before", actions.RootElement[3].GetProperty("type").GetString());
            Assert.False(actions.RootElement[3].GetProperty("html").GetBoolean());
            Assert.True(actions.RootElement[3].GetProperty("hasContentOptions").GetBoolean());
            Assert.Equal("request", actions.RootElement[3].GetProperty("streamSource").GetString());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }


    [Fact]
    public async Task ManagedCallbackRejectsWebSocketResponseContent()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("main", new WebSocketResponseContentElementHandler())],
            documentHandler: null);

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => InvokeElementAsync(registration, "main"));
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    [Fact]
    public async Task ManagedCallbackAwaitsAsyncEndTagHandlers()
    {
        var registration = HtmlRewriterRegistry.Register(
            "invocation-html",
            new CapturingDispatcher("{}"),
            [new HtmlSelectorHandler("span", new AsyncEndTagElementHandler())],
            documentHandler: null);

        try
        {
            var elementResult = await InvokeElementAsync(registration, "span");
            using var elementActions = JsonDocument.Parse(elementResult);
            var endTagHandlerId = elementActions.RootElement[0].GetProperty("handlerId").GetString()!;

            var endTagPayload = JsonSerializer.Serialize(new
            {
                registryId = registration.Id,
                handlerId = endTagHandlerId,
                kind = "endTag",
                snapshot = new { name = "span" }
            });

            var endTagResult = await HtmlRewriterRegistry.InvokeCallbackAsync(endTagPayload);
            using var endTagActions = JsonDocument.Parse(endTagResult);

            Assert.Equal("after", endTagActions.RootElement[0].GetProperty("type").GetString());
            Assert.Equal("async", endTagActions.RootElement[0].GetProperty("content").GetString());
        }
        finally
        {
            HtmlRewriterRegistry.Release(registration.Id);
        }
    }

    private static Task<string> InvokeAsync(HtmlRewriterRegistration registration, string kind, object snapshot)
    {
        var payload = JsonSerializer.Serialize(new
        {
            registryId = registration.Id,
            handlerId = registration.DocumentHandlerId,
            kind,
            snapshot
        });
        return HtmlRewriterRegistry.InvokeCallbackAsync(payload);
    }

    private static Task<string> InvokeElementAsync(HtmlRewriterRegistration registration, string tagName)
    {
        var payload = JsonSerializer.Serialize(new
        {
            registryId = registration.Id,
            handlerId = registration.Selectors[0].HandlerId,
            kind = "element",
            snapshot = new
            {
                tagName,
                namespaceUri = (string?)null,
                removed = false,
                attributes = Array.Empty<object>()
            }
        });
        return HtmlRewriterRegistry.InvokeCallbackAsync(payload);
    }

    private sealed class ReplaceElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.Replace("ok");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LinkElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            if (element.GetAttribute("href") == "/old")
                element.SetAttribute("href", "/new");

            element.Append("!");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RawHtmlElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.SetInnerContent("<b>escaped</b>");
            element.Append("<b>raw</b>", HtmlContentOptions.Html);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DocumentHandler : HtmlDocumentHandler
    {
        public override ValueTask TextAsync(HtmlTextChunk text)
        {
            if (text.Text == "hello")
                text.Replace("HELLO");

            return ValueTask.CompletedTask;
        }

        public override ValueTask CommentsAsync(HtmlComment comment)
        {
            comment.Remove();
            return ValueTask.CompletedTask;
        }

        public override ValueTask EndAsync(HtmlDocumentEnd end)
        {
            end.Append("<footer>done</footer>", HtmlContentOptions.Html);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class EndTagElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.OnEndTag(static tag => tag.Before("done"));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ResponseContentElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.Append(Response.Html("<strong>ok</strong>"));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class WebSocketResponseContentElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.Append(Response.FromWebSocket(new WebSocket("invocation-html", "ws:test", new CapturingDispatcher("{}"))));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StreamContentElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.Append(new ReadableStream("invocation-html", NativeStreamSource.Request, "request:1", new CapturingDispatcher("{}")));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ChainedContentOptionsHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            var returned = element
                .SetAttribute("data-chain", "yes")
                .Append("<strong>raw</strong>", HtmlContentOptions.Html)
                .Prepend(Response.Html("<em>response</em>"), HtmlContentOptions.Html)
                .Before(new ReadableStream("invocation-html", NativeStreamSource.Request, "request:chain", new CapturingDispatcher("{}")), HtmlContentOptions.Text);

            Assert.Same(element, returned);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AsyncEndTagElementHandler : HtmlElementHandler
    {
        public override ValueTask ElementAsync(HtmlElement element)
        {
            element.OnEndTag(static async tag =>
            {
                await Task.Yield();
                tag.After("async");
            });
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingDispatcher(params string[] responses) : IBindingDispatcher
    {
        private readonly Queue<string> _responses = new(responses);

        public List<BindingInvocation> Invocations { get; } = [];

        public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
        {
            Invocations.Add(invocation);
            return Task.FromResult(_responses.Count == 0 ? "{}" : _responses.Dequeue());
        }
    }
}
