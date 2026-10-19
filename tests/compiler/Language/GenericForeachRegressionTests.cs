namespace Workers.Compiler.Tests;

public sealed class GenericForeachRegressionTests
{
    [Fact]
    public void SpecializedStringReceiversEnumerateUtf16CodeUnits() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int[] Units<T>(T source) where T : IEnumerable<char>
            {
                var values = new List<int>();
                foreach (var character in source) values.Add(character);
                return values.ToArray();
            }
            public static object Run() => Units("a\U0001F600z");
        }
        """);

    [Fact]
    public void SpecializedDictionaryReceiversEnumerateEntries() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string[] Entries<T>(T source) where T : IEnumerable<KeyValuePair<string, int>>
            {
                var values = new List<string>();
                foreach (var entry in source) values.Add(entry.Key + entry.Value);
                return values.ToArray();
            }
            public static object Run() => Entries(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
        }
        """);
}
