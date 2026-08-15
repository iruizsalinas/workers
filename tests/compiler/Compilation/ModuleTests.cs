namespace Workers.Compiler.Tests;

public sealed class ModuleTests
{
    [Fact]
    public void EmitsDirectFetchModuleWithoutRuntime()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, Context ctx) =>
                    Task.FromResult(Response.Text("Hello"));
            }
            """);

        Assert.Contains("return new Response(\"Hello\");", module);
        Assert.Contains("export default { fetch: $workers$fetch };", module);
        Assert.DoesNotContain("dotnet", module, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wasm", module, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsUnsupportedStatements()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    while (true) { }
                }
            }
            """));

        Assert.StartsWith("WRK100:", error.Message);
    }

    [Fact]
    public void EmitsUserMethodsThatShareAnIntrinsicNameWithoutRewritingThemAsBindings()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    var value = await UserApi.GetAsync("key");
                    return Response.Text(value);
                }
            }

            public static class UserApi
            {
                public static Task<string> GetAsync(string key) => Task.FromResult(key);
            }
            """);

        Assert.Contains("function $workers$cs$UserApi$GetAsync$0", module);
        Assert.Contains("await $workers$cs$UserApi$GetAsync$0(\"key\")", module);
        Assert.DoesNotContain(".get(\"key\")", module);
    }

}
