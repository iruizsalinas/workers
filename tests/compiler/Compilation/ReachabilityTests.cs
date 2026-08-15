namespace Workers.Compiler.Tests;

public sealed class ReachabilityTests
{
    [Fact]
    public void EmitsOnlyTransitivelyReachableStaticHelpersAcrossSourceFiles()
    {
        var module = Compile(
            """
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text(Formatting.Render(3));
            }
            """,
            """
            public static class Formatting
            {
                public static string Render(int value) => Prefix() + CountDown(value);
                public static string Render(string value) => "unused-overload";
                public static string NeverCalled() => "dead-code";
                private static string Prefix() => "value=";
                private static string CountDown(int value) => value == 0 ? "0" : CountDown(value - 1);
            }
            """);

        Assert.Contains("function $workers$cs$Formatting$Render$0", module);
        Assert.Contains("function $workers$cs$Formatting$Prefix$1", module);
        Assert.Contains("function $workers$cs$Formatting$CountDown$2", module);
        Assert.Contains("$workers$cs$Formatting$CountDown$2((value - 1) | 0)", module);
        Assert.DoesNotContain("unused-overload", module);
        Assert.DoesNotContain("dead-code", module);
    }

    [Fact]
    public void AssignsDifferentNamesToReachableOverloads()
    {
        var module = Compile("""
            using Workers;
            public static class Helper
            {
                public static string Value(int value) => "number";
                public static string Value(string value) => "text";
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text(Helper.Value(1) + Helper.Value("x"));
            }
            """);

        Assert.Contains("function $workers$cs$Helper$Value$0", module);
        Assert.Contains("function $workers$cs$Helper$Value$1", module);
        Assert.Contains("($workers$cs$Helper$Value$0(1) ?? \"\") + ($workers$cs$Helper$Value$1(\"x\") ?? \"\")", module);
    }

}
