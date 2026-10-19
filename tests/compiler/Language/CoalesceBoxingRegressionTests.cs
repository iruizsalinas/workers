namespace Workers.Compiler.Tests;

public sealed class CoalesceBoxingRegressionTests
{
    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void CoalescingANullableRecordStructToInterfaceCreatesSeparateBoxes(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            private static int calls;
            private static IEquatable<Point> Fallback() { calls++; return new Point(3, 4); }
            public static object Run()
            {
                Point? present = new Point(1, 2);
                Point? absent = null;
                IEquatable<Point> first = present ?? Fallback();
                IEquatable<Point> second = present ?? Fallback();
                IEquatable<Point> missing = absent ?? Fallback();
                return new { same = first == second, missing = ((Point)(object)missing).X, calls };
            }
        }
        """);

    [Fact]
    public void CoalescingANullableEnumToObjectPreservesItsType() => DifferentialRunner.Equal("""
        public enum Status { Ready = 3 }
        public static class Test
        {
            private static int calls;
            private static object Fallback() { calls++; return "missing"; }
            public static object Run()
            {
                Status? present = Status.Ready;
                Status? absent = null;
                object boxed = present ?? Fallback();
                object missing = absent ?? Fallback();
                return new { matches = boxed is Status.Ready, numeric = boxed is 3, text = boxed.ToString(), missing, calls };
            }
        }
        """);

    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void CoalescingANullableRecordStructToObjectCreatesSeparateBoxes(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                Point? point = new Point(1, 2);
                object first = point ?? (object)"missing";
                object second = point ?? (object)"missing";
                return new { same = first == second, text = first.ToString() };
            }
        }
        """);
}
