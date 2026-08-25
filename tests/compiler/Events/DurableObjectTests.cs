namespace Workers.Compiler.Tests;

public sealed class DurableObjectTests
{
    [Fact]
    public void EmitsNativeDurableObjectClassWithoutARegistry()
    {
        var module = Compile("""
            using Workers;

            [DurableObject("Counter")]
            public sealed class CounterObject
            {
                public CounterObject(DurableObjectState state, Env env) { }

                public Task<Response> FetchAsync(Request request) =>
                    Task.FromResult(Response.Text("durable"));

                public ValueTask<int> AddAsync(int left, int right) =>
                    ValueTask.FromResult(left + right);
            }
            """);

        Assert.Contains("export class Counter", module);
        Assert.Contains("constructor(state, env)", module);
        Assert.Contains("fetch(request)", module);
        Assert.Contains("add(left, right)", module);
        Assert.DoesNotContain("registry", module, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitializesFieldsToTheirCSharpDefaults()
    {
        var module = Compile("""
            using Workers;
            [DurableObject("Counter")]
            public sealed class Counter
            {
                private int _count;
                private string? _label;
                public int Value() => _count;
            }
            """);

        Assert.Contains("this._count = 0;", module);
        Assert.Contains("this._label = null;", module);
    }

}
