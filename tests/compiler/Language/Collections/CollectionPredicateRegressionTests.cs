namespace Workers.Compiler.Tests;

public sealed class CollectionPredicateRegressionTests
{
    [Fact]
    public void PrimitiveSetOperationsRejectNullSequencesWithArgumentNullException() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static bool Catch(Action action)
            {
                try { action(); return false; } catch (ArgumentNullException) { return true; } catch (Exception) { return false; }
            }
            public static object Run()
            {
                var set = new HashSet<int> { 1 };
                return new { union = Catch(() => set.UnionWith(null!)), intersection = Catch(() => set.IntersectWith(null!)), except = Catch(() => set.ExceptWith(null!)) };
            }
        }
        """);
    [Fact]
    public void PredicatesReadReplacementValuesAndIndexMethodsKeepTheirOriginalRange() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var first = new List<int> { 1 };
                var found = first.Find(value => { first[0] = 9; return true; });
                var second = new List<int> { 1 };
                var all = second.FindAll(value => { second[0] = 8; return true; });
                var third = new List<int> { 1 };
                var index = third.FindIndex(value => { if (value == 1) third.Add(2); return value == 2; });
                var fourth = new List<int> { 1 };
                var exists = fourth.Exists(value => { if (value == 1) fourth.Add(2); return value == 2; });
                return new { found, all = all.ToArray(), index, exists };
            }
        }
        """);

    [Fact]
    public void EmptyFindAllStillReturnsAListAndNullReceiversWinOverNullPredicates() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var empty = new List<int>().FindAll(value => true); empty.Add(3);
                List<int>? source = null; var caught = false;
                try { source!.Find(null!); } catch (NullReferenceException) { caught = true; }
                return new { values = empty.ToArray(), caught };
            }
        }
        """);

    [Fact]
    public void PrimitiveHashSetsSerializeAsCollections()
    {
        var value = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) => Response.Json(new { values = new HashSet<int> { 1, 1, 2 } });
            }
            """);
        Assert.Equal(2, value.GetProperty("values").GetArrayLength());
        Assert.Equal(1, value.GetProperty("values")[0].GetInt32());
        Assert.Equal(2, value.GetProperty("values")[1].GetInt32());
    }

    [Fact]
    public void TypedJsonReadsUseRecordEqualityForHashSets()
    {
        var value = JavaScriptRunner.FetchJson("""
            using Workers;
            public sealed record Row(int Value);
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    var input = new Request("https://worker.test/", new RequestInit { Method = "POST", Body = "[{\"value\":1},{\"value\":1},{\"value\":2}]" });
                    var values = await input.JsonAsync<HashSet<Row>>();
                    return Response.Json(new { count = values!.Count, contains = values.Contains(new Row(1)) });
                }
            }
            """);
        Assert.Equal(2, value.GetProperty("count").GetInt32());
        Assert.True(value.GetProperty("contains").GetBoolean());
    }
    [Fact]
    public void FindFindAllAndTrueForAllObserveElementsAddedByPredicates() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var first = new List<int> { 1 };
                var found = first.Find(value => { if (value == 1) first.Add(2); return value == 2; });
                var second = new List<int> { 1 };
                var all = second.FindAll(value => { if (value == 1) second.Add(2); return true; });
                var third = new List<int> { 1 };
                var every = third.TrueForAll(value => { if (value == 1) third.Add(2); return value == 1; });
                return new { found, all = all.ToArray(), every };
            }
        }
        """);

    [Fact]
    public void ListPredicateMethodsRejectNullPredicatesWithArgumentNullException() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string Catch(Action action)
            {
                try { action(); return "none"; }
                catch (ArgumentNullException) { return "null"; }
                catch (Exception) { return "other"; }
            }
            public static object Run()
            {
                var empty = new List<int>();
                return new { find = Catch(() => { empty.Find(null!); }), all = Catch(() => { empty.FindAll(null!); }),
                    index = Catch(() => { empty.FindIndex(null!); }), exists = Catch(() => { empty.Exists(null!); }),
                    every = Catch(() => { empty.TrueForAll(null!); }) };
            }
        }
        """);

    [Fact]
    public void PrimitiveSetsInvalidateEnumerationOnNewInsertionsOnly() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var set = new HashSet<int> { 1, 2 }; var caught = false; var visited = new List<int>();
                try { foreach (var value in set) { visited.Add(value); if (value == 1) set.Add(3); } }
                catch (InvalidOperationException) { caught = true; }
                var duplicates = new HashSet<int> { 1, 2 }; var sum = 0;
                foreach (var value in duplicates) { duplicates.Add(value); sum += value; }
                var removal = new HashSet<int> { 1, 2, 3 }; var removed = 0;
                foreach (var value in removal) { removed += value; removal.Remove(2); }
                return new { caught, visited = visited.ToArray(), sum, removed };
            }
        }
        """);
}
