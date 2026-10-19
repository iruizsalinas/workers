namespace Workers.Compiler.Tests;

public sealed class RecordStructBoxingRegressionTests
{
    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void EachBoxingConversionCreatesAnIndependentObject(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                object first = point;
                object second = point;
                object alias = first;
                return new { same = first == second, alias = alias == first };
            }
        }
        """);

    [Theory]
    [InlineData("point")]
    [InlineData("(object)point")]
    [InlineData("(Point?)point")]
    public void BoxingCapturesAnIndependentRecordStructValue(string expression) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                object boxed = {{expression}};
                point.X = 9;
                var snapshot = (Point)boxed;
                snapshot.Y = 7;
                var again = (Point)boxed;
                return new { originalX = point.X, boxedX = again.X, boxedY = again.Y, snapshotY = snapshot.Y };
            }
        }
        """);

    [Fact]
    public void BoxingCopiesNestedValuesAndPreservesReferenceMembers() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public sealed class Label { public string Text = "before"; }
        public record struct TaggedPoint(Point Position, Label Label);
        public static class Test
        {
            public static object Run()
            {
                var point = new TaggedPoint(new Point(1, 2), new Label());
                object boxed = point;
                point.Position = new Point(9, 9);
                point.Label.Text = "after";
                var snapshot = (TaggedPoint)boxed;
                return new { x = snapshot.Position.X, y = snapshot.Position.Y, text = snapshot.Label.Text };
            }
        }
        """);
}
