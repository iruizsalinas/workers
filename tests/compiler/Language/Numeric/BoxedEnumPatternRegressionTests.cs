namespace Workers.Compiler.Tests;

public sealed class BoxedEnumPatternRegressionTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("byte")]
    [InlineData("uint")]
    public void BoxedEnumConstantsMatchTheirOwnEnumPattern(string underlying) => DifferentialRunner.Equal($$"""
        public enum Status : {{underlying}} { Ready = 3, Done = 4 }
        public enum Other { Ready = 3 }
        public static class Test
        {
            private static bool Matches<T>(T value) => value is Status.Ready;
            public static object Run()
            {
                object value = Status.Ready;
                object number = 3;
                object missing = null;
                object nullable = (Status?)Status.Ready;
                var statement = 0;
                switch (value) { case Status.Ready: statement = 1; break; }
                return new { ready = value is Status.Ready, done = value is Status.Done,
                    other = value is Other.Ready, numeric = value is 3, raw = number is Status.Ready,
                    missing = missing is Status.Ready, expression = value switch { Status.Ready => 1, _ => 0 }, statement,
                    nullable = nullable is Status.Ready, generic = Matches(value), typed = Matches(Status.Ready) };
            }
        }
        """);
}
