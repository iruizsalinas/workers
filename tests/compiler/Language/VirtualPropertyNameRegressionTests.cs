namespace Workers.Compiler.Tests;

public sealed class VirtualPropertyNameRegressionTests
{
    [Theory]
    [InlineData("display-name")]
    [InlineData("with space")]
    [InlineData("9lives")]
    public void VirtualAutoPropertiesSupportJsonNamesThatRequireQuoting(string name)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public class Item
            {
                [JsonName("{{name}}")]
                public virtual int Value { get; set; }
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var item = new Item { Value = 7 };
                    return Response.Json(new { value = item.Value });
                }
            }
            """);

        Assert.Equal(7, result.GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("get => base.Value + 1;")]
    [InlineData("set => base.Value = value + 1;")]
    public void PartialOverridesPreserveQuotedInheritedAccessors(string accessor)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public class Item
            {
                [JsonName("display-name")]
                public virtual int Value { get; set; }
            }
            public sealed class Derived : Item
            {
                public override int Value { {{accessor}} }
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    Item item = new Derived();
                    item.Value = 7;
                    return Response.Json(new { value = item.Value });
                }
            }
            """);

        Assert.Equal(8, result.GetProperty("value").GetInt32());
    }
}
