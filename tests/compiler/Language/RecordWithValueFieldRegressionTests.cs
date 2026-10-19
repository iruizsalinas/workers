namespace Workers.Compiler.Tests;

public sealed class RecordWithValueFieldRegressionTests
{
    [Fact]
    public void WithExpressionsCopyMutableRecordStructFields() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public sealed record Box
        {
            public Point Value = new Point(0, 0);
            public Box(Point value) { Value = value; }
        }
        public static class Test
        {
            public static object Run()
            {
                var original = new Box(new Point(1, 2));
                var copy = original with { };
                copy.Value.X = 9;
                return new { originalX = original.Value.X, copiedX = copy.Value.X };
            }
        }
        """);

    [Fact]
    public void ValueFieldsAreCopiedBeforeWithInitializersRun() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public sealed record Box
        {
            public Point Value = new Point(1, 2);
            public int Number { get; init; }
        }
        public static class Test
        {
            private static Box original = new Box();
            private static int ChangeOriginal() { original.Value.X = 9; return 7; }
            public static object Run()
            {
                var copy = original with { Number = ChangeOriginal() };
                return new { originalX = original.Value.X, copiedX = copy.Value.X, number = copy.Number };
            }
        }
        """);
}
