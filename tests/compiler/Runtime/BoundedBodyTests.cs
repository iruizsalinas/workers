namespace Workers.Compiler.Tests;

public sealed class BoundedBodyTests
{
    [Fact]
    public void EmitsBoundedReadsAndPreservesTypedJsonHydration()
    {
        var module = Compile("""
            using Workers;
            public sealed record Input(string Text);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var limit = int.Parse(new Url(request.Url).SearchParams.Get("limit") ?? "4096");
                    if (new Url(request.Url).Pathname == "/text") return new Response(await request.TextAsync(maxBytes: limit));
                    if (new Url(request.Url).Pathname == "/bytes") return new Response(await request.BytesAsync(limit));
                    return Response.Json(await request.JsonAsync<Input>(maxBytes: limit));
                }
            }
            """);

        Assert.Contains("$workers$requestText($workers$notNull(request), limit)", module);
        Assert.Contains("$workers$requestBytes($workers$notNull(request), limit)", module);
        Assert.Contains("$workers$requestJson($workers$notNull(request), limit).then(value => value == null ? value : $workers$Input.$fromJSON(value))", module);
        Assert.Contains("$fromJSON", module);
        Assert.Contains("request.body.getReader()", module);
        Assert.Contains("new $workers$HttpError(413, \"Request body too large\")", module);
    }

    [Fact]
    public void KeepsUnboundedTextAndByteReadsNative()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    if (new Url(request.Url).Pathname == "/text") return new Response(await request.TextAsync());
                    if (new Url(request.Url).Pathname == "/bytes") return new Response(await request.BytesAsync());
                    return Response.Json(await request.JsonAsync<object>());
                }
            }
            """);

        Assert.Contains("request.text()", module);
        Assert.Contains("$workers$notNull(request).bytes()", module);
        Assert.Contains("$workers$requestJson($workers$notNull(request))", module);
        Assert.DoesNotContain("request.body.getReader()", module);
    }
}
