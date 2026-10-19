namespace Workers.Compiler.Tests;

public sealed class RecordStructEqualityCaptureRegressionTests
{
    [Theory]
    [InlineData("point == Change()")]
    [InlineData("point != Change()")]
    [InlineData("point.Equals(Change())")]
    public void RecordEqualityPreservesValueOperandsAndInstanceReceiverStorage(string expression) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            private static Point point = new Point(1, 2);
            private static Point Change() { point.X = 9; return new Point(1, 2); }
            public static object Run()
            {
                var equal = {{expression}};
                return new { equal, changed = point.X };
            }
        }
        """);

    [Theory]
    [InlineData("point", "point = new Point(9, 2);")]
    [InlineData("values[0]", "values[0] = new Point(9, 2);")]
    [InlineData("holder.Value", "holder.Value = new Point(9, 2);")]
    [InlineData("Property", "point = new Point(9, 2);")]
    public void InstanceEqualityReadsVariableStorageAndCapturesPropertyValues(string receiver, string mutation) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public sealed class Holder { public Point Value = new Point(1, 2); }
        public static class Test
        {
            private static Point point = new Point(1, 2);
            private static Point[] values = [new Point(1, 2)];
            private static Holder holder = new Holder();
            private static Point Property => point;
            private static Point Change() { {{mutation}} return new Point(1, 2); }
            public static object Run()
            {
                var equal = {{receiver}}.Equals(Change());
                return new { equal, changed = point.X, element = values[0].X, field = holder.Value.X };
            }
        }
        """);

    [Fact]
    public void InstanceEqualityTracksCapturedLocalReceiverStorage() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                Point Change() { point = new Point(9, 2); return new Point(1, 2); }
                return point.Equals(Change());
            }
        }
        """);
}
