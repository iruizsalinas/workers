namespace Workers.Compiler.Tests;

public sealed class StaticInitializationRegressionTests
{
    [Fact]
    public void StaticFieldsHaveDefaultsBeforeAnyInitializerRuns() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int first = second + 1;
            private static bool wasNull = text is null;
            private static bool wasFalse = flag == false;
            private static bool wasZero = other == 0;
            private static bool wasDefaultChar = letter == '\0';
            private static int second = 2;
            private static string text = "ready";
            private static bool flag = true;
            private static int other { get; } = 7;
            private static char letter = 'X';
            public static object Run() => new { first, wasNull, wasFalse, wasZero, wasDefaultChar, second, text, flag, other, letter };
        }
        """);

    [Fact]
    public void StaticConstructorsRunBeforeCallingStaticMethods() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            public static void Mark(string value) { trace += value; }
            public static object Run()
            {
                var first = Config.Read();
                var second = Config.Read();
                return new { first, second, trace };
            }
        }
        public static class Config
        {
            static Config() { Test.Mark("initialized;"); }
            public static int Read() { Test.Mark("read;"); return 42; }
        }
        """);

    [Fact]
    public void FailedStaticInitializationKeepsFailingOnLaterAccess() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int failures;
            public static int Fail() { failures++; throw new System.Exception("failure"); }
            public static object Run()
            {
                var caught = 0;
                var message = "";
                try { var value = Config.Value; }
                catch (System.Exception error) { caught++; message = error.Message; }
                try { var value = Config.Value; }
                catch (System.Exception) { caught++; }
                return new { caught, failures, message };
            }
        }
        public static class Config
        {
            public static int Value = Test.Fail();
        }
        """);

    [Fact]
    public void StaticConstructorReturnDoesNotReturnFromTheFieldAccessor() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run() => new { value = Config.Value };
        }
        public static class Config
        {
            public static int Value = 7;
            static Config() { if (Value == 7) return; Value = 9; }
        }
        """);

    [Fact]
    public void AsyncCallsReportTypeInitializationFailuresSynchronously() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var caught = false;
                try { var pending = Config.ReadAsync(); }
                catch (System.Exception) { caught = true; }
                return new { caught };
            }
        }
        public static class Config
        {
            static Config() { throw new System.Exception("failure"); }
            public static async Task<int> ReadAsync() => await Task.FromResult(42);
        }
        """);

    [Fact]
    public void StaticGettersAndIteratorFactoriesTriggerExplicitInitialization() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            public static void Mark(string value) { trace += value; }
            public static object Run()
            {
                var value = Settings.Value;
                var sequence = Values.Read();
                var beforeEnumeration = trace;
                var first = sequence.First();
                return new { value, beforeEnumeration, first, trace };
            }
        }
        public static class Settings
        {
            static Settings() { Test.Mark("settings;"); }
            public static int Value => 7;
        }
        public static class Values
        {
            static Values() { Test.Mark("values;"); }
            public static IEnumerable<int> Read() { Test.Mark("enumeration;"); yield return 2; }
        }
        """);

    [Fact]
    public void StaticDefaultsPreserveSupportedStructFields() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static bool initiallyEmpty = buffer.Length == 0;
            private static bool initiallyDefault = pair.Key is null && pair.Value == 0;
            private static ReadOnlyMemory<byte> buffer = new byte[] { 65, 66 };
            private static KeyValuePair<string, int> pair = new("key", 7);
            private static ValueTask<int> pending = ValueTask.FromResult(9);
            public static async Task<object> Run() => new {
                initiallyEmpty, initiallyDefault, length = buffer.Length,
                key = pair.Key, value = pair.Value, awaited = await pending
            };
        }
        """);
}
