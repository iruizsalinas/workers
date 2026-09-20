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

    [Theory]
    [InlineData("values.Contains(new Item(1))")]
    [InlineData("values.Contains(new Item(1), EqualityComparer<Item>.Default)")]
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
