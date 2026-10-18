namespace Workers.Compiler.Tests;

public sealed class IteratorParameterCopyRegressionTests
{
    [Theory]
    [InlineData("public static", "Source.Values")]
    [InlineData("public", "new Source().Values")]
    public void RepeatedEnumerationsStartWithIndependentTupleParameters(string modifiers, string call) => DifferentialRunner.Equal($$"""
        public sealed class Source
        {
            {{modifiers}} IEnumerable<int> Values((int, int) pair)
            {
                pair.Item1++;
                yield return pair.Item1;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var values = {{call}}((1, 2));
                return new { first = values.ToArray(), second = values.ToArray() };
            }
        }
        """);

    [Fact]
    public void LocalIteratorsResetTupleParametersForEachEnumeration() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                IEnumerable<int> Values((int, int) pair)
                {
                    pair.Item1++;
                    yield return pair.Item1;
                }
                var values = Values((1, 2));
                return new { first = values.ToArray(), second = values.ToArray() };
            }
        }
        """);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AsyncIteratorsResetTupleParametersForEachEnumeration(bool local) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            {{(local ? "" : "private static async IAsyncEnumerable<int> Values((int, int) pair) { await Task.CompletedTask; pair.Item1++; yield return pair.Item1; }")}}
            public static async Task<object> Run()
            {
                {{(local ? "async IAsyncEnumerable<int> Values((int, int) pair) { await Task.CompletedTask; pair.Item1++; yield return pair.Item1; }" : "")}}
                var values = Values((1, 2));
                var first = new List<int>();
                var second = new List<int>();
                await foreach (var item in values) first.Add(item);
                await foreach (var item in values) second.Add(item);
                return new { first, second };
            }
        }
        """);
}
