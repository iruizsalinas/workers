namespace Workers.Compiler.Tests;

public sealed class LinqOrderingTests
{
    [Fact]
    public void EmitsEqualityOperatorsOnlyForFaithfulKeyTypes()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<int> values = [1, 2, 1];
                    List<string> words = ["a", "b", "cc"];
                    return Response.Json(new
                    {
                        distinct = values.Distinct().ToArray(),
                        byLength = words.DistinctBy(word => word.Length).ToArray(),
                        equal = values.SequenceEqual(new List<int> { 1, 2, 1 })
                    });
                }
            }
            """);

        Assert.Contains("$workers$linqDistinct(values)", module);
        Assert.Contains("$workers$linqDistinctBy(words, word => word.length)", module);
        Assert.Contains("$workers$linqSequenceEqual(values, [1, 2, 1])", module);
        Assert.Contains("const seen = new Set()", module);
    }

    [Fact]
    public void SupportsNullablePrimitiveEqualityKeys()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<int?> values = [1, null, 1];
                    return Response.Json(new {
                        distinct = values.Distinct().ToArray(),
                        groups = values.GroupBy(value => value).Select(group => group.Count()).ToArray(),
                        union = values.Union(new List<int?> { null, 2 }).ToArray()
                    });
                }
            }
            """);

        Assert.Contains("$workers$linqDistinct(values)", module);
        Assert.Contains("$workers$linqGroupBy(values", module);
        Assert.Contains("$workers$linqSet(values", module);
    }

    [Fact]
    public void EmitsStableMultiKeyOrderingAsADeferredSequence()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<Item> values = [new Item(2, 1, 'B'), new Item(1, 2, 'A')];
                    var ordered = values.OrderBy(value => value.Group)
                        .ThenByDescending(value => value.Score)
                        .ThenBy(value => value.Code);
                    return Response.Json(ordered.Select(value => value.Score).ToArray());
                }
            }
            public sealed record Item(int Group, int Score, char Code);
            """);

        Assert.Contains("$workers$linqOrder(values, value => value.group, false, 0, false)", module);
        Assert.Contains(", value => value.score, true, 0, true)", module);
        Assert.Contains(", value => value.code, false, 1, true)", module);
        Assert.Contains("const $workers$linqOrderState = Symbol()", module);
        Assert.Contains("keys: criteria.map(criterion => criterion.selector(value))", module);
        Assert.Contains("return left.index - right.index", module);
    }

    [Theory]
    [InlineData("words.OrderBy(value => value)")]
    [InlineData("items.OrderBy(value => value)")]
    [InlineData("numbers.OrderBy(value => value, Comparer<int>.Default)")]
    public void RejectsOrderingWithoutFaithfulDefaultComparerSemantics(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<string> words = ["b", "a"];
                    List<Item> items = [new Item(1)];
                    List<int> numbers = [2, 1];
                    return Response.Json({{operation}}.ToArray());
                }
            }
            public sealed record Item(int Value);
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

}
