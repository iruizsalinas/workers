namespace Workers.Compiler.Tests;

public sealed class JsonIndexerRegressionTests
{
    [Fact]
    public void IndexedTypesBindOnlyTheirNamedProperties()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public interface ILabel { string Text { get; } }
            public sealed class Label : ILabel { public string Text { get; set; } = ""; }
            public sealed class Indexed
            {
                public string Name { get; set; } = "";
                public ILabel this[int index]
                {
                    get => new Label { Text = Name };
                    set => throw new Exception("The indexer cannot be assigned from JSON.");
                }
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var value = Json.Parse<Indexed>("""{"name":"Ada"}""")!;
                    return Response.Json(new { text = value[0].Text, serialized = Json.Stringify(value) });
                }
            }
            """");
        Assert.Equal("Ada", result.GetProperty("text").GetString());
        Assert.Equal("{\"name\":\"Ada\"}", result.GetProperty("serialized").GetString());
    }
}
