namespace Workers.Compiler.Tests;

public sealed class InterpolatedTextRegressionTests
{
    [Theory]
    [InlineData("\\r\\n")]
    [InlineData("\\r")]
    [InlineData("\\uD800")]
    [InlineData("\\uDFFF")]
    [InlineData("\\uD83D\\uDE00\\r\\n")]
    public void InterpolatedTextPreservesItsUtf16CodeUnits(string prefix) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                var number = 7;
                var text = $"{{prefix}}{number}";
                return text.Select(character => (int)character).ToArray();
            }
        }
        """);
}
