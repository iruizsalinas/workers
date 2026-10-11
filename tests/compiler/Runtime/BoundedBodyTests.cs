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
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var limit = int.Parse(request.QueryParameters.Get("limit") ?? "4096");
                    if (request.Path == "/text") return Response.Text(await request.TextAsync(maxBytes: limit));
                    if (request.Path == "/bytes") return Response.FromBody(Body.FromBytes(await request.BytesAsync(limit)));
                    return Response.Json(await request.JsonAsync<Input>(maxBytes: limit));
                }
            }
            """);

        Assert.Contains("$workers$requestText(request, limit)", module);
        Assert.Contains("$workers$requestBytes(request, limit)", module);
        Assert.Contains("$workers$requestJson(request, limit).then(", module);
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
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    if (request.Path == "/text") return Response.Text(await request.TextAsync());
                    if (request.Path == "/bytes") return Response.FromBody(Body.FromBytes(await request.BytesAsync()));
                    return Response.Json(await request.JsonAsync<object>());
                }
            }
            """);

        Assert.Contains("request.text()", module);
        Assert.Contains("request.bytes()", module);
        Assert.Contains("$workers$requestJson(request)", module);
        Assert.DoesNotContain("request.body.getReader()", module);
    }
}
