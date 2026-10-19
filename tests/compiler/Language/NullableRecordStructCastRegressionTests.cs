namespace Workers.Compiler.Tests;

public sealed class NullableRecordStructCastRegressionTests
{
    [Theory]
    [InlineData("boxed as Point?")]
    [InlineData("(Point?)boxed")]
    public void NullableRecordStructCastsPreserveNullAndCopyBoxedValues(string expression) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            private static Point? Read(object? boxed) => {{expression}};
            public static object Run()
            {
                object boxed = new Point(1, 2);
                var value = Read(boxed);
                var copy = value!.Value;
                copy.X = 9;
                return new { absent = Read(null) == null, stored = Read(boxed)!.Value.X, changed = copy.X };
            }
        }
        """);

    [Fact]
    public void NullableRecordStructCastsCheckTypeAndEvaluateOperandsOnce() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public readonly record struct Other(int X, int Y);
        public static class Test
        {
            private static int calls;
            private static object Input() { calls++; return new Other(1, 2); }
            public static object Run()
            {
                var optional = Input() as Point?;
                var caught = false;
                try { var value = (Point?)Input(); }
                catch (InvalidCastException) { caught = true; }
                return new { absent = optional == null, caught, calls };
            }
        }
        """);
}
