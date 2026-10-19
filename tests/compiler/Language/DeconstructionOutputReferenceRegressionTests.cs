namespace Workers.Compiler.Tests;

public sealed class DeconstructionOutputReferenceRegressionTests
{
    [Theory]
    [InlineData("var (x, y) = new Pair(); return new { x, y };")]
    [InlineData("return new Pair() is (6, 3);")]
    [InlineData("return new Pair() switch { (6, 3) => 1, _ => 0 };")]
    public void DeconstructorsCanReadTheirAssignedOutParameters(string body) => DifferentialRunner.Equal($$"""
        public sealed class Pair
        {
            public void Deconstruct(out int x, out int y)
            {
                x = 2;
                y = x + 1;
                x += 4;
            }
        }
        public static class Test
        {
            public static object Run() { {{body}} }
        }
        """);
}
