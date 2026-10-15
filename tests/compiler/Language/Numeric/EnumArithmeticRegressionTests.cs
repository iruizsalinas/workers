namespace Workers.Compiler.Tests;

public sealed class EnumArithmeticRegressionTests
{
    [Fact]
    public void EnumArithmeticUsesTheUnderlyingIntegerWidth() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                byte one = 1;
                var maximum = Small.Maximum;
                var minimum = Small.Minimum;
                var top = Signed.Maximum;
                var bottom = Signed.Minimum;
                var unsigned = Unsigned.Maximum;
                uint unsignedOne = 1;
                return new {
                    incremented = maximum + one, decremented = minimum - one,
                    difference = minimum - maximum, signedNext = top + one,
                    signedPrevious = bottom - one, signedDifference = bottom - top,
                    unsignedNext = unsigned + unsignedOne,
                    equal = maximum + one == Small.Minimum
                };
            }
        }
        public enum Small : byte { Minimum = 0, Maximum = 255 }
        public enum Signed : int { Minimum = int.MinValue, Maximum = int.MaxValue }
        public enum Unsigned : uint { Maximum = uint.MaxValue }
        """);
}
