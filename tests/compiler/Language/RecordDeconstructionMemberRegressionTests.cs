namespace Workers.Compiler.Tests;

public sealed class RecordDeconstructionMemberRegressionTests
{
    [Theory]
    [InlineData("var (first, second) = row;")]
    [InlineData("row.Deconstruct(out var first, out var second);")]
    public void RecordDeconstructionSupportsExplicitPositionalFields(string deconstruction) => DifferentialRunner.Equal($$"""
        public sealed record Row(int First, int Second)
        {
            public int First = First;
        }
        public static class Test
        {
            public static object Run()
            {
                var row = new Row(1, 2);
                {{deconstruction}}
                return new { first, second };
            }
        }
        """);

    [Theory]
    [InlineData("var (first, second) = row;")]
    [InlineData("row.Deconstruct(out var first, out var second);")]
    public void RecordDeconstructionCopiesTupleOutputsBeforeLaterGettersRun(string deconstruction) => DifferentialRunner.Equal($$"""
        public sealed record Row((int, int) First, int Second)
        {
            public (int, int) First = First;
            public int Second { get { First.Item1 = 9; return 3; } }
        }
        public static class Test
        {
            public static object Run()
            {
                var row = new Row((1, 2), 3);
                {{deconstruction}}
                return new { first = first.Item1, second, current = row.First.Item1 };
            }
        }
        """);

    [Fact]
    public void RecordDeconstructionSupportsInheritedPositionalFields() => DifferentialRunner.Equal("""
        public record Base(int First) { public int First = First; }
        public sealed record Row(int First, int Second) : Base(First);
        public static class Test
        {
            public static object Run()
            {
                var (first, second) = new Row(1, 2);
                return new { first, second };
            }
        }
        """);
}
