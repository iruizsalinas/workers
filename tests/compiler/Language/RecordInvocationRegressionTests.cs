namespace Workers.Compiler.Tests;

public sealed class RecordInvocationRegressionTests
{
    [Fact]
    public void RecordInstanceMethodsThrowForNullReceivers() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int arguments;
            private static Key Other() { arguments++; return new Key(1); }
            public static object Run()
            {
                Key missing = null;
                Empty empty = null;
                var caught = 0;
                try { var value = missing.Equals(Other()); }
                catch (System.Exception) { caught++; }
                try { var value = missing.Equals((Key)null); }
                catch (System.Exception) { caught++; }
                try { var value = empty.ToString(); }
                catch (System.Exception) { caught++; }
                Key otherMissing = null;
                return new { caught, arguments, equal = missing == otherMissing, text = "prefix" + empty };
            }
        }
        public sealed record Key(int Value);
        public sealed record Empty;
        """);
}
