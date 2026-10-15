namespace Workers.Compiler.Tests;

public sealed class ExpressionRegressionTests
{
    [Fact]
    public void UnsignedBitwiseResultsKeepTheirHighBit() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run() => Calculate(0x80000000u, 0xffffffffu);
            private static object Calculate(uint high, uint all) => new
            {
                and = high & all, or = high | 1u, xor = all ^ 1u,
                comparison = (high | 1u) > 0u, quotient = (high & all) / 2u
            };
        }
        """);

    [Fact]
    public void UnsignedEnumBitwiseOperatorsPreserveTheirUnderlyingType() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var high = Bits.High;
                var low = Bits.Low;
                var result = high | low;
                return new { result, equal = result == Bits.Both, intersected = result & high, toggled = result ^ low };
            }
        }
        public enum Bits : uint { Low = 1, High = 0x80000000u, Both = 0x80000001u }
        """);

    [Fact]
    public void CoalesceAppliesTheConversionToThePresentValue() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            public static object Run()
            {
                int? value = 16777217;
                int? missing = null;
                float present = value ?? Fallback();
                float absent = missing ?? Fallback();
                return new { present, absent, calls };
            }
            private static float Fallback() { calls++; return 3f; }
        }
        """);

    [Fact]
    public void CoalescePreservesBoxingWideningAndLazyAsyncFallbacks() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            public static async Task<object> Run()
            {
                int? value = 16777217;
                int? missing = null;
                object boxed = value ?? (object)"fallback";
                long wide = value ?? 0L;
                float? present = value ?? (float?)await Fallback();
                float absent = missing ?? await Fallback();
                char? letter = 'A';
                int code = letter ?? 0;
                return new { boxed, wide, present, absent, code, calls };
            }
            private static async Task<float> Fallback() { calls++; return await Task.FromResult(3f); }
        }
        """);

    [Fact]
    public void AwaitedThrowExpressionsRunAndPropagateTheirException() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            public static async Task<object> Run()
            {
                string message = "";
                string present = "kept";
                var skipped = present ?? throw await Error();
                try { string value = null; var result = value ?? throw await Error(); }
                catch (System.Exception error) { message = error.Message; }
                return new { message, skipped, calls };
            }
            private static async Task<System.Exception> Error()
            {
                calls++;
                await Task.FromResult(0);
                return new System.Exception("expected");
            }
        }
        """);

    [Fact]
    public void NarrowFloatingPointCastsWrapAfterInt32Conversion() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run() => Convert(300.75, -130.75, 65537.75);
            private static object Convert(double largeByte, double smallByte, double largeShort) => new
            {
                unsignedByte = unchecked((byte)largeByte), signedByte = unchecked((sbyte)smallByte),
                unsignedShort = unchecked((ushort)largeShort), signedShort = unchecked((short)largeShort),
                character = (int)unchecked((char)largeShort)
            };
        }
        """);

    [Fact]
    public void NarrowFloatingPointCastsHandleLargeAndNonFiniteValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                double[] values = [300.75, -130.75, 65537.75, -65537.75, 2147483648d,
                    -2147483649d, 4294967297d, 1e30, -1e30,
                    double.NaN, double.PositiveInfinity, double.NegativeInfinity];
                return values.Select(value => new {
                    unsignedByte = unchecked((byte)value), signedByte = unchecked((sbyte)value),
                    unsignedShort = unchecked((ushort)value), signedShort = unchecked((short)value),
                    character = (int)unchecked((char)value), singleByte = unchecked((byte)(float)value)
                }).ToArray();
            }
        }
        """);
}
