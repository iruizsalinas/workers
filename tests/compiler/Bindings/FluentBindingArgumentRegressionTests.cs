namespace Workers.Compiler.Tests;

public sealed class FluentBindingArgumentRegressionTests
{
    [Theory]
    [InlineData("element.SetAttribute(arg0, value)", "data-name", "content")]
    [InlineData("element.RemoveAttribute(value)", "content", "")]
    [InlineData("element.Remove()", "", "")]
    public void FluentCallsPreserveArgumentBindingsAndReturnTheirReceiver(string expression, string expectedName, string expectedValue)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    string capturedName = "", capturedValue = "";
                    var element = JsonValue.From(new
                    {
                        setAttribute = (Action<string, string>)((name, text) => { capturedName = name; capturedValue = text; }),
                        removeAttribute = (Action<string>)(name => capturedName = name),
                        remove = (Action)(() => { })
                    }).As<HtmlElement>()!;
                    string value = "content";
                    string arg0 = "data-name";
                    var returned = {{expression}};
                    return Response.Json(new { name = capturedName, text = capturedValue, same = returned == element });
                }
            }
            """);
        Assert.Equal(expectedName, result.GetProperty("name").GetString());
        Assert.Equal(expectedValue, result.GetProperty("text").GetString());
        Assert.True(result.GetProperty("same").GetBoolean());
    }
}
