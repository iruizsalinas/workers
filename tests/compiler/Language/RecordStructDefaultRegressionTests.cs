namespace Workers.Compiler.Tests;

public sealed class RecordStructDefaultRegressionTests
{
    [Theory]
    [InlineData("record struct", "new Point()")]
    [InlineData("record struct", "new Point { X = 5 }")]
    [InlineData("record struct", "default(Point)")]
    [InlineData("record struct", "Create<Point>()")]
    [InlineData("readonly record struct", "new Point()")]
    [InlineData("readonly record struct", "new Point { X = 5 }")]
    [InlineData("readonly record struct", "default(Point)")]
    [InlineData("readonly record struct", "Create<Point>()")]
    public void DefaultRecordStructConstructionInitializesEveryValueField(string declaration, string construction) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            private static T Create<T>() where T : new() => new T();
            public static object Run()
            {
                var point = {{construction}};
                return new { x = point.X, y = point.Y };
            }
        }
        """);

    [Fact]
    public void FieldsAndArrayElementsHaveIndependentDefaultValues() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public sealed class Holder { public Point Value; }
        public static class Test
        {
            public static object Run()
            {
                var holder = new Holder();
                var values = new Point[2];
                values[0].X = 7;
                var copy = holder.Value;
                copy.Y = 9;
                return new { fieldX = holder.Value.X, fieldY = holder.Value.Y,
                    first = values[0].X, second = values[1].X, copiedY = copy.Y };
            }
        }
        """);

    [Fact]
    public void ParameterlessConstructionUsesZeroInsteadOfOptionalConstructorDefaults() => DifferentialRunner.Equal("""
        public record struct Point(int X = 5, int Y = 6);
        public static class Test
        {
            public static object Run()
            {
                var zero = new Point();
                var supplied = new Point(X: 7);
                return new { zeroX = zero.X, zeroY = zero.Y, suppliedX = supplied.X, suppliedY = supplied.Y };
            }
        }
        """);
}
