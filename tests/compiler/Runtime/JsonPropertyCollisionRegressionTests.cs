namespace Workers.Compiler.Tests;

public sealed class JsonPropertyCollisionRegressionTests
{
    [Fact]
    public void ExplicitNameCanDifferFromTheDefaultByCase()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public sealed class Payload
            {
                public string Name { get; set; } = "";
                [JsonName("Name")] public string Other { get; set; } = "";
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) =>
                    Response.Json(Json.Parse<Payload>("""{"name":"default","Name":"explicit"}""")!);
            }
            """");

        Assert.Equal("default", result.GetProperty("name").GetString());
        Assert.Equal("explicit", result.GetProperty("Name").GetString());
    }

    [Fact]
    public void TypedReadsRejectNamesThatCollideAfterCamelCasing()
    {
        var error = Assert.Throws<NotSupportedException>(() => TestCompiler.Compile("""
            using Workers;
            public sealed class Payload
            {
                public string Name { get; set; } = "";
                public string name { get; set; } = "";
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) =>
                    Response.Json(Json.Parse<Payload>("{}")!.Name);
            }
            """));

        Assert.Contains("conflicting JSON property name 'name'", error.Message);
    }
}
