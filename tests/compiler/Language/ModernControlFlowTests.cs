namespace Workers.Compiler.Tests;

public sealed class ModernControlFlowTests
{
    [Fact]
    public void UsesObjectSemanticsForStringDictionaries()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 };
                    var total = 0;
                    foreach (var entry in values) total += entry.Value;
                    return Response.Json(new { count = values.Count, total, first = values["one"], values });
                }
            }
            """);

        Assert.Contains("let values = { [\"one\"]: 1, [\"two\"]: 2 };", module);
        Assert.Contains("for (const entry of Object.entries(values))", module);
        Assert.Contains("count: Object.keys(values).length", module);
        Assert.Contains("first: values[\"one\"]", module);
    }

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
                    return Response.Text("ok");
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
        Assert.Contains("for (let index = 0; index < all.length; index++)", module);
        Assert.Contains("continue;", module);
    }

    [Fact]
    public void ParenthesizesLogicalNegation()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { present = !request.Body.IsEmpty });
            }
            """);

        Assert.Contains("!(request.body === null)", module);
    }
}
