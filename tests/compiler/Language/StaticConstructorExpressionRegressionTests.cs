namespace Workers.Compiler.Tests;

public sealed class StaticConstructorExpressionRegressionTests
{
    [Theory]
    [InlineData("Capture(out var ignored)")]
    [InlineData("Value = int.TryParse(\"7\", out var value) ? value : 0")]
    public void ExpressionBodiedStaticConstructorsCanDeclareOutLocals(string expression) => DifferentialRunner.Equal($$"""
        public static class Config
        {
            public static int Value;
            static Config() => {{expression}};
            private static void Capture(out int value) { value = 7; Value = value; }
        }
        public static class Test
        {
            public static object Run() => new { value = Config.Value };
        }
        """);
}
