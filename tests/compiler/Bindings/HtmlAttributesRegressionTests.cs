namespace Workers.Compiler.Tests;

public sealed class HtmlAttributesRegressionTests
{
    [Fact]
    public void HtmlAttributeEntriesSupportDictionaryOperations()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static int reads;
                private static HtmlElement Next(HtmlElement element) { reads++; return element; }
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var element = JsonValue.From(new
                    {
                        attributes = new[] { new[] { "id", "link" }, new[] { "title", "next" } }
                    }).As<HtmlElement>()!;
                    var attributes = Next(element).Attributes;
                    var summary = "";
                    foreach (var entry in attributes) summary += entry.Key + ":" + entry.Value + ";";
                    return Response.Json(new { id = attributes["id"], count = attributes.Count,
                        contains = attributes.ContainsKey("title"), summary, reads });
                }
            }
            """);
        Assert.Equal("link", result.GetProperty("id").GetString());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("contains").GetBoolean());
        Assert.Equal("id:link;title:next;", result.GetProperty("summary").GetString());
        Assert.Equal(1, result.GetProperty("reads").GetInt32());
    }
}
