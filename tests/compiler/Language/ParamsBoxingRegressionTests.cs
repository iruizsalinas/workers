namespace Workers.Compiler.Tests;

public sealed class ParamsBoxingRegressionTests
{
    [Theory]
    [InlineData("(1, 2)")]
    [InlineData("new Point(1, 2)")]
    [InlineData("new KeyValuePair<int, int>(1, 2)")]
    public void ExpandedObjectParamsPreserveBoxedValues(string value) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            private static string Describe(params object[] values) => values[0].ToString();
            public static object Run() => new
            {
                expanded = Describe({{value}}),
                explicitArray = Describe(new object[] { {{value}} })
            };
        }
        """);
}
