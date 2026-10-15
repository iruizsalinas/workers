namespace Workers.Compiler.Tests;

public sealed class ListEnumerationRegressionTests
{
    [Theory]
    [InlineData("values.Add(9)")]
    [InlineData("values.AddRange(new[] { 4, 5 })")]
    [InlineData("values[0] = values[0]")]
    [InlineData("values.Remove(1)")]
    [InlineData("values.RemoveAt(0)")]
    [InlineData("values.Insert(0, 9)")]
    [InlineData("values.RemoveAll(value => value == 3)")]
    [InlineData("values.Clear()")]
    [InlineData("values.Reverse()")]
    [InlineData("values.Sort()")]
    public void ListMutationsInvalidateDeferredEnumeration(string mutation) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                List<int> values = [3, 1, 2];
                IEnumerable<int> sequence = values.Select(value => value);
                var caught = false;
                var seen = 0;
                try
                {
                    foreach (var value in sequence)
                    {
                        seen++;
                        if (seen > 1) break;
                        {{mutation}};
                    }
                }
                catch (System.Exception) { caught = true; }
                return new { caught, seen };
            }
        }
        """);

    [Theory]
    [InlineData("new List<int>(new[] { 1, 2 })")]
    [InlineData("new[] { 1, 2 }.ToList()")]
    [InlineData("new List<int> { 1, 2 }.FindAll(value => true)")]
    [InlineData("new List<int> { 1, 2 }.GetRange(0, 2)")]
    public void MaterializedListsKeepEnumerationChecks(string creation) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                var values = {{creation}};
                var caught = false;
                try { foreach (var value in values) { values.Clear(); } }
                catch (System.Exception) { caught = true; }
                return caught;
            }
        }
        """);

    [Fact]
    public void ArraysAllowElementUpdatesAndListsAllowSelfCopying() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                int[] array = [1, 2];
                var total = 0;
                foreach (var value in array) { total += value; array[1] = 3; }
                var list = new List<int> { 1, 2 };
                list.AddRange(list);
                var seen = 0;
                foreach (var value in list) { list.Remove(999); list.RemoveAll(item => false); seen++; }
                return new { total, array, list, seen };
            }
        }
        """);

    [Fact]
    public void JsonListsKeepEnumerationChecksWithoutChangingTheirJsonShape()
    {
        var actual = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var values = Json.Parse<List<int>>("[1,2]")!;
                    var caught = false;
                    try { foreach (var value in values) { values.Clear(); } }
                    catch (Exception) { caught = true; }
                    values.Add(3);
                    return Response.Json(new { caught, text = Json.Stringify(values) });
                }
            }
            """);
        Assert.True(actual.GetProperty("caught").GetBoolean());
        Assert.Equal("[3]", actual.GetProperty("text").GetString());
    }

    [Fact]
    public void ListEnumerationDetectsMutationBeforeReadingAnotherElement() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new List<int> { 1, 2 };
                var caught = false;
                var seen = 0;
                try
                {
                    foreach (var value in values)
                    {
                        seen++;
                        values.Add(value);
                        if (seen == 4) break;
                    }
                }
                catch (System.Exception) { caught = true; }
                return new { caught, seen, values };
            }
        }
        """);

    [Fact]
    public void AddRangeRejectsAQueryThatMutatesItsOwnSource() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new List<int> { 1, 2 };
                var calls = 0;
                var caught = false;
                var query = values.Where(value => {
                    calls++;
                    if (calls > 4) throw new System.Exception("bounded reproduction");
                    return true;
                });
                try { values.AddRange(query); }
                catch (System.Exception) { caught = true; }
                return new { caught, calls, values };
            }
        }
        """);
}
