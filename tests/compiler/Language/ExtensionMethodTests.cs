namespace Workers.Compiler.Tests;

public sealed class ExtensionMethodTests
{
    [Fact]
    public void SpecializesInferredTypeArgumentsAndConditionalReceivers() => DifferentialRunner.Equal("""
        public static class Extensions
        {
            public static T DefaultValue<T>(this T value) => default(T);
            public static string Append(this string value, int number) => value + number;
        }
        public static class Test
        {
            private static int calls;
            private static int Next() { calls++; return calls; }
            public static object Run()
            {
                string missing = null;
                System.Func<string> empty = "x".DefaultValue;
                return new { a = 7.DefaultValue(), b = "x".DefaultValue(), c = empty(),
                    d = missing?.Append(Next()), e = "x"?.Append(Next()), calls };
            }
        }
        """);
    [Fact]
    public void PreservesReducedStaticNamedOptionalAndNullCalls() => DifferentialRunner.Equal("""
        public static class Extensions
        {
            public static string Format(this string value, int first = 1, int second = 2) =>
                (value ?? "null") + first + second;
        }
        public static class Test
        {
            private static string log = "";
            private static string Receiver() { log += "r"; return "v"; }
            private static int Arg(int value) { log += value; return value; }
            public static object Run()
            {
                var a = Receiver().Format(second: Arg(4), first: Arg(3));
                var b = "b".Format();
                var c = Extensions.Format(null, second: 5);
                string missing = null;
                var d = missing.Format();
                return new { a, b, c, d, log };
            }
        }
        """);

    [Fact]
    public void CapturesExtensionMethodGroupReceiverOnceIncludingNull() => DifferentialRunner.Equal("""
        public static class Extensions
        {
            public static string Format(this string value, int count) => (value ?? "null") + count;
        }
        public static class Test
        {
            private static int calls;
            private static string Receiver() { calls++; return "v"; }
            public static object Run()
            {
                System.Func<int, string> format = Receiver().Format;
                string missing = null;
                System.Func<int, string> other = missing.Format;
                return new { a = format(1), b = format(2), c = other(3), calls };
            }
        }
        """);
}
