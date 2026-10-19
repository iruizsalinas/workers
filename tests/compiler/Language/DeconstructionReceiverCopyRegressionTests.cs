namespace Workers.Compiler.Tests;

public sealed class DeconstructionReceiverCopyRegressionTests
{
    [Fact]
    public void PositionalPatternsPassValueReceiversByValueToExtensionDeconstructors() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Extensions
        {
            public static void Deconstruct(this Point point, out int x, out int y, out int sum)
            {
                point.X++;
                x = point.X;
                y = point.Y;
                sum = 4;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                var matched = point is (2, 2, 4);
                return new { matched, x = point.X, y = point.Y };
            }
        }
        """);
}
