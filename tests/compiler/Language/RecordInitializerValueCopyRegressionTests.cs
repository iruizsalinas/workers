namespace Workers.Compiler.Tests;

public sealed class RecordInitializerValueCopyRegressionTests
{
    [Theory]
    [InlineData("sealed record")]
    [InlineData("readonly record struct")]
    public void PositionalRecordInitializersCopyMutableValues(string kind) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public {{kind}} Holder(Point Value);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                var holder = new Holder(new Point(0, 0)) { Value = point };
                point.X = 9;
                return new { stored = holder.Value.X, original = point.X };
            }
        }
        """);
}
