namespace Workers.Compiler.Tests;

public sealed class WebPlatformProfileTests
{
    [Fact]
    public void EmitsMutableUrlsHmacAndCompression()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var url = new Url(request.Url.ToString());
                    url.QueryParameters.Set("lang", "en");
                    url.QueryParameters.Sort();
                    byte[] signature = [1];
                    byte[] payload = [2];
                    var valid = await Crypto.VerifyHmacSha256Async("secret", signature, payload);
                    var stream = request.BodyStream()!.Compress(CompressionFormat.Gzip);
                    return Response.FromStream(stream).WithHeader("x-valid", valid.ToString());
                }
            }
            """);

        Assert.Contains("new URL(new URL(request.url).toString())", module);
        Assert.Contains("searchParams.set(\"lang\", \"en\")", module);
        Assert.Contains("subtle.importKey", module);
        Assert.Contains("pipeThrough(new CompressionStream(\"gzip\"))", module);
    }

    [Fact]
    public void EmitsMultipartFileSlices()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    foreach (var entry in await request.FormDataAsync())
                    {
                        var file = entry.Value.File;
                        if (file is not null) return Response.Json(await file.SliceBytesAsync(0, 8));
                    }
                    return Response.Empty(204);
                }
            }
            """);

        Assert.Contains("instanceof File ?", module);
        Assert.Contains(")(entry[1])", module);
        Assert.Contains("file.slice(0, 8).arrayBuffer()", module);
    }

    [Fact]
    public void EvaluatesFormEntryPropertyReceiversOnce()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    foreach (var entry in await request.FormDataAsync())
                    {
                        var file = Pass(entry.Value).File;
                        var text = Pass(entry.Value).Text;
                        return Response.Json(new { file = file is not null, text });
                    }
                    return Response.Empty(204);
                }

                private static FormEntry Pass(FormEntry entry) => entry;
            }
            """);

        Assert.Contains("=> $workers$formEntry instanceof File ? $workers$formEntry : null)", module);
        Assert.Contains("=> typeof $workers$formEntry$2 === \"string\" ? $workers$formEntry$2 : null)", module);
        Assert.Equal(3, module.Split("$workers$cs$Worker$Pass$0(", StringSplitOptions.None).Length - 1);
    }
}
