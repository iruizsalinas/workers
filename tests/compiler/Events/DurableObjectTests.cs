namespace Workers.Compiler.Tests;

public sealed class DurableObjectTests
{
    [Fact]
    public void InlinesConstantsWithoutEmittingClassFields()
    {
        var module = Compile("""
            using Workers;
            public sealed class Constants : DurableObject
            {
                private const string Name = "worker";
                private const int Limit = 4;
                private const bool Enabled = true;
                private const char Marker = 'W';
                private const Mode DefaultMode = Mode.Ready;

                public Response Read() => Response.Json(new
                {
                    Name,
                    qualified = Constants.Limit,
                    Enabled,
                    Marker,
                    DefaultMode
                });
            }
            public enum Mode { None, Ready }
            """);

        Assert.Contains("Name: \"worker\"", module);
        Assert.Contains("qualified: 4", module);
        Assert.Contains("Enabled: true", module);
        Assert.Contains("Marker: \"W\"", module);
        Assert.Contains("DefaultMode: 1", module);
        Assert.DoesNotContain("this.Name", module);
        Assert.DoesNotContain("this.Limit", module);
    }

    [Fact]
    public void EmitsNativeDurableObjectClassWithoutARegistry()
    {
        var module = Compile("""
            using Workers;

            public sealed class Counter : DurableObject
            {
                public override Task<Response> FetchAsync(Request request) =>
                    Task.FromResult(new Response("durable"));

                public ValueTask<int> AddAsync(int left, int right) =>
                    ValueTask.FromResult(left + right);
            }
            """);

        Assert.Contains("import { DurableObject as $workers$DurableObject } from \"cloudflare:workers\";", module);
        Assert.Contains("export class Counter extends $workers$DurableObject {", module);
        Assert.DoesNotContain("constructor(", module);
        Assert.Contains("fetch(request)", module);
        Assert.Contains("add(left, right)", module);
        Assert.DoesNotContain("registry", module, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitializesFieldsToTheirCSharpDefaults()
    {
        var module = Compile("""
            using Workers;
            public sealed class Counter : DurableObject
            {
                private int _count;
                private string? _label;
                public int Value() => _count;
            }
            """);

        Assert.Contains("constructor(ctx, env) {", module);
        Assert.Contains("super(ctx, env);", module);
        Assert.Contains("this._count = 0;", module);
        Assert.Contains("this._label = null;", module);
    }

    [Fact]
    public void PreservesOptionalMethodParameters()
    {
        var module = Compile("""
            using Workers;
            public sealed class Counter : DurableObject
            {
                public int Add(int amount = 1) => amount;
                public int Test() => Add();
            }
            """);

        Assert.Contains("add(amount = 1)", module);
        Assert.Contains("return this.add();", module);
    }

    [Fact]
    public void EmitsIteratorMethodsAsGenerators()
    {
        var module = Compile("""
            using Workers;
            public sealed class Counter : DurableObject
            {
                private IEnumerable<int> Values()
                {
                    yield return 1;
                }
            }
            """);

        Assert.Contains("*#values()", module);
    }

}
