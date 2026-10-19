namespace Workers.Compiler.Tests;

public sealed class RecordStructMemberStorageRegressionTests
{
    [Theory]
    [InlineData("point.X = await Change();")]
    [InlineData("point.X += await Change();")]
    public void AwaitedMemberWritesPreserveContainingVariableStorage(string assignment) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static async Task<object> Run()
            {
                var point = new Point(1, 2);
                async Task<int> Change()
                {
                    await Task.FromResult(0);
                    point = new Point(9, 8);
                    return 7;
                }
                {{assignment}}
                return new { x = point.X, y = point.Y };
            }
        }
        """);

    [Theory]
    [InlineData("point.X = Change();")]
    [InlineData("point.X += Change();")]
    [InlineData("(point.X, _) = (Change(), 0);")]
    public void MemberWritesFollowContainingVariableStorage(string assignment) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                int Change() { point = new Point(9, 8); return 7; }
                {{assignment}}
                return new { x = point.X, y = point.Y };
            }
        }
        """);

    [Theory]
    [InlineData("values[0].X = Change();")]
    [InlineData("pair.Item1.X += Change();")]
    [InlineData("(holder.Value.X, _) = (Change(), 0);")]
    public void MemberWritesPreserveArrayTupleAndFieldStorage(string assignment) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public sealed class Holder { public Point Value = new Point(1, 2); }
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { new Point(1, 2) };
                var pair = (new Point(1, 2), 0);
                var holder = new Holder();
                int Change()
                {
                    values[0] = new Point(9, 8);
                    pair = (new Point(9, 8), 0);
                    holder.Value = new Point(9, 8);
                    return 7;
                }
                {{assignment}}
                return new { element = values[0].X, tuple = pair.Item1.X, field = holder.Value.X };
            }
        }
        """);
}
