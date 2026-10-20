namespace Workers.Compiler.Tests;

public sealed class JsonReadOnlyPropertyRegressionTests
{
    [Fact]
    public void ReadOnlyRecordPropertiesStillValidateTheirConstructorInput()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public interface ILabel { string Text { get; } }
            public sealed record Envelope(ILabel Label)
            {
                public ILabel Label { get; } = Label;
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) => Response.Json(Json.Parse<Envelope>("{}"));
            }
            """));
        Assert.StartsWith("WRK119: Typed JSON reads need a concrete type", error.Message);
    }

    [Fact]
    public void ComputedReadOnlyPropertiesDoNotRequireInputReaders()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public interface ILabel { string Text { get; } }
            public sealed class Label : ILabel { public string Text { get; set; } = ""; }
            public sealed class User
            {
                public string Name { get; set; } = "";
                public ILabel Label => new Label { Text = Name };
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var user = Json.Parse<User>("""{"name":"Ada","label":{"text":"spoofed"}}""")!;
                    return Response.Json(new { user.Name, label = user.Label.Text });
                }
            }
            """");
        Assert.Equal("Ada", result.GetProperty("name").GetString());
        Assert.Equal("Ada", result.GetProperty("label").GetString());
    }
}
