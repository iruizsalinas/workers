namespace Workers.Compiler.Tests;

public sealed class JsonNodeTests
{
    private static string Worker(string body) => $$"""
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Workers;
        public static class Worker
        {
            [Fetch]
            public static async Task<Response> Fetch(Request request, Env env, Context context)
            {
                {{body}}
            }
        }
        """;

    [Fact]
    public void MutatesObjectsThroughPrototypeSafeHelpers()
    {
        var module = Compile(Worker("""
            var target = new JsonObject { ["a"] = 1 };
            target["__proto__"] = "value";
            target.Add("b", 2.5);
            var read = target["constructor"];
            return Response.Json(new JsonObject { ["target"] = target, ["read"] = read });
            """));

        Assert.Contains("Object.create(null)", module);
        Assert.Contains("$workers$jsonObjectSet(target, \"__proto__\", \"value\")", module);
        Assert.Contains("$workers$jsonObjectAdd(target, \"b\", $workers$jsonNodeNumber(2.5))", module);
        Assert.Contains("$workers$jsonObjectGet(target, \"constructor\")", module);
        Assert.Contains("Object.defineProperty(target, key, { value, writable: true, enumerable: true, configurable: true })", module);
        Assert.Contains("return Object.hasOwn(target, key) ? target[key] : null;", module);
        Assert.Contains("if (parents.has(child)) throw new Error(\"The node already has a parent.\");", module);
    }

    [Fact]
    public void ParsesRequestBodiesFromTextSoLargeNumbersKeepTheirDigits()
    {
        var module = Compile(Worker("""
            var body = await request.JsonAsync<JsonObject>();
            var cached = await env.Kv("KV").GetJsonAsync<JsonNode>("key");
            return Response.Json(new JsonArray(body, cached));
            """));

        Assert.Contains("request.text().then(text => text == null ? null : $workers$jsonNodeParse(text, 1))", module);
        Assert.Contains("type: \"text\"", module);
        Assert.Contains("JSON.rawJSON(context.source)", module);
        Assert.Contains("if (depth >= 64) throw new RangeError(\"The maximum configured depth of 64 has been exceeded.\");", module);
    }

    [Fact]
    public void ConvertsValuesWithTheirStaticTypes()
    {
        var module = Compile(Worker("""
            JsonNode single = 1.1f;
            JsonNode when = DateTimeOffset.UnixEpoch;
            var list = new JsonArray { 1, "x" };
            list.Add(2.5);
            var count = (int)list[0]!;
            var text = (string?)list[1];
            return Response.Json(new JsonArray(single, when, list, count, text));
            """));

        Assert.Contains("$workers$jsonNodeSingle(", module);
        Assert.Contains("$workers$jsonNodeDate(new Date(0), true)", module);
        Assert.Contains("$workers$jsonArrayAdd(list, $workers$jsonNodeNumber(2.5))", module);
        Assert.Contains("$workers$jsonNodeValue(node, 2, \"System.Int32\", -2147483648, 2147483647)", module);
        Assert.Contains("((node) => node == null ? null : $workers$jsonNodeValue(node, 0, \"System.String\"))", module);
    }

    [Theory]
    [InlineData("return Response.Json(JsonNode.Parse(\"{}\")!.Parent);", "Parent")]
    [InlineData("return Response.Text(JsonNode.Parse(\"{}\")!.GetPath());", "GetPath")]
    [InlineData("return Response.Json(JsonNode.Parse(\"\\\"2026-01-01\\\"\")!.GetValue<DateOnly>());", "Reading a JsonValue as 'System.DateOnly'")]
    [InlineData("var node = new JsonArray(1); node.Remove(node[0]); return Response.Json(node);", "Remove")]
    [InlineData("return Response.Json(JsonNode.Parse(\"{}\", new JsonNodeOptions { PropertyNameCaseInsensitive = true }));", "JsonNodeOptions")]
    [InlineData("return Response.Json(JsonValue.Create(TimeSpan.Zero));", "Converting 'System.TimeSpan' to a JsonNode")]
    public void RejectsMembersWithoutAFaithfulMapping(string body, string member)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile(Worker(body)));
        Assert.Contains(member, error.Message);
    }
}
