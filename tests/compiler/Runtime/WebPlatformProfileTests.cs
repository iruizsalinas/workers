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

        Assert.Contains("new URL(request.url.toString())", module);
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

        Assert.Contains("entry[1] instanceof File", module);
        Assert.Contains("file.slice(0, 8).arrayBuffer()", module);
    }
}
