namespace Workers.Compiler.Tests;

public sealed class DeconstructionReceiverBoxingRegressionTests
{
    [Theory]
    [InlineData("object", "var (first, _, _) = point; var (second, _, _) = point;")]
    [InlineData("object", "object? first = null, second = null; foreach (var (captured, _, _) in new[] { point, point }) { if (first == null) first = captured; else second = captured; }")]
    [InlineData("IEquatable<Point>", "var (first, _, _) = point; var (second, _, _) = point;")]
    public void ExtensionDeconstructorsBoxReferenceTypeReceivers(string receiverType, string deconstruction) => DifferentialRunner.Equal($$"""
        public readonly record struct Point(int X, int Y);
        public static class Extensions
        {
            public static void Deconstruct(this {{receiverType}} value, out object captured, out int a, out int b)
            {
                captured = value;
                a = 0;
                b = 0;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                {{deconstruction}}
                return new { same = first == second, text = first!.ToString() };
            }
        }
        """);
}
