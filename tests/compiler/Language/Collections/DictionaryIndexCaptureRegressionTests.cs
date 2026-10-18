namespace Workers.Compiler.Tests;

public sealed class DictionaryIndexCaptureRegressionTests
{
    [Theory]
    [InlineData("values[key] = Input();")]
    [InlineData("values[key] += Input();")]
    [InlineData("(values[key], var ignored) = (Input(), 0);")]
    public void DictionaryAssignmentsCaptureTupleKeysBeforeEvaluatingTheirValues(string assignment) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static (int, int) key = (1, 2);
            private static int Input() { key.Item1 = 9; return 7; }
            public static object Run()
            {
                var values = new Dictionary<(int, int), int> { [(1, 2)] = 3 };
                {{assignment}}
                return new { stored = values[(1, 2)], count = values.Count, first = key.Item1 };
            }
        }
        """);
}
