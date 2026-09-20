namespace Workers.Compiler.Tests;

public sealed class SecurityEmissionTests
{
    [Fact]
    public void EmitsPrototypeSensitiveObjectKeysAsComputedProperties()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var anonymous = new { __proto__ = "anonymous", @default = 1 };
                    var record = new Payload(__proto__: "record");
                    var model = new Model { __proto__ = "class" };
                    return Response.Json(new { anonymous, record, model });
                }
            }
            public sealed record Payload(string __proto__);
            public sealed class Model
            {
                public string __proto__ { get; init; } = "initial";
            }
            """);

        Assert.Contains("[\"__proto__\"]: \"anonymous\"", module);
        Assert.True(module.Split("[\"__proto__\"]:", StringSplitOptions.None).Length >= 4);
        Assert.Contains("this.__proto__$2 = \"initial\"", module);
        Assert.Contains("[\"__proto__\"]: this.__proto__$2", module);
        Assert.Contains("default: 1", module);
        Assert.DoesNotContain("{ __proto__:", module);
        Assert.DoesNotContain("this.__proto__ =", module);
    }

    [Fact]
    public void RejectsExplicitJsonNamesThatTargetPrototypeMutation()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using System.Text.Json.Serialization;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(new Model());
            }
            public sealed class Model
            {
                [JsonPropertyName("__proto__")]
                public string Value { get; init; } = "unsafe";
            }
            """));

        Assert.StartsWith("WRK119:", error.Message);
    }
}
