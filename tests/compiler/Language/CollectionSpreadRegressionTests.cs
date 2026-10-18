namespace Workers.Compiler.Tests;

public sealed class CollectionSpreadRegressionTests
{
    [Theory]
    [InlineData("char[]")]
    [InlineData("List<char>")]
    public void SpreadsEnumerateStringCodeUnitsThroughInterfaces(string target) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                System.Collections.Generic.IEnumerable<char> text = "a\U0001F600z";
                {{target}} copied = [.. text];
                return new { count = copied.Count(), units = copied.Select(value => (int)value).ToArray() };
            }
        }
        """);

    [Theory]
    [InlineData("Dictionary<string, int>")]
    [InlineData("IEnumerable<KeyValuePair<string, int>>")]
    [InlineData("IReadOnlyDictionary<string, int>")]
    public void SpreadsEnumerateDictionaryEntries(string source) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                {{source}} values = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
                KeyValuePair<string, int>[] copied = [.. values];
                return copied.Select(pair => pair.Key + pair.Value).ToArray();
            }
        }
        """);

    [Fact]
    public void SpreadsBoxValueElementsAsIndependentSnapshots() => DifferentialRunner.Equal("""
        public enum Status { Ready = 3 }
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { (1, 2) };
                object[] tuples = [.. values];
                object[] states = [.. new[] { Status.Ready }];
                values[0].Item1 = 9;
                return new { tuple = $"{tuples[0]}", state = $"{states[0]}" };
            }
        }
        """);
}
