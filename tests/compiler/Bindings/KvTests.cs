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

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver).get($workers$arg1, $workers$arg2 ?? undefined))($workers$notNull(env)[\"DATA\"], \"key\", { cacheTtl: 60 })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2, $workers$arg2$2, $workers$arg3) => $workers$notNull($workers$receiver$2).put($workers$arg1$2, JSON.stringify($workers$arg2$2), $workers$arg3))($workers$notNull(env)[\"DATA\"], \"result\", { text: text }, { expirationTtl: 300 })", module);
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

        Assert.Contains("$workers$notNull($workers$notNull(env)[\"DATA\"]).get(\"json\", { type: \"json\" })", module);
        Assert.Contains("$workers$notNull($workers$notNull(env)[\"DATA\"]).get(\"bytes\", { type: \"arrayBuffer\" })", module);
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
