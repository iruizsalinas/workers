namespace Workers.Compiler.Tests;

public sealed class SequenceIndexerTests
{
    [Fact]
    public void BoundsChecksSequenceReadsWritesAndConditionalAccess()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    int[] values = [1, 2];
                    values[0] = values[1];
                    List<int>? optional = values.ToList();
                    return Response.Json(new { first = values[0], character = "ab"[1], optional = optional?[0] });
                }
            }
            """);

        Assert.Contains("sequenceSet(values, 0, $workers$sequenceIndex(values, 1))", module);
        Assert.Contains("$workers$sequenceIndex(\"ab\", 1)", module);
        Assert.Contains("== null ? null : $workers$sequenceIndex(", module);
        Assert.Contains("index < 0 || index >= source.length", module);
    }

    [Fact]
    public void DoesNotShipSequenceBoundsHelperWhenNoIndexerIsUsed()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Text("ok");
            }
            """);

        Assert.DoesNotContain("sequenceIndex", module);
        Assert.DoesNotContain("sequenceSet", module);
    }

    [Fact]
    public void UsesPrototypeSafeDictionariesAndChecksIndexerKeys()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new Dictionary<string, int> { ["__proto__"] = 1 };
                    values["answer"] = 42;
                    Dictionary<string, int>? optional = values;
                    return Response.Json(new { special = values["__proto__"], optional = optional?["answer"] });
                }
            }
            """);

        Assert.Contains("Object.assign(Object.create(null), { [\"__proto__\"]: 1 })", module);
        Assert.Contains("dictionarySet(values, \"answer\", 42)", module);
        Assert.Contains("dictionaryIndex(values, \"__proto__\")", module);
        Assert.Contains("Object.hasOwn(source, key)", module);
    }
}

