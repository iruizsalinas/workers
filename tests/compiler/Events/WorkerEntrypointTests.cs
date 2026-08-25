namespace Workers.Compiler.Tests;

public sealed class WorkerEntrypointTests
{
    [Fact]
    public void RemovesAsyncOnlyAsATerminalConvention()
    {
        var module = Compile("""
            using Workers;
            [WorkerEntrypoint("Names")]
            public sealed class Names : WorkerEntrypoint
            {
                public Response Asyncify(string value) => Response.Text(value);
                public Task<Response> ResolveAsync(string value) => Task.FromResult(Response.Text(value));
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
            [WorkerEntrypoint("Users")]
            public sealed class Users : WorkerEntrypoint
            {
                public async Task<string> FindAsync(string prefix, int limit = 10)
                {
                    Context.WaitUntil(Task.CompletedTask);
                    return await Environment.Kv("DATA").GetTextAsync(prefix) ?? limit.ToString();
                }
            }
            """);

        Assert.Contains("WorkerEntrypoint", module);
        Assert.Contains("export class Users extends", module);
        Assert.Contains("async find(prefix, limit = 10)", module);
        Assert.Contains("this.ctx.waitUntil(Promise.resolve())", module);
        Assert.Contains("this.env[\"DATA\"]", module);
    }

    [Fact]
    public void EmitsNonPublicHelpersWithoutExposingThemOverRpc()
    {
        var module = Compile("""
            using Workers;
            [WorkerEntrypoint("Users")]
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
    public void InitializesEntrypointInstanceState()
    {
        var module = Compile("""
            using Workers;
            [WorkerEntrypoint("Counter")]
            public sealed class Counter : WorkerEntrypoint
            {
                private int _count;
                public int Value() => _count;
            }
            """);

        Assert.Contains("constructor(ctx, env) { super(ctx, env);", module);
        Assert.Contains("this._count = 0;", module);
    }
}
