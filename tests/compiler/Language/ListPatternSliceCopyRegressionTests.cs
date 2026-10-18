namespace Workers.Compiler.Tests;

public sealed class ListPatternSliceCopyRegressionTests
{
    [Theory]
    [InlineData("[.. var slice]", "0")]
    [InlineData("[_, .. var slice]", "1")]
    [InlineData("[.. var slice, _]", "0")]
    public void ArrayPatternSlicesCopyTupleElements(string pattern, string sourceIndex) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { (1, 2), (3, 4) };
                if (values is {{pattern}})
                {
                    values[{{sourceIndex}}].Item1 = 9;
                    slice[0].Item2 = 8;
                    return new { copied = slice[0].Item1, current = values[{{sourceIndex}}].Item1,
                        originalSecond = values[{{sourceIndex}}].Item2, slicedSecond = slice[0].Item2 };
                }
                return null;
            }
        }
        """);
}
