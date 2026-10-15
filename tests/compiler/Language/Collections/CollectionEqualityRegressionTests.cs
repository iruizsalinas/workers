namespace Workers.Compiler.Tests;

public sealed class CollectionEqualityRegressionTests
{
    [Fact]
    public void ListsCompareKeyValuePairsByTheirContents() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var items = new List<KeyValuePair<string, int>> { new("key", 7), new("other", 9) };
                var pair = new KeyValuePair<string, int>("key", 7);
                var contains = items.Contains(pair);
                var index = items.IndexOf(pair);
                var removed = items.Remove(pair);
                var nanPairs = new List<KeyValuePair<string, double>> { new("nan", double.NaN) };
                return new { contains, index, removed, count = items.Count,
                    nanEqual = nanPairs.Contains(new KeyValuePair<string, double>("nan", double.NaN)) };
            }
        }
        """);

    [Fact]
    public void RecordsCompareKeyValuePairFieldsByTheirContents() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var left = new Entry(new KeyValuePair<string, int>("key", 7));
                var right = new Entry(new KeyValuePair<string, int>("key", 7));
                var different = new Entry(new KeyValuePair<string, int>("key", 8));
                return new { equal = left == right, different = left == different };
            }
        }
        public sealed record Entry(KeyValuePair<string, int> Pair);
        """);

    [Fact]
    public void SortingWithANullComparisonThrowsInsteadOfUsingTheDefault() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new List<int> { 3, 1, 2 };
                var caught = false;
                try { values.Sort((Comparison<int>)null); }
                catch (System.Exception) { caught = true; }
                return new { caught, values };
            }
        }
        """);
}
