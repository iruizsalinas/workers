namespace Workers.Compiler.Tests;

public sealed class CharacterMutationRegressionTests
{
    [Theory]
    [InlineData("'\\uFFFF'")]
    [InlineData("'\\0'")]
    [InlineData("'\\uD800'")]
    public void CharacterMutationUsesUtf16CodeUnits(string initial) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                char value = {{initial}};
                var before = value++;
                var after = value;
                var restored = --value;
                return new { before = (int)before, after = (int)after, restored = (int)restored };
            }
        }
        """);

    [Fact]
    public void CharacterPropertyMutationCallsEachAccessorOnce() => DifferentialRunner.Equal("""
        public sealed class Holder
        {
            private char stored = 'A';
            public int Reads;
            public int Writes;
            public char Value { get { Reads++; return stored; } set { Writes++; stored = value; } }
        }
        public static class Test
        {
            public static object Run()
            {
                var holder = new Holder();
                var value = ++holder.Value;
                return new { code = (int)value, reads = holder.Reads, writes = holder.Writes };
            }
        }
        """);
}
