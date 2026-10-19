namespace Workers.Compiler.Tests;

public sealed class RecordStructCastRegressionTests
{
    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void ExplicitNullableRecordCastsRejectMissingValues(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                Point? missing = null;
                var caught = false;
                try { var value = (Point)missing; }
                catch (System.InvalidOperationException) { caught = true; }
                Point? present = new Point(1, 2);
                var converted = (Point)present;
                return new { caught, x = converted.X, y = converted.Y };
            }
        }
        """);

    [Fact]
    public void ExplicitNullableTupleCastsRejectMissingValuesAndCopyPresentValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                (int X, int Y)? missing = null;
                var caught = false;
                try { var value = ((int X, int Y))missing; }
                catch (System.InvalidOperationException) { caught = true; }
                (int X, int Y)? present = (1, 2);
                var converted = ((int X, int Y))present;
                converted.X = 9;
                return new { caught, original = present.Value.X, converted = converted.X };
            }
        }
        """);

    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void UnboxingNullCannotProduceARecordStructValue(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            private static int calls;
            private static object Missing() { calls++; return null; }
            public static object Run()
            {
                var caught = false;
                var unexpected = false;
                try { var value = (Point)Missing(); }
                catch (System.NullReferenceException) { caught = true; }
                catch (System.Exception) { unexpected = true; }
                object present = new Point(1, 2);
                var converted = (Point)present;
                return new { caught, unexpected, calls, x = converted.X, y = converted.Y };
            }
        }
        """);
}
