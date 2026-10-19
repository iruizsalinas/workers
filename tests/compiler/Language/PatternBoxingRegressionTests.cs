namespace Workers.Compiler.Tests;

public sealed class PatternBoxingRegressionTests
{
    [Fact]
    public void ObjectDeclarationPatternsCaptureValueInputs() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                if (point is object boxed)
                {
                    point.X = 9;
                    var snapshot = (Point)boxed;
                    return new { stored = snapshot.X, changed = point.X };
                }
                throw new Exception();
            }
        }
        """);

    [Fact]
    public void ObjectDeclarationPatternsBoxPresentNullableInputs() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                Point? present = new Point(1, 2);
                Point? absent = null;
                if (present is object boxed)
                    return new { missingMatches = absent is object missing, text = boxed.ToString() };
                throw new Exception();
            }
        }
        """);

    [Theory]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void ObjectDeclarationPatternsBoxValueInputs(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                if (point is object first && point is object second)
                    return new { same = first == second, text = first.ToString() };
                throw new Exception();
            }
        }
        """);

    [Fact]
    public void ObjectDeclarationPatternsKeepEnumTypeAndText() => DifferentialRunner.Equal("""
        public enum Status { Ready = 3 }
        public static class Test
        {
            public static object Run()
            {
                var status = Status.Ready;
                if (status is object boxed)
                    return new { matches = boxed is Status.Ready, text = boxed.ToString() };
                throw new Exception();
            }
        }
        """);
}
