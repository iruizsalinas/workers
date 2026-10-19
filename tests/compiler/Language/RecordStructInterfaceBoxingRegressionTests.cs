namespace Workers.Compiler.Tests;

public sealed class RecordStructInterfaceBoxingRegressionTests
{
    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void InterfaceConversionsCreateIndependentBoxes(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                IEquatable<Point> first = point;
                IEquatable<Point> second = point;
                var same = first == second;
                return new { same };
            }
        }
        """);

    [Theory]
    [InlineData("point")]
    [InlineData("(Point?)point")]
    public void InterfaceConversionsCaptureValueFields(string expression) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                IEquatable<Point> boxed = {{expression}};
                point.X = 9;
                var snapshot = (Point)(object)boxed;
                return new { stored = snapshot.X, changed = point.X };
            }
        }
        """);

    [Fact]
    public void NullNullableRecordStructConvertsToNullInterface() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                Point? point = null;
                IEquatable<Point>? boxed = point;
                return boxed == null;
            }
        }
        """);
}
