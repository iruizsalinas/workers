namespace Workers.Compiler.Tests;

public sealed class WorkerEntrypointTests
{
    [Fact]
    public void AllowsConstantsWithoutCreatingEntrypointState()
    {
        var module = Compile("""
            using Workers;
            public sealed class Constants : WorkerEntrypoint
            {
                private const string Value = "ok";
                public string Read() => Value;
            }
            """);

        Assert.Contains("return \"ok\";", module);
        Assert.DoesNotContain("this.Value", module);
    }

    [Fact]
    public void RemovesAsyncOnlyAsATerminalConvention()
    {
        var module = Compile("""
            using Workers;
            public sealed class Names : WorkerEntrypoint
            {
                public Response Asyncify(string value) => new Response(value);
                public Task<Response> ResolveAsync(string value) => Task.FromResult(new Response(value));
            }
            """);

        Assert.Contains("asyncify(value)", module);
        Assert.Contains("resolve(value)", module);
        Assert.DoesNotContain("  ify(value)", module);
    }

    [Fact]
    public void EmitsNamedEntrypointsWithContextAndDefaults()
    {
        var module = Compile("""
            using Workers;
            public sealed class Users : WorkerEntrypoint
            {
                public async Task<string> FindAsync(string prefix, int limit = 10)
                {
                    Ctx.WaitUntil(Task.CompletedTask);
                    return await Env.KV("DATA").GetAsync(prefix) ?? limit.ToString();
                }
            }
            """);

        Assert.Contains("WorkerEntrypoint", module);
        Assert.Contains("export class Users extends", module);
        Assert.Contains("async find(prefix, limit = 10)", module);
        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).waitUntil($workers$arg1))(this.ctx, Promise.resolve())", module);
        Assert.Contains("$workers$notNull($workers$notNull(this.env)[\"DATA\"])", module);
    }

    [Fact]
    public void EmitsNonPublicHelpersWithoutExposingThemOverRpc()
    {
        var module = Compile("""
            using Workers;
            public sealed class Users : WorkerEntrypoint
            {
                public string Find(string value) => Normalize(value);
                private string Normalize(string value) => value.Trim();
            }
            """);

        Assert.Contains("#normalize(value)", module);
        Assert.Contains("return this.#normalize(value);", module);
    }

    [Fact]
    public void EmitsIteratorMethodsAsGenerators()
    {
        var module = Compile("""
            using Workers;
            public sealed class Users : WorkerEntrypoint
            {
                private async IAsyncEnumerable<int> ValuesAsync()
                {
                    await Task.CompletedTask;
                    yield return 1;
                }
            }
            """);

        Assert.Contains("#values()", module);
        Assert.Contains("return { [Symbol.asyncIterator]: (async function* ()", module);
    }

    [Fact]
    public void InitializesEntrypointInstanceState()
    {
        var module = Compile("""
            using Workers;
            public sealed class Counter : WorkerEntrypoint
            {
                private int _count;
                public int Value() => _count;
            }
            """);

        Assert.Contains("constructor(ctx, env) {", module);
        Assert.Contains("super(ctx, env);", module);
        Assert.Contains("this._count = 0;", module);
    }

    [Fact]
    public void PreservesNamedInstanceArgumentOrderAndSourceEvaluationOrder()
    {
        var module = Compile("""
            using Workers;
            public sealed class Counter : WorkerEntrypoint
            {
                private int _next;
                private int Next() => _next++;
                private string Pair(int a, int b) => $"{a}:{b}";
                public string Run() => Pair(b: Next(), a: Next());
            }
            """);

        Assert.Contains("(($workers$arg1, $workers$arg2) => this.#pair($workers$arg2, $workers$arg1))(this.#next(), this.#next())", module);
    }

    [Theory]
    [InlineData("@class")]
    [InlineData("@delete")]
    public void RejectsInvalidJavascriptExportNames(string name)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public sealed class {{name}} : WorkerEntrypoint { public string Ping() => "ok"; }
            """));

        Assert.Contains("WRK117", error.Message);
    }

    [Theory]
    [InlineData("public static string Ping() => \"ok\";")]
    [InlineData("public string Name { get; } = \"ok\";")]
    public void RejectsGeneratedClassMembersThatWouldHaveTheWrongJavascriptShape(string member)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public sealed class Entry : WorkerEntrypoint { {{member}} }
            """));

        Assert.Contains("WRK116", error.Message);
    }
}
