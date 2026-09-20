namespace Workers.Compiler.Tests;

public sealed class LinqTests
{
    [Fact]
    public void EmitsLazyStreamingOperatorsAsReusableIterables()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new List<int> { 1, 2, 3 };
                    var query = values.Where((value, index) => value > index)
                        .Select((value, index) => value + index)
                        .Skip(1).Take(2);
                    values.Add(4);
                    List<int> tail = [10];
                    return Response.Json(query.Concat(tail).ToArray());
                }
            }
            """);

        Assert.Contains("$workers$linqWhere(values, (value, index) =>", module);
        Assert.Contains("$workers$linqSelect(", module);
        Assert.Contains("$workers$linqSkip(", module);
        Assert.Contains("$workers$linqTake(", module);
        Assert.Contains("$workers$linqConcat(", module);
        Assert.Contains("$workers$linqToArray(", module);
        Assert.Contains("*[Symbol.iterator]()", module);
        Assert.DoesNotContain(".filter(", module);
        Assert.DoesNotContain(".map(", module);
    }

    [Fact]
    public void EmitsTerminalOperatorsAndTypeCorrectDefaults()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<int> values = [1, 2, 3];
                    List<int> empty = [];
                    List<bool> emptyFlags = [];
                    return Response.Json(new
                    {
                        any = values.Any(), all = values.All(value => value > 0),
                        count = values.Count(value => value > 1), contains = values.Contains(2),
                        first = values.First(), firstMatch = values.First(value => value > 1),
                        firstDefault = empty.FirstOrDefault(), explicitDefault = empty.FirstOrDefault(42),
                        last = values.Last(), single = values.Single(value => value == 2),
                        singleDefault = empty.SingleOrDefault(), boolDefault = emptyFlags.LastOrDefault(),
                        list = values.ToList()
                    });
                }
            }
            """);

        Assert.Contains("$workers$linqAny(values, null, false)", module);
        Assert.Contains("$workers$linqAll(values, value => value > 0)", module);
        Assert.Contains("$workers$linqCount(values, value => value > 1, true)", module);
        Assert.Contains("$workers$linqContains(values, 2)", module);
        Assert.Contains("$workers$linqFirst(empty, null, false, 0, true)", module);
        Assert.Contains("$workers$linqFirst(empty, null, false, 42, true)", module);
        Assert.Contains("$workers$linqLast(emptyFlags, null, false, false, true)", module);
        Assert.Contains("$workers$linqSingle(values, value => value === 2, true, 0, false)", module);
        Assert.Contains("$workers$linqToArray(values)", module);
    }

    [Fact]
    public void EmitsOnlyTheRequestedLinqHelper()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<int> values = [1];
                    return Response.Json(values.Any());
                }
            }
            """);

        Assert.Contains("function $workers$linqAny(source, predicate, hasPredicate)", module);
        Assert.DoesNotContain("function $workers$linqWhere", module);
        Assert.DoesNotContain("function $workers$linqSelect", module);
        Assert.DoesNotContain("function $workers$linqOrder", module);
        Assert.DoesNotContain("function $workers$linqGroupBy", module);
        Assert.DoesNotContain("function $workers$linqNumericAggregate", module);
        Assert.DoesNotContain("function $workers$linqJoin", module);
        Assert.DoesNotContain("function $workers$linqFirst", module);
        Assert.DoesNotContain("function $workers$linqToArray", module);
    }

    [Fact]
    public void EnumeratesStringsAsUtf16Characters()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { count = "😀".Count(), values = "😀".ToArray() });
            }
            """);

        Assert.Contains("function* $workers$linqValues(source)", module);
        Assert.Contains("index < source.length", module);
        Assert.Contains("yield source[index]", module);
        Assert.Contains("$workers$linqCount(", module);
        Assert.Contains(", null, false)", module);
        Assert.Contains("$workers$linqToArray(", module);
        Assert.Contains("Array.from($workers$linqValues(source))", module);
    }

    [Fact]
    public void EmitsAdditionalStreamingAndElementOperators()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<List<int>> groups = [new List<int> { 1, 2 }, new List<int> { 3 }];
                    var values = groups.SelectMany((group, index) => group.Select(value => value + index))
                        .Prepend(0).Append(4).SkipWhile((value, index) => value == index)
                        .TakeWhile(value => value < 4);
                    return Response.Json(new
                    {
                        values = values.ToArray(),
                        item = values.ElementAt(1),
                        missing = values.ElementAtOrDefault(100)
                    });
                }
            }
            """);

        Assert.Contains("$workers$linqSelectMany(", module);
        Assert.Contains("$workers$linqPrepend(", module);
        Assert.Contains("$workers$linqAppend(", module);
        Assert.Contains("$workers$linqSkipWhile(", module);
        Assert.Contains("$workers$linqTakeWhile(", module);
        Assert.Contains("$workers$linqElementAt(", module);
        Assert.Contains("*[Symbol.iterator]()", module);
    }

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
