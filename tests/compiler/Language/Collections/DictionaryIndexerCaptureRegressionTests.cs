namespace Workers.Compiler.Tests;

public sealed class DictionaryIndexerCaptureRegressionTests
{
    [Theory]
    [InlineData("values[key]")]
    [InlineData("Read(values)")]
    public void DictionaryIndexerCopiesValueTypeKeysBeforeHashing(string lookup) => DifferentialRunner.Equal($$"""
        public sealed record Key(int Id)
        {
            public override int GetHashCode() { Test.Change(); return Id; }
        }
        public static class Test
        {
            private static (Key, int) key = (new Key(1), 2);
            private static bool change;
            public static void Change() { if (change) key.Item2 = 9; }
            private static TValue Read<TValue>(Dictionary<(Key, int), TValue> values)
                => values[key];
            public static object Run()
            {
                var values = new Dictionary<(Key, int), int> { [key] = 7 };
                change = true;
                var stored = {{lookup}};
                return new { stored, changed = key.Item2 };
            }
        }
        """);
}
