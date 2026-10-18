namespace Workers.Compiler.Tests;

public sealed class GenericTypePatternRegressionTests
{
    [Fact]
    public void SpecializedTypePatternsRecognizeTheirOwnValueType() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static bool IsInt<T>(T value) => value is int;
            private static bool CaptureInt<T>(T value) { return value is int number && number == 7; }
            public static object Run() => new { matches = IsInt(7), captured = CaptureInt(7) };
        }
        """);

    [Fact]
    public void SpecializedNullablePatternsCheckPresenceBeforeCapturing() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int Read<T>(T value) { return value is int number ? number : -1; }
            public static object Run() => new { present = Read<int?>(7), absent = Read<int?>(null) };
        }
        """);

    [Fact]
    public void GenericIdentityPatternsRecognizeSpecializedArraysAndEnums() => DifferentialRunner.Equal("""
        public enum Status { Ready = 3 }
        public static class Test
        {
            private static bool Matches<T>(T value) => value is T;
            public static object Run() => new
            {
                array = Matches(new[] { 1, 2 }), missing = Matches<int[]>(null),
                status = Matches(Status.Ready), number = Matches(7)
            };
        }
        """);
}
