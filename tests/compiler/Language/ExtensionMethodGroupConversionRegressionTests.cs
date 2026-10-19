namespace Workers.Compiler.Tests;

public sealed class ExtensionMethodGroupConversionRegressionTests
{
    [Theory]
    [InlineData("public static object Capture(this IEquatable<Point> value) => value;")]
    [InlineData("extension(IEquatable<Point> value) { public object Capture() => value; }")]
    public void InterfaceExtensionMethodGroupsBoxReadonlyValueReceivers(string extension) => DifferentialRunner.Equal($$"""
        public readonly record struct Point(int X, int Y);
        public static class Extensions
        {
            {{extension}}
        }
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                Func<object> capture = point.Capture;
                Func<object> other = point.Capture;
                return new { same = capture == other, sameBox = capture() == other(), text = capture().ToString() };
            }
        }
        """);

    [Theory]
    [InlineData("public static object Capture(this object value) => value;")]
    [InlineData("extension(object value) { public object Capture() => value; }")]
    public void ObjectExtensionMethodGroupsBoxTheirReceiverAtCreation(string extension) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Extensions
        {
            {{extension}}
        }
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                Func<object> capture = point.Capture;
                Func<object> other = point.Capture;
                point.X = 9;
                var boxed = capture();
                var snapshot = (Point)boxed;
                return new { copiedX = snapshot.X, originalX = point.X, text = boxed.ToString(), same = capture == other };
            }
        }
        """);
}
