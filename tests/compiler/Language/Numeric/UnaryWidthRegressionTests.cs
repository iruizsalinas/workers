namespace Workers.Compiler.Tests;

public sealed class UnaryWidthRegressionTests
{
    [Theory]
    [InlineData("byte", "255")]
    [InlineData("sbyte", "127")]
    [InlineData("short", "32767")]
    [InlineData("ushort", "65535")]
    public void NarrowIntegerIncrementWrapsAndPreservesPostfixValues(string type, string maximum) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                {{type}} value = {{maximum}};
                var previous = value++;
                var wrapped = value;
                var back = --value;
                return new { previous, wrapped, back };
            }
        }
        """);

    [Theory]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    public void EnumComplementUsesTheUnderlyingWidth(string underlying) => DifferentialRunner.Equal($$"""
        public enum Bits : {{underlying}} { Low = 1 }
        public static class Test
        {
            public static object Run()
            {
                var value = Bits.Low;
                return (double)~value;
            }
        }
        """);

    [Theory]
    [InlineData("byte", "255")]
    [InlineData("sbyte", "127")]
    [InlineData("short", "32767")]
    [InlineData("ushort", "65535")]
    [InlineData("int", "2147483647")]
    [InlineData("uint", "4294967295")]
    public void EnumIncrementWrapsAndPreservesPostfixValues(string underlying, string maximum) => DifferentialRunner.Equal($$"""
        public enum Bits : {{underlying}} { Maximum = {{maximum}} }
        public static class Test
        {
            public static object Run()
            {
                var value = Bits.Maximum;
                var previous = value++;
                var wrapped = value;
                var back = --value;
                return new { previous = (double)previous, wrapped = (double)wrapped, back = (double)back };
            }
        }
        """);
}
