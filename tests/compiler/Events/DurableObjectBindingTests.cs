namespace Workers.Compiler.Tests;

public sealed class DurableObjectBindingTests
{
    [Fact]
    public void LowersDurableObjectNamespaceStorageSqlAndContainerMethods()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;

            [DurableObject("Counter")]
            public sealed class Counter
            {
                public async Task Tick(DurableObjectState state, Env env)
                {
                    var stub = env.DurableObject("COUNTERS").GetByName("primary");
                    var response = await stub.FetchAsync("https://counter");
                    var value = await stub.InvokeAsync<int>("read", ["count"]);
                    await stub.InvokeVoidAsync("reset");
                    await state.Storage.PutAsync("count", 1);
                    var count = await state.Storage.GetAsync<int>("count");
                    var rows = await state.Storage.Sql.Prepare("SELECT 1").AllAsync<object>();
                    await state.Container.SignalAsync(15);
                }
            }
            """);

        Assert.Contains("env[\"COUNTERS\"].getByName(\"primary\")", module);
        Assert.Contains("stub.fetch(\"https://counter\")", module);
        Assert.Contains("stub[\"read\"]", module);
        Assert.Contains("stub[\"reset\"]()", module);
        Assert.Contains("state.storage.put(\"count\", 1)", module);
        Assert.Contains("state.storage.get(\"count\")", module);
        Assert.Contains("state.storage.sql.exec(\"SELECT 1\").all()", module);
        Assert.Contains("state.container.signal(15)", module);
    }

    [Fact]
    public void EmitsDurableObjectBaseConstructorAndInstanceFieldAccess()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            [DurableObject("Stateful")]
            public sealed class Stateful
            {
                private readonly DurableObjectState _state;
                public Stateful(DurableObjectState objectState, Env environment)
                {
                    _state = objectState;
                }

                public async Task<int> Increment()
                {
                    var count = await _state.Storage.GetAsync<int>("count");
                    await _state.Storage.PutAsync("count", count + 1);
                    return count + 1;
                }
            }
            """);

        Assert.StartsWith("import { DurableObject as $workers$DurableObject } from \"cloudflare:workers\";", module);
        Assert.Contains("export class Stateful extends $workers$DurableObject", module);
        Assert.Contains("constructor(objectState, environment) { super(objectState, environment);", module);
        Assert.Contains("this._state = objectState", module);
        Assert.Contains("this._state.storage.get(\"count\")", module);
        Assert.Contains("this._state.storage.put(\"count\", (count + 1) | 0)", module);
    }

}
