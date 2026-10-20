namespace Workers.Compiler.Tests;

public sealed class JsonAccessorVisibilityRegressionTests
{
    [Theory]
    [InlineData("Json.Parse<Login>(\"{\\\"credential\\\":\\\"secret\\\"}\")!")]
    [InlineData("new UrlSearchParams(\"credential=secret\").As<Login>()")]
    public void SetterOnlyPropertiesAcceptInputWithoutAppearingInOutput(string read)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public sealed class Login
            {
                private string password = "";
                [JsonName("credential")] public string Password { set => password = value; }
                public bool HasPassword() => password == "secret";
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var login = {{read}};
                    return Response.Json(new { accepted = login.HasPassword(), serialized = Json.Stringify(login) });
                }
            }
            """);
        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.Equal("{}", result.GetProperty("serialized").GetString());
    }

    [Fact]
    public void PrivateGettersStayOutOfJsonWhilePublicSettersCanReadInput()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public sealed class Login
            {
                public string Name { get; set; } = "";
                public string Password { private get; set; } = "";
                public bool HasPassword() => Password == "secret";
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var login = Json.Parse<Login>("""{"name":"Ada","password":"secret"}""")!;
                    return Response.Json(new { serialized = Json.Stringify(login), accepted = login.HasPassword() });
                }
            }
            """");
        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.Equal("{\"name\":\"Ada\"}", result.GetProperty("serialized").GetString());
    }
}
