namespace Workers.Compiler.Tests;

public sealed class KvTests
{
    [Fact]
    public void LowersKvOperationsAndStructuralOptions()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    var text = await env.Kv("DATA").GetTextAsync("key", new KvGetOptions { CacheTtl = 60 });
                    await env.Kv("DATA").PutJsonAsync("result", new { text }, new KvPutOptions { ExpirationTtl = 300 });
                    return Response.Text(text ?? "missing");
                }
            }
            """);

        Assert.Contains("env[\"DATA\"].get(\"key\", { cacheTtl: 60 })", module);
        Assert.Contains("env[\"DATA\"].put(\"result\", JSON.stringify({ text: text }), { expirationTtl: 300 })", module);
    }

    [Fact]
    public void LowersTypedKvReadsWithNativeTypeOptions()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    var json = await env.Kv("DATA").GetJsonAsync<object>("json");
                    var bytes = await env.Kv("DATA").GetBytesAsync("bytes");
                    return Response.Json(new { json, bytes });
                }
            }
            """);

        Assert.Contains("env[\"DATA\"].get(\"json\", { type: \"json\" })", module);
        Assert.Contains("env[\"DATA\"].get(\"bytes\", { type: \"arrayBuffer\" })", module);
    }

}
