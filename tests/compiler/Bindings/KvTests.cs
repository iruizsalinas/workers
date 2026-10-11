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
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var text = await env.KV("DATA").GetAsync("key", new KVNamespaceGetOptions { CacheTtl = 60 });
                    await env.KV("DATA").PutJsonAsync("result", new { text }, new KVNamespacePutOptions { ExpirationTtl = 300 });
                    return new Response(text ?? "missing");
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
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var json = await env.KV("DATA").GetJsonAsync<object>("json");
                    var bytes = await env.KV("DATA").GetBytesAsync("bytes");
                    return Response.Json(new { json, bytes });
                }
            }
            """);

        Assert.Contains("env[\"DATA\"].get(\"json\", { type: \"json\" })", module);
        Assert.Contains("env[\"DATA\"].get(\"bytes\", { type: \"arrayBuffer\" })", module);
    }

    [Fact]
    public void ResolvesVerbatimNamedBindingArgumentsByTheirCSharpNames()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    await env.KV("DATA").PutAsync(value: "content", @key: "entry");
                    return new Response();
                }
            }
            """);

        Assert.Contains(".put($workers$arg2, $workers$arg1)", module);
        Assert.Contains(", \"content\", \"entry\")", module);
        Assert.DoesNotContain("@key", module);
    }

}
