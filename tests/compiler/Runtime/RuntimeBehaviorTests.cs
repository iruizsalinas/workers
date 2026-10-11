using System.Text.Json;

namespace Workers.Compiler.Tests;

// These tests run the generated module in Node.js and assert results, not generated text.
public sealed class RuntimeBehaviorTests
{
    [Fact]
    public void TypedJsonReadsUseJsonNamesDefaultsAndDates()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public sealed record Item(string Name, int Count = 7);
            public sealed record Order(
                string Id,
                Date? CreatedAt,
                double Total,
                [property: JsonName("line_items")] List<Item> Items,
                HashSet<string> Tags,
                [property: JsonIgnore] string Secret = "kept");
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var order = Json.Parse<Order>("""{"id":"a1","createdAt":"2026-01-02T03:04:05.000Z","line_items":[{"name":"pen"}],"tags":["x","x","y"],"secret":"sent"}""")!;
                    var empty = Json.Parse<Order>("""{"id":"a2"}""")!;
                    return Response.Json(new
                    {
                        order.Id, year = order.CreatedAt!.GetUtcFullYear(), order.Total, firstItem = order.Items[0].Name,
                        defaultCount = order.Items[0].Count, tagCount = order.Tags.Count, order.Secret,
                        emptyHasDate = empty.CreatedAt is not null, emptyItems = empty.Items is null
                    });
                }
            }
            """");

        Assert.Equal("a1", result.GetProperty("id").GetString());
        Assert.Equal(2026, result.GetProperty("year").GetInt32());
        Assert.Equal(0, result.GetProperty("total").GetDouble());
        Assert.Equal("pen", result.GetProperty("firstItem").GetString());
        Assert.Equal(7, result.GetProperty("defaultCount").GetInt32());
        Assert.Equal(2, result.GetProperty("tagCount").GetInt32());
        Assert.Equal("kept", result.GetProperty("secret").GetString());
        Assert.False(result.GetProperty("emptyHasDate").GetBoolean());
        Assert.True(result.GetProperty("emptyItems").GetBoolean());
    }

    [Fact]
    public void QueryBindingConvertsTextAndKeepsRepeatedValues()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public sealed class Search
            {
                public string? Term { get; set; }
                public int Page { get; set; } = 1;
                public bool Exact { get; set; }
                public List<string> Tag { get; set; } = [];
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var search = new Url(request.Url).SearchParams.As<Search>();
                    return Response.Json(new { search.Term, search.Page, search.Exact, tags = search.Tag.Count });
                }
            }
            """", "https://worker.test/?term=ada&term=grace&page=3&exact=true&tag=a&tag=b");

        Assert.Equal("ada", result.GetProperty("term").GetString());
        Assert.Equal(3, result.GetProperty("page").GetInt32());
        Assert.True(result.GetProperty("exact").GetBoolean());
        Assert.Equal(2, result.GetProperty("tags").GetInt32());
    }

    [Fact]
    public void JsonValueAccessorsCheckTheValueType()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var value = Json.Parse("""{"name":"Ada","age":36,"tags":["a","b"],"none":null}""");
                    var error = "";
                    try { value["age"].AsString(); }
                    catch (Exception exception) { error = exception.Message; }
                    return Response.Json(new
                    {
                        name = value["name"].AsString(), age = value["age"].AsNumber(), length = value["tags"].Length,
                        missing = value["other"].IsUndefined, isNull = value["none"].IsNull, has = value.Has("name"), error
                    });
                }
            }
            """");

        Assert.Equal("Ada", result.GetProperty("name").GetString());
        Assert.Equal(36, result.GetProperty("age").GetDouble());
        Assert.Equal(2, result.GetProperty("length").GetInt32());
        Assert.True(result.GetProperty("missing").GetBoolean());
        Assert.True(result.GetProperty("isNull").GetBoolean());
        Assert.True(result.GetProperty("has").GetBoolean());
        Assert.Equal("Expected a JSON string, got number.", result.GetProperty("error").GetString());
    }

    [Fact]
    public void PrimitiveParsingIsStrictForIntegersAndUsesJavaScriptNumbersForDoubles()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    var parsedInt = int.TryParse(" 42 ", out var number);
                    var prefixed = int.TryParse("12abc", out _);
                    var tooLarge = int.TryParse("2147483648", out _);
                    var unsigned = uint.Parse("4294967295");
                    var negativeUnsigned = uint.TryParse("-1", out _);
                    var flag = bool.Parse(" TRUE ");
                    var exponent = double.Parse("1e3");
                    var invalid = double.TryParse("abc", out _);
                    return Response.Json(new { parsedInt, number, prefixed, tooLarge, unsigned, negativeUnsigned, flag, exponent, invalid });
                }
            }
            """");

        Assert.True(result.GetProperty("parsedInt").GetBoolean());
        Assert.Equal(42, result.GetProperty("number").GetInt32());
        Assert.False(result.GetProperty("prefixed").GetBoolean());
        Assert.False(result.GetProperty("tooLarge").GetBoolean());
        Assert.Equal(4294967295, result.GetProperty("unsigned").GetInt64());
        Assert.False(result.GetProperty("negativeUnsigned").GetBoolean());
        Assert.True(result.GetProperty("flag").GetBoolean());
        Assert.Equal(1000, result.GetProperty("exponent").GetDouble());
        Assert.False(result.GetProperty("invalid").GetBoolean());
    }

    [Fact]
    public void WhenAllWaitsForEveryTaskAndThrowsTheFirstFailureInOrder()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var completed = new List<string>();
                    var values = await Task.WhenAll(Delay(5, "a", completed), Delay(1, "b", completed));
                    var message = "";
                    try
                    {
                        await Task.WhenAll(Fail(10, "slow", completed), Fail(1, "fast", completed));
                    }
                    catch (Exception exception)
                    {
                        message = exception.Message;
                    }
                    return Response.Json(new { values, message, completed });
                }

                private static async Task<string> Delay(int milliseconds, string value, List<string> completed)
                {
                    await Scheduler.WaitAsync(milliseconds);
                    completed.Add(value);
                    return value;
                }

                private static async Task<string> Fail(int milliseconds, string value, List<string> completed)
                {
                    await Scheduler.WaitAsync(milliseconds);
                    completed.Add(value);
                    throw new InvalidOperationException(value);
                }
            }
            """");

        Assert.Equal(["a", "b"], Strings(result.GetProperty("values")));
        Assert.Equal("slow", result.GetProperty("message").GetString());
        Assert.Equal(["b", "a", "fast", "slow"], Strings(result.GetProperty("completed")));
    }

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();
}
