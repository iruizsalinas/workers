namespace Workers.Compiler.Tests;

public sealed class DictionaryInitializerCaptureRegressionTests
{
    [Theory]
    [InlineData("[key] = Input()")]
    [InlineData("{ key, Input() }")]
    public void DictionaryInitializersCaptureValueTypeKeysBeforeEvaluatingValues(string initializer) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static (int, int) key = (1, 2);
            private static int Input() { key.Item1 = 9; return 7; }
            public static object Run()
            {
                var values = new Dictionary<(int, int), int> { {{initializer}} };
                var found = values.TryGetValue((1, 2), out var stored);
                return new { found, stored, count = values.Count, changed = key.Item1 };
            }
        }
        """);

    [Theory]
    [InlineData("[new Key(1)] = point")]
    [InlineData("{ new Key(1), point }")]
    public void DictionaryInitializersCopyValueArgumentsBeforeHashingKeys(string initializer) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public sealed record Key(int Id)
        {
            public override int GetHashCode() { Test.Change(); return Id; }
        }
        public static class Test
        {
            private static Point point = new Point(1, 2);
            public static void Change() { point.X = 9; }
            public static object Run()
            {
                var values = new Dictionary<Key, Point> { {{initializer}} };
                return new { stored = values[new Key(1)].X, changed = point.X };
            }
        }
        """);
}
