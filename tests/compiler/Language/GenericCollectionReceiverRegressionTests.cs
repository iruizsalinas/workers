namespace Workers.Compiler.Tests;

public sealed class GenericCollectionReceiverRegressionTests
{
    [Theory]
    [InlineData("source[1]")]
    [InlineData("source.Keys.ToArray()")]
    [InlineData("source.Values.ToArray()")]
    public void GenericDictionaryReadsUseTheSpecializedRepresentation(string expression) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static object Read<T>(T source) where T : IDictionary<int, int> => {{expression}};
            public static object Run() => Read(new Dictionary<int, int> { [1] = 7 });
        }
        """);

    [Theory]
    [InlineData("source[1] = 9;")]
    [InlineData("source[1] += 2;")]
    [InlineData("source[1]++;")]
    public void GenericDictionaryWritesUseTheSpecializedRepresentation(string mutation) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static void Update<T>(T source) where T : IDictionary<int, int> { {{mutation}} }
            public static object Run()
            {
                var source = new Dictionary<int, int> { [1] = 7 };
                Update(source);
                return source[1];
            }
        }
        """);

    [Theory]
    [InlineData("new List<int> { 7 }")]
    [InlineData("new[] { 7 }")]
    public void GenericSequenceReadsStillCheckBounds(string source) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static bool Read<T>(T source) where T : IList<int>
            {
                try { var value = source[2]; }
                catch (System.Exception) { return true; }
                return false;
            }
            public static object Run() => Read({{source}});
        }
        """);

    [Theory]
    [InlineData("new List<int> { 1, 2, 3 }")]
    [InlineData("new[] { 1, 2, 3 }")]
    public void GenericListPatternsUseTheSpecializedInputType(string source) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static bool Matches<T>(T source) where T : IReadOnlyList<int> => source is [1, .., 3];
            public static object Run() => Matches({{source}});
        }
        """);
}
