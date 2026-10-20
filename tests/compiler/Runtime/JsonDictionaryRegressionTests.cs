namespace Workers.Compiler.Tests;

public sealed class JsonDictionaryRegressionTests
{
    [Theory]
    [InlineData("values[\"__proto__\"] = item;")]
    [InlineData("values.Add(\"__proto__\", item);")]
    [InlineData("values.TryAdd(\"__proto__\", item);")]
    public void JsonDictionaryWritesPreservePrototypeNamedKeys(string write)
    {
        var result = JavaScriptRunner.FetchJson($$""""
            using Workers;
            using System.Collections.Generic;
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var values = Json.Parse<Dictionary<string, JsonValue>>("{}")!;
                    var item = Json.Parse("""{"admin":true}""");
                    {{write}}
                    return Response.Json(new {
                        present = values.ContainsKey("__proto__"),
                        count = values.Count,
                        cleanPrototype = JsonValue.From(values)["admin"].IsUndefined,
                        serialized = Json.Stringify(values)
                    });
                }
            }
            """");
        Assert.True(result.GetProperty("present").GetBoolean());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("cleanPrototype").GetBoolean());
        Assert.Equal("{\"__proto__\":{\"admin\":true}}", result.GetProperty("serialized").GetString());
    }
}
