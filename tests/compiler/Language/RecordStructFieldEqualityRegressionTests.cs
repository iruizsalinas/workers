namespace Workers.Compiler.Tests;

public sealed class RecordStructFieldEqualityRegressionTests
{
    [Theory]
    [InlineData("record struct", "value == value")]
    [InlineData("readonly record struct", "value == value")]
    [InlineData("record struct", "value.Equals(value)")]
    [InlineData("readonly record struct", "value.Equals(value)")]
    [InlineData("sealed record", "value.Equals(value)")]
    public void StructEqualityInvokesFieldEqualityEvenForTheSameValue(string declaration, string comparison) => DifferentialRunner.Equal($$"""
        public sealed class Field : IEquatable<Field>
        {
            public static int Calls;
            public bool Equals(Field? other) { Calls++; return other != null; }
        }
        public {{declaration}} Box(Field Value);
        public static class Test
        {
            public static object Run()
            {
                var value = new Box(new Field());
                var equal = {{comparison}};
                return new { equal, calls = Field.Calls };
            }
        }
        """);

    [Fact]
    public void NullableStructEqualityKeepsNullCasesAndFieldComparisons() => DifferentialRunner.Equal("""
        public sealed class Field : IEquatable<Field>
        {
            public static int Calls;
            public bool Equals(Field? other) { Calls++; return other != null; }
        }
        public readonly record struct Box(Field Value);
        public static class Test
        {
            public static object Run()
            {
                Box? absent = null;
                Box? present = new Box(new Field());
                var one = absent == present;
                var both = absent == (Box?)null;
                var same = present == present;
                return new { one, both, same, calls = Field.Calls };
            }
        }
        """);
}
