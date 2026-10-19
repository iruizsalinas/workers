namespace Workers.Compiler.Tests;

public sealed class RecordStructTemporaryReceiverRegressionTests
{
    [Theory]
    [InlineData("(new Point(1, 2), 0).Item1")]
    [InlineData("Pair.Item1")]
    [InlineData("(((Point, double))Pair).Item1")]
    public void EqualityCallsAcceptFieldsOfTemporaryTuples(string receiver) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            private static (Point, int) Pair => (new Point(1, 2), 0);
            public static object Run() => {{receiver}}.Equals(new Point(1, 2));
        }
        """);
}
