namespace Workers.Compiler.Tests;

public sealed class CollectionInterfaceBoxingRegressionTests
{
    [Theory]
    [InlineData("IEquatable<Point>[] values = [.. source];")]
    [InlineData("var values = new List<IEquatable<Point>>(); foreach (IEquatable<Point> value in source) values.Add(value);")]
    public void CollectionElementInterfaceConversionsBoxValueTypes(string construction) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var source = new[] { new Point(1, 2) };
                {{construction}}
                source[0].X = 9;
                var stored = (Point)(object)values[0];
                return new { stored = stored.X, changed = source[0].X, text = values[0].ToString() };
            }
        }
        """);

    [Theory]
    [InlineData("IEquatable<Point>?[] values = [.. source];")]
    [InlineData("var values = new List<IEquatable<Point>?>(); foreach (IEquatable<Point>? value in source) values.Add(value);")]
    public void NullableCollectionElementsBoxPresentValuesAndPreserveNull(string construction) => DifferentialRunner.Equal($$"""
        public readonly record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                Point?[] source = [null, new Point(1, 2)];
                {{construction}}
                return new { absent = values[0] == null, text = values[1]!.ToString() };
            }
        }
        """);

    [Fact]
    public void AsyncForeachBoxesInterfaceIterationVariables() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            private static async IAsyncEnumerable<Point> Source()
            {
                await Task.FromResult(0);
                yield return new Point(1, 2);
            }
            public static async Task<object> Run()
            {
                var values = new List<IEquatable<Point>>();
                await foreach (IEquatable<Point> value in Source()) values.Add(value);
                return values[0].ToString()!;
            }
        }
        """);
}
