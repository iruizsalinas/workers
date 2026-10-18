namespace Workers.Compiler.Tests;

public sealed class RelationalPatternCoercionRegressionTests
{
    [Fact]
    public void NumericRelationalPatternsDoNotCoerceNonnumericObjects() => DifferentialRunner.Equal("""
        public enum Status { Ready = 3 }
        public static class Test
        {
            private static object Match(object value) => new
            {
                greater = value is > 2,
                positive = value is > 0,
                bounded = value is >= 1 and <= 4,
                selected = value switch { > 2 => 1, _ => 0 }
            };
            private static bool Generic<T>(T value) => value is > 2;
            public static object Run() => new
            {
                values = new[] { Match(3), Match("3"), Match(true), Match(Status.Ready), Match(null), Match(3.5) },
                generic = Generic<object>("3"), typed = Generic(3)
            };
        }
        """);
}
