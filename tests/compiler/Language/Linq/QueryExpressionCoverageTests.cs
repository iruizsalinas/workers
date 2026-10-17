namespace Workers.Compiler.Tests;

public sealed class QueryExpressionCoverageTests
{
    [Fact]
    public void TypedQueriesAcceptTheirOrderedSequences()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    var ordered = new[] { 3, 1, 2 }.OrderBy(value => value);
                    return (from IOrderedEnumerable<int> items in new[] { ordered }
                        from int value in items select value).ToArray();
                }
            }
            """);
    }

    [Fact]
    public void TypedQueriesAcceptTheirGroupingResults()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    var groups = new[] { 1, 2, 3 }.GroupBy(value => value % 2);
                    return (from IGrouping<int, int> g in groups
                        select new { key = g.Key, values = g.ToArray() }).ToArray();
                }
            }
            """);
    }

    [Fact]
    public void TypedQueriesPreserveNativeStringEnums()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request) {
                    object[] formats = { QueueContentType.Json, QueueContentType.V8 };
                    object[] algorithms = { DigestAlgorithm.Sha256 };
                    return Response.Json(new {
                        formats = (from QueueContentType format in formats select format).ToArray(),
                        algorithms = (from DigestAlgorithm algorithm in algorithms select algorithm).ToArray()
                    });
                }
            }
            """);
        Assert.Equal(new[] { "json", "v8" }, result.GetProperty("formats").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("SHA-256", result.GetProperty("algorithms")[0].GetString());
    }

    [Fact]
    public void RecordKeysKeepPropertiesThatDifferOnlyByCase()
    {
        DifferentialRunner.Equal("""
            public sealed record Key(int Value, int value);
            public static class Test
            {
                public static object Run() => (from key in new[] { new Key(1, 0), new Key(2, 0) }
                    group key by key into g
                    select new { upper = g.Key.Value, lower = g.Key.value, count = g.Count() }).ToArray();
            }
            """);
    }

    [Fact]
    public void TypedCollectionClausesAcceptArraysAndSets()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    object[] values = { new[] { 2, 3 }, new HashSet<int> { 4, 5 } };
                    object[] dictionaries = { new Dictionary<string, int> { ["a"] = 4 } };
                    return new {
                        mutable = (from ICollection<int> items in values select items.Count).ToArray(),
                        readOnly = (from IReadOnlyCollection<int> items in values select items.Count).ToArray(),
                        dictionaries = (from IReadOnlyCollection<KeyValuePair<string, int>> items in dictionaries
                                        select items.Count).ToArray()
                    };
                }
            }
            """);
    }

    [Fact]
    public void TypedEnumerableClausesAcceptNativeSetsAndDictionaryEntries()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    object[] sets = { new HashSet<int> { 2, 3 } };
                    object[] dictionaries = { new Dictionary<string, int> { ["a"] = 4 } };
                    return new {
                        sets = (from IEnumerable<int> set in sets from value in set select value).ToArray(),
                        pairs = (from IEnumerable<KeyValuePair<string, int>> dictionary in dictionaries
                                 from pair in dictionary select pair.Value).ToArray()
                    };
                }
            }
            """);
    }

    [Fact]
    public void ConditionalLinqCallsPreserveNullShortCircuiting()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    int[] missing = null;
                    int calls = 0;
                    Func<int, int> Selector() { calls++; return value => value * 2; }
                    var skipped = missing?.Select(selector: Selector()).ToArray();
                    var present = new[] { 1, 2 }?.Select(value => value * 2).ToArray();
                    return new { skipped, present, calls };
                }
            }
            """);
    }

    [Fact]
    public void NamedLinqArgumentsKeepReceiverEvaluationBeforeArguments()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private static int[] values = new[] { 1, 2 };
                private static Func<int, int> Selector() {
                    values = new[] { 9 };
                    return value => value * 2;
                }
                public static object Run() => values.Select(selector: Selector()).ToArray();
            }
            """);
    }

    [Fact]
    public void ExplicitComparersWorkWithNamedAndStaticOperatorCalls()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    var words = new[] { "b", "a" };
                    return new {
                        named = words.OrderBy(comparer: StringComparer.Ordinal, keySelector: value => value).ToArray(),
                        direct = Enumerable.OrderBy(words, value => value, StringComparer.Ordinal).ToArray()
                    };
                }
            }
            """);
    }

    [Fact]
    public void TypedQueriesKeepNativeWorkerValues()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request) {
                    object[] values = { new Url("https://worker.test/a"), new Url("https://worker.test/b"), null };
                    return Response.Json((from Url? url in values select url?.Pathname).ToArray());
                }
            }
            """);
        Assert.Equal(new[] { "/a", "/b", null }, result.EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void TypedQueriesSupportEnumAndCollectionRepresentations()
    {
        DifferentialRunner.Equal("""
            public enum Kind { One = 1, Two = 2 }
            public static class Test
            {
                public static object Run() {
                    object[] kinds = { Kind.One, Kind.Two };
                    object[] lists = { new List<int> { 3, 4 } };
                    return new {
                        kinds = (from Kind kind in kinds select (int)kind).ToArray(),
                        lists = (from IEnumerable<int> list in lists from value in list select value).ToArray()
                    };
                }
            }
            """);
    }

    [Fact]
    public void TypedFromSupportsRecordsTuplesArraysAndNullableValues()
    {
        DifferentialRunner.Equal("""
            public sealed record Item(int Value);
            public static class Test
            {
                public static object Run() {
                    object[] records = { new Item(2), null };
                    object[] tuples = { (3, 4) };
                    object[] arrays = { new int[] { 5, 6 } };
                    object[] nullable = { 7, null };
                    return new {
                        records = (from Item item in records select item == null ? 0 : item.Value).ToArray(),
                        tuples = (from (int a, int b) pair in tuples select pair.a + pair.b).ToArray(),
                        arrays = (from int[] array in arrays from value in array select value).ToArray(),
                        nullable = (from int? value in nullable select value ?? -1).ToArray()
                    };
                }
            }
            """);
    }

    [Fact]
    public void AnonymousKeysKeepDistinctPropertiesThatDifferOnlyByCase()
    {
        DifferentialRunner.Equal("""
            public sealed record Item(int Value);
            public static class Test
            {
                public static object Run() => (from a in new[] { new Item(1), new Item(2) }
                    group a by new { a.Value, value = 0 } into g
                    select new { upper = g.Key.Value, lower = g.Key.value, count = g.Count() }).ToArray();
            }
            """);
    }

    [Fact]
    public void GroupClausePassesKeyBeforeElementToSourceDefinedOperators()
    {
        DifferentialRunner.Equal("""
            public sealed class Query(int value)
            {
                public string GroupBy(Func<int, int> key, Func<int, int> element) => key(value) + ":" + element(value);
            }
            public static class Test
            {
                public static object Run() => from value in new Query(2) group value * 10 by value + 1;
            }
            """);
    }

    [Fact]
    public void GroupClauseKeepsKeyAndElementSelectorsInTheirSpecifiedRoles()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() => (from value in new[] { 1, 2, 3, 4 }
                    group value * 10 by value % 2 into g
                    select new { key = g.Key, values = g.ToArray() }).ToArray();
            }
            """);
    }

    [Fact]
    public void InferredAndExplicitAnonymousKeysShareTheSamePropertyIdentity()
    {
        DifferentialRunner.Equal("""
            public sealed record Item(int Value);
            public static class Test
            {
                public static object Run() {
                    var items = new[] { new Item(1), new Item(2) };
                    return (from a in items join b in items
                            on new { a.Value } equals new { Value = b.Value }
                            select a.Value + b.Value).ToArray();
                }
            }
            """);
    }

    [Fact]
    public void CompositeKeysSupportTuplesRecordsAndNullGroups()
    {
        DifferentialRunner.Equal("""
            public sealed record Key(int Value);
            public static class Test
            {
                public static object Run() {
                    var tupleGroups = from value in new[] { 1, 2, 3 }
                                      group value by (Odd: value % 2, Other: 0) into g
                                      select new { key = g.Key.Odd, values = g.ToArray() };
                    var recordGroups = from value in new[] { 1, 2, 3 }
                                       group value by value == 2 ? null : new Key(1) into g
                                       select new { missing = g.Key == null, values = g.ToArray() };
                    return new { tuples = tupleGroups.ToArray(), records = recordGroups.ToArray() };
                }
            }
            """);
    }

    [Fact]
    public void TypedFromClausesAndTypedJoins()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    object[] first = { 1, 2, 3 };
                    object[] second = { 2, 3 };
                    var initial = from int a in first where a > 1 select a * 2;
                    var nested = from int a in first from int b in second where a == b select a + b;
                    var joined = from int a in first join int b in second on a equals b select a * b;
                    return new { initial = initial.ToArray(), nested = nested.ToArray(), joined = joined.ToArray() };
                }
            }
            """);
    }

    [Fact]
    public void TypedFromChecksValuesLazilyAndDisposesItsSource()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private static bool disposed;
                private static IEnumerable<object> Values() {
                    try { yield return "first"; yield return 2; }
                    finally { disposed = true; }
                }
                public static object Run() {
                    var query = from string value in Values() select value;
                    bool deferred = !disposed;
                    string first = query.First();
                    bool firstDisposed = disposed;
                    disposed = false;
                    bool failed = false;
                    try { query.ToArray(); } catch (InvalidCastException) { failed = true; }
                    return new { deferred, first, firstDisposed, failed, disposed };
                }
            }
            """);
    }

    [Fact]
    public void JoinsAndGroupsWithAnonymousCompositeKeys()
    {
        DifferentialRunner.Equal("""
            public sealed record Item(string Name, int Region, int Value);
            public static class Test
            {
                public static object Run() {
                    var items = new[] { new Item("a", 1, 2), new Item("a", 1, 3), new Item("a", 2, 4) };
                    var joined = from a in items
                                 join b in items on new { a.Name, a.Region } equals new { b.Name, b.Region }
                                 select a.Value + b.Value;
                    var grouped = from item in items group item.Value by new { item.Name, item.Region } into g
                                  select new { name = g.Key.Name, region = g.Key.Region, total = g.Sum() };
                    var left = from a in items
                               join b in items on new { a.Name, a.Region } equals new { b.Name, b.Region } into matches
                               from match in matches.DefaultIfEmpty()
                               let total = a.Value + match.Value
                               where total > 5
                               select total;
                    return new { joined = joined.ToArray(), grouped = grouped.ToArray(), left = left.ToArray() };
                }
            }
            """);
    }

    [Fact]
    public void GroupContinuationCanUseACompositeKeyAgain()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    var values = new[] { 1, 2, 3, 4 };
                    return (from value in values
                            group value by new { Odd = value % 2 } into first
                            group first.Sum() by new { first.Key.Odd } into second
                            select new { odd = second.Key.Odd, values = second.ToArray() }).ToArray();
                }
            }
            """);
    }

    [Fact]
    public void QueryOrderingSupportsNativeStringsAndMultipleDirections()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request) => Response.Json(
                    (from word in new string[] { "b", "A", "a", "aa", "b" }
                     orderby word.Length descending, word ascending
                     select word).ToArray());
            }
            """);
        Assert.Equal(new[] { "aa", "A", "a", "b", "b" }, result.EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void GroupJoinContinuationKeepsAllRangeVariablesInScope()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    return (from a in new[] { 1, 2, 3 }
                            let key = a * 2
                            join b in new[] { 2, 2, 6 } on key equals b into matches
                            let count = matches.Count()
                            orderby count descending, a ascending
                            select new { a, key, count } into result
                            where result.count > 0
                            select result).ToArray();
                }
            }
            """);
    }
}
