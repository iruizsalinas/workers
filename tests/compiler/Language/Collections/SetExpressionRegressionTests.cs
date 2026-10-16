namespace Workers.Compiler.Tests;

public sealed class SetExpressionRegressionTests
{
    [Fact]
    public void HashSetCollectionExpressionsCreateSetsAndRemoveDuplicates() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                HashSet<int> values = [1, 1, 2, .. new[] { 2, 3 }];
                var contains = values.Contains(2);
                var added = values.Add(4);
                var duplicate = values.Add(4);
                values.Remove(1);
                HashSet<double?> special = [double.NaN, double.NaN, null];
                return new { contains, added, duplicate, values = values.ToArray(),
                    count = special.Count, containsNull = special.Contains(null), containsNan = special.Contains(double.NaN) };
            }
        }
        """);

    [Fact]
    public void EmitsValueEqualityForRecordSetExpressions()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    HashSet<Key> values = [new Key(1), new Key(1)];
                    return Response.Json(values.Count);
                }
            }
            public sealed record Key(int Value);
            """);
        Assert.Contains("keyCollection", module);
    }
}
