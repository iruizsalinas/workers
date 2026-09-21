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
                    builder.Append(':').Append(missing).Append("😀").Append(true).Append(42);
                    builder.AppendLine();
                    builder.AppendLine("end");
                    builder.Append('!', 2).Insert(0, "[").Replace("start", "begin").Remove(1, 1);
                    builder.AppendJoin(",", new List<string?> { "a", null, "b" });
                    builder.AppendJoin('-', "x", "y");
                    var beforeClear = new { text = builder.ToString(), builder.Length };
                    builder.Clear().Append("reset");
                    builder.Length = 7;
                    return Response.Json(new { beforeClear, text = builder.ToString(), builder.Length });
                }
            }
            """);

        Assert.Contains("stringBuilder(\"start\")", module);
        Assert.Contains("stringBuilderAppend", module);
        Assert.Contains("stringBuilderClear", module);
        Assert.Contains("stringBuilderText", module);
        Assert.Contains("stringBuilderAppendValue", module);
        Assert.Contains("stringBuilderAppendRepeat", module);
        Assert.Contains("stringBuilderInsert", module);
        Assert.Contains("stringBuilderRemove", module);
        Assert.Contains("stringBuilderReplace", module);
        Assert.Contains("stringBuilderAppendJoin", module);
        Assert.Contains("stringBuilderLength", module);
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
    [InlineData("new StringBuilder().Append(DateTimeOffset.UtcNow)")]
    [InlineData("new StringBuilder().AppendFormat(\"{0}\", 1)")]
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
    public void EmitsLengthMutationThroughConsistentBuilderState()
    {
        var module = Compile("""
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
            """);

        Assert.Contains("stringBuilderLength(builder, 0)", module);
    }
}

