namespace Workers.Compiler.Tests;

public sealed class LinqAdvancedTests
{
    [Fact]
    public void EmitsGroupingMaterializationAndJoinOperators()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<Item> items = [new Item("a", 1), new Item("a", 2), new Item("b", 3)];
                    var groups = items.GroupBy(item => item.Name, item => item.Value);
                    var lookup = items.ToLookup(item => item.Name, item => item.Value);
                    var dictionary = items.DistinctBy(item => item.Name)
                        .ToDictionary(item => item.Name, item => item.Value);
                    var joined = items.Join(new List<string> { "a" }, item => item.Name, name => name,
                        (item, name) => item.Value);
                    var grouped = items.GroupJoin(new List<string> { "a" }, item => item.Name, name => name,
                        (item, names) => names.Count());
                    return Response.Json(new {
                        groups = groups.Select(group => new { group.Key, Count = group.Count() }).ToArray(),
                        found = lookup.Contains("a"), count = lookup.Count, values = lookup["a"].ToArray(),
                        dictionary, joined = joined.ToArray(), grouped = grouped.ToArray()
                    });
                }
            }
            public sealed record Item(string Name, int Value);
            """);

        Assert.Contains("$workers$linqGroupBy(items, item => item.name, item => item.value)", module);
        Assert.Contains("$workers$linqToLookup(items, item => item.name, item => item.value)", module);
        Assert.Contains("$workers$linqToDictionary(", module);
        Assert.Contains("lookup.contains(\"a\")", module);
        Assert.Contains("lookup.get(\"a\")", module);
        Assert.Contains("$workers$linqJoin(items, [\"a\"]", module);
    }

    [Fact]
    public void EmitsAggregatesSetsAndBufferedSequenceOperators()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<int> values = [1, 2, 3];
                    List<Item> items = [new Item(1, 3), new Item(2, 1), new Item(1, 2)];
                    return Response.Json(new {
                        sum = values.Sum(), average = values.Average(), min = values.Min(), max = values.Max(),
                        minBy = items.MinBy(item => item.Score), maxBy = items.MaxBy(item => item.Score),
                        union = values.Union(new List<int> { 3, 4 }).ToArray(),
                        intersect = values.Intersect(new List<int> { 2 }).ToArray(),
                        except = values.Except(new List<int> { 2 }).ToArray(),
                        unionBy = items.UnionBy(items, item => item.Key).ToArray(),
                        reverse = Enumerable.Reverse(values).ToArray(), defaults = new List<int>().DefaultIfEmpty().ToArray(),
                        chunks = values.Chunk(2).ToArray(),
                        zipped = values.Zip(values, (left, right) => left + right).ToArray(),
                        aggregate = values.Aggregate(10, (total, value) => total + value, total => total * 2)
                    });
                }
            }
            public sealed record Item(int Key, int Score);
            """);

        Assert.Contains("$workers$linqNumericAggregate(values, 0, null, 0, false)", module);
        Assert.Contains("$workers$linqExtremum(items, item => item.score", module);
        Assert.Contains("$workers$linqSet(values", module);
        Assert.Contains("$workers$linqReverse(values)", module);
        Assert.Contains("$workers$linqDefaultIfEmpty([], 0)", module);
        Assert.Contains("$workers$linqChunk(values, 2)", module);
        Assert.Contains("$workers$linqZip(values, values", module);
        Assert.Contains("left.return?.()", module);
        Assert.Contains("right.return?.()", module);
        Assert.Contains("$workers$linqAggregate(values", module);
    }

    [Theory]
    [InlineData("items.GroupBy(item => item)")]
    [InlineData("items.ToDictionary(item => item.Value)")]
    [InlineData("items.Union(items, EqualityComparer<Item>.Default)")]
    [InlineData("items.MinBy(item => item.Name)")]
    public void RejectsNewLinqOverloadsWithoutFaithfulSemantics(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<Item> items = [new Item("a", 1)];
                    return Response.Json({{operation}});
                }
            }
            public sealed record Item(string Name, int Value);
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

    [Theory]
    [InlineData("values.Contains(new Item(1))")]
    [InlineData("values.Contains(new Item(1), EqualityComparer<Item>.Default)")]
    [InlineData("values.Distinct()")]
    [InlineData("values.SequenceEqual(values)")]
    public void RejectsEqualityOverloadsWithoutFaithfulSemantics(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<Item> values = [new Item(1)];
                    return Response.Json({{operation}});
                }
            }
            public sealed record Item(int Value);
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }
}
