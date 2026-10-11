namespace Workers.Compiler.Tests;

public sealed class DurableObjectBindingTests
{
    [Fact]
    public void LowersDurableObjectNamespaceStorageSqlAndContainerMethods()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;

            public sealed class Counter : DurableObject
            {
                public async Task Tick()
                {
                    var stub = Env.DurableObject("COUNTERS").GetByName("primary");
                    var response = await stub.FetchAsync("https://counter");
                    var value = await stub.InvokeAsync<int>("read", "count");
                    await stub.InvokeAsync("reset");
                    await Ctx.Storage.PutAsync("count", 1);
                    var count = await Ctx.Storage.GetAsync<int>("count");
                    var rows = Ctx.Storage.Sql.Exec<object>("SELECT ? AS value", 1).ToArray();
                    var container = Ctx.Container!;
                    if (!container.Running) container.Start();
                    container.Signal(15);
                }
            }
            """);

        Assert.Contains("this.env[\"COUNTERS\"].getByName(\"primary\")", module);
        Assert.Contains("stub.fetch(\"https://counter\")", module);
        Assert.Contains("stub[\"read\"](...$workers$rpcArguments([\"count\"]))", module);
        Assert.Contains("stub[\"reset\"]()", module);
        Assert.Contains("this.ctx.storage.put(\"count\", 1)", module);
        Assert.Contains("this.ctx.storage.get(\"count\")", module);
        Assert.Contains("let rows = this.ctx.storage.sql.exec(\"SELECT ? AS value\", 1).toArray();", module);
        Assert.Contains("let container = this.ctx.container;", module);
        Assert.Contains("if (!(container.running)) {", module);
        Assert.Contains("container.start();", module);
        Assert.Contains("container.signal(15);", module);
    }

    [Fact]
    public void EmitsDurableObjectBaseConstructorAndInstanceFieldAccess()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Stateful : DurableObject
            {
                private int _increments;

                public Stateful() => _increments = 1;

                public async Task<int> Increment()
                {
                    var count = await Ctx.Storage.GetAsync<int>("count");
                    await Ctx.Storage.PutAsync("count", count + _increments);
                    return count + _increments;
                }
            }
            """);

        Assert.StartsWith("import { DurableObject as $workers$DurableObject } from \"cloudflare:workers\";", module);
        Assert.Contains("export class Stateful extends $workers$DurableObject", module);
        Assert.Contains("constructor(ctx, env) {", module);
        Assert.Contains("super(ctx, env);", module);
        Assert.Contains("this._increments = 1", module);
        Assert.Contains("this.ctx.storage.get(\"count\")", module);
        Assert.Contains("this.ctx.storage.put(\"count\", ((count + this._increments) | 0))", module);
    }

    [Fact]
    public void UsesTheSynchronousCurrentDurableObjectIdentityApi()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var ns = env.DurableObject("OBJECTS").Jurisdiction("eu");
                    var first = ns.IdFromName("one");
                    var restored = ns.IdFromString(first.ToString());
                    var unique = ns.NewUniqueId();
                    var stub = ns.Get(first);
                    return Response.Json(new { same = first.Equals(restored), idName = first.Name, first.Jurisdiction, stub.Id, stubName = stub.Name });
                }
            }
            """);

        Assert.Contains("env[\"OBJECTS\"].jurisdiction(\"eu\")", module);
        Assert.Contains("ns.idFromName(\"one\")", module);
        Assert.Contains("ns.idFromString(first.toString())", module);
        Assert.Contains("ns.newUniqueId()", module);
        Assert.Contains("first.equals(restored)", module);
        Assert.Contains("jurisdiction: first.jurisdiction", module);
        Assert.Contains("id: stub.id", module);
    }

}
