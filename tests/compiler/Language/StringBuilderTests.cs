namespace Workers.Compiler.Tests;

public sealed class StringBuilderTests
{
    [Fact]
    public void EmitsDemandLoadedStringBuilderOperations()
    {
        var module = Compile("""
            using System.Text;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    string? missing = null;
                    var builder = new StringBuilder("start");
                    builder.Append(':').Append(missing).Append("😀");
                    builder.AppendLine();
                    builder.AppendLine("end");
                    var beforeClear = new { text = builder.ToString(), builder.Length };
                    builder.Clear().Append("reset");
                    return Response.Json(new { beforeClear, text = builder.ToString(), builder.Length });
                }
            }
            """);

        Assert.Contains("stringBuilder(\"start\")", module);
        Assert.Contains("stringBuilderAppend", module);
        Assert.Contains("stringBuilderClear", module);
        Assert.Contains("stringBuilderText", module);
        Assert.Contains("length: builder.length", module);
    }

    [Fact]
    public void DoesNotShipStringBuilderSupportWhenUnused()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Text("ok");
            }
            """);

        Assert.DoesNotContain("stringBuilder", module);
    }

    [Theory]
    [InlineData("new StringBuilder(10)")]
    [InlineData("new StringBuilder().Append(42)")]
    [InlineData("new StringBuilder().Insert(0, \"x\")")]
    public void RejectsUnsupportedStringBuilderShapes(string expression)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Text;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Text({{expression}}.ToString());
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

    [Fact]
    public void RejectsLengthMutationInsteadOfCorruptingBuilderState()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using System.Text;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var builder = new StringBuilder("value");
                    builder.Length = 0;
                    return Response.Text(builder.ToString());
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }
}
