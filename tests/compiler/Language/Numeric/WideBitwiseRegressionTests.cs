namespace Workers.Compiler.Tests;

public sealed class WideBitwiseRegressionTests
{
    [Fact]
    public void WideBitwiseOperationsDoNotTruncateToThirtyTwoBits() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                long high = 4294967296L, all = 4294967297L, negative = -1;
                ulong unsignedHigh = 4294967296UL, unsignedAll = 4294967297UL;
                var flag = Flags.High;
                return new {
                    and = high & all, or = high | 1L, xor = all ^ 1L,
                    signed = negative & high, inverted = negative ^ high,
                    unsignedAnd = unsignedHigh & unsignedAll,
                    unsignedOr = unsignedHigh | 1UL, unsignedXor = unsignedAll ^ 1UL,
                    flags = flag | Flags.Low, equal = (flag | Flags.Low) == Flags.Both
                };
            }
        }
        public enum Flags : long { Low = 1, High = 4294967296L, Both = 4294967297L }
        """);
}
