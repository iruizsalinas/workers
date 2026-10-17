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

        Assert.Contains("$workers$notNull($workers$notNull(this.env)[\"COUNTERS\"]).getByName(\"primary\")", module);
        Assert.Contains("$workers$notNull(stub).fetch(\"https://counter\")", module);
        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver)[$workers$arg1](...$workers$rpcArguments([$workers$arg2])))(stub, \"read\", \"count\")", module);
        Assert.Contains("$workers$notNull(stub)[\"reset\"]()", module);
        Assert.Contains("$workers$notNull($workers$notNull(this.ctx).storage).put(\"count\", 1)", module);
        Assert.Contains("$workers$notNull($workers$notNull(this.ctx).storage).get(\"count\")", module);
        Assert.Contains("let rows = $workers$notNull((($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).exec($workers$arg1$2, $workers$arg2$2))($workers$notNull($workers$notNull(this.ctx).storage).sql, \"SELECT ? AS value\", 1)).toArray();", module);
        Assert.Contains("let container = $workers$notNull(this.ctx).container;", module);
        Assert.Contains("if (!($workers$notNull(container).running)) {", module);
        Assert.Contains("$workers$notNull(container).start();", module);
        Assert.Contains("$workers$notNull(container).signal(15);", module);
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
        Assert.Contains("class $workers$Stateful extends $workers$DurableObject", module);
        Assert.Contains("export { $workers$Stateful as Stateful };", module);
        Assert.Contains("constructor(ctx, env) {", module);
        Assert.Contains("super(ctx, env);", module);
        Assert.Contains("this._increments = 1", module);
        Assert.Contains("$workers$notNull($workers$notNull(this.ctx).storage).get(\"count\")", module);
        Assert.Contains(".put($workers$arg1, $workers$arg2))($workers$notNull(this.ctx).storage, \"count\", ((count + this._increments) | 0))", module);
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

        Assert.Contains("$workers$notNull($workers$notNull(env)[\"OBJECTS\"]).jurisdiction(\"eu\")", module);
        Assert.Contains("$workers$notNull(ns).idFromName(\"one\")", module);
        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).idFromString($workers$arg1))(ns, $workers$notNull(first).toString())", module);
        Assert.Contains("$workers$notNull(ns).newUniqueId()", module);
        Assert.Contains("$workers$notNull(first).equals(restored)", module);
        Assert.Contains("jurisdiction: $workers$notNull(first).jurisdiction", module);
        Assert.Contains("id: $workers$notNull(stub).id", module);
    }

}
