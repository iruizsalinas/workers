namespace Workers.Compiler.Tests;

public sealed class ExtensionInvocationRegressionTests
{
    [Fact]
    public void NamedCollectionExtensionsCaptureTheReceiverFirst()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private static Dictionary<string, int> values = new() { ["a"] = 1 };
                private static string Key() { values = new() { ["b"] = 2 }; return "a"; }
                public static object Run() => values.GetValueOrDefault(key: Key(), defaultValue: -1);
            }
            """);
    }

    [Fact]
    public void ConditionalCollectionExtensionsUseTheBoundReceiver()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    Dictionary<string, int> missing = null;
                    var present = new Dictionary<string, int> { ["a"] = 1 };
                    return new { skipped = missing?.GetValueOrDefault("a"), found = present?.GetValueOrDefault("a") };
                }
            }
            """);
    }
}
