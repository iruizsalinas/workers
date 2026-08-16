namespace Workers.Compiler.Tests;

public sealed class WorkerEntrypointTests
{
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
        Assert.Contains("this.ctx.waitUntil(undefined)", module);
        Assert.Contains("this.env[\"DATA\"]", module);
    }
}
