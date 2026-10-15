namespace Workers.Compiler.Tests;

public sealed class NullableEnumRegressionTests
{
    [Fact]
    public void LiftedEnumArithmeticPropagatesNullAndPreservesTheUnderlyingWidth() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                Small? missing = null;
                Small? maximum = Small.Maximum;
                Small? minimum = Small.Minimum;
                byte? one = 1;
                Signed? top = Signed.Maximum;
                int? increment = 1;
                return new { missing = missing + one, wrapped = maximum + one,
                    previous = minimum - one, difference = minimum - maximum,
                    signed = top + increment, equal = missing == (Small?)null };
            }
        }
        public enum Small : byte { Minimum = 0, Maximum = 255 }
        public enum Signed : int { Maximum = int.MaxValue }
        """);

    [Fact]
    public void LiftedEnumBitwiseOperatorsPropagateNullAndPreserveHighBits() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                Flags? missing = null;
                Flags? high = Flags.High;
                Flags? low = Flags.Low;
                return new { missing = missing | high, combined = high | low,
                    intersected = high & low, toggled = high ^ low };
            }
        }
        public enum Flags : uint { Low = 1, High = 0x80000000u }
        """);
}
