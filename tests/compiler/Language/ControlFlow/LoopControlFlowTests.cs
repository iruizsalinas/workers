namespace Workers.Compiler.Tests;

public sealed class LoopControlFlowTests
{
    [Fact]
    public void SupportsForDeclarationsWithoutInitializers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    for (int index; request.Method.Length > 0;)
                    {
                        index = 1;
                        break;
                    }
                    return new Response("ok");
                }
            }
            """);

        Assert.Contains("for (let index; request.method.length > 0; )", module);
    }

    [Fact]
    public void EmitsLoopsContinueAndTypedByteSpreads()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    byte[] left = [1, 2];
                    byte[] right = [3];
                    byte[] all = [.. left, .. right];
                    var total = 0;
                    for (var index = 0; index < all.Length; index++)
                    {
                        if (index == 1) continue;
                        total += all[index];
                    }
                    return Response.Json(new { total });
                }
            }
            """);

        Assert.Contains("Uint8Array.from([...left, ...right])", module);
        Assert.Contains("for (let index = 0; index < all.length; (($workers$value) =>", module);
        Assert.Contains("continue;", module);
    }

    [Fact]
    public void ParenthesizesLogicalNegation()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var body = request.Method;
                    return Response.Json(new { present = !(body is null) });
                }
            }
            """);

        Assert.True(result.GetProperty("present").GetBoolean());
    }
}
