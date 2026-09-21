namespace Workers.Compiler.Tests;

public sealed class JsonApiTests
{
    [Fact]
    public void EmitsNativeSerializerOperationsWithoutHelpers()
    {
        var module = Compile("""
            using System.Text.Json;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var json = JsonSerializer.Serialize(new { value = 42 });
                    var parsed = JsonSerializer.Deserialize<JsonElement>(json);
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(new { ok = true });
                    var fromBytes = JsonSerializer.Deserialize<JsonElement>(bytes);
                    return Response.Json(new { parsed, fromBytes, bytes.Length });
                }
            }
            """);

        Assert.Contains("JSON.stringify({ value: 42 })", module);
        Assert.Contains("JSON.parse(json)", module);
        Assert.Contains("new TextEncoder().encode(JSON.stringify({ ok: true }))", module);
        Assert.Contains("JSON.parse(new TextDecoder().decode(bytes))", module);
        Assert.DoesNotContain("function $workers$json", module);
    }

    [Fact]
    public void EmitsJsonElementInspectionWithDemandLoadedValidation()
    {
        var module = Compile("""
            using System.Text.Json;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var root = await request.JsonAsync<JsonElement>();
                    var values = root.GetProperty("values");
                    var first = values[0];
                    return Response.Json(new {
                        root.ValueKind,
                        text = root.GetProperty("text").GetString(),
                        flag = root.GetProperty("flag").GetBoolean(),
                        number = first.GetInt32(),
                        approximate = first.GetDouble(),
                        count = values.GetArrayLength(),
                        copy = values.Clone(),
                        items = values.EnumerateArray().ToArray()
                    });
                }
            }
            """);

        Assert.Contains("function $workers$jsonElementValueKind(value)", module);
        Assert.Contains("function $workers$jsonElementGetValue(value, operation)", module);
        Assert.Contains("function $workers$jsonElementGetProperty(value, property)", module);
        Assert.Contains("function $workers$jsonElementGetIndex(value, index)", module);
    }

    [Fact]
    public void AppliesJsonPropertyNamesAndIgnoresToUserTypes()
    {
        var module = Compile("""
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using Workers;
            public sealed class Payload
            {
                [JsonPropertyName("display-name")]
                public string Name { get; init; } = "";

                [JsonIgnore]
                public string Secret { get; init; } = "";
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var decoded = JsonSerializer.Deserialize<Payload>("{\"display-name\":\"Ada\"}")!;
                    var encoded = JsonSerializer.Serialize(new Payload { Name = decoded.Name, Secret = "hidden" });
                    return Response.Json(new { decoded.Name, encoded });
                }
            }
            """);

        Assert.Contains("decoded[\"display-name\"]", module);
        Assert.Contains("this[\"display-name\"]", module);
        Assert.Contains("return { \"display-name\": this[\"display-name\"] };", module);
        Assert.DoesNotContain("secret: this.secret", module);
    }

    [Fact]
    public void MaterializesUserTypesUsingCaseSensitiveJsonContracts()
    {
        var module = Compile("""
            using System.Text.Json;
            using Workers;
            public sealed class User
            {
                public string Name { get; init; } = "missing";
            }
            public sealed record Item(string Label, int Count);
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var user = JsonSerializer.Deserialize<User>("{\"Name\":\"Ada\"}")!;
                    var item = JsonSerializer.Deserialize<Item>("{\"Label\":\"box\",\"Count\":2}")!;
                    return Response.Json(new { encoded = JsonSerializer.Serialize(user), user.Name, item.Label, item.Count });
                }
            }
            """);

        Assert.Contains("$workers$User.$fromJSON(JSON.parse(", module);
        Assert.Contains("Object.hasOwn(value, \"Name\")", module);
        Assert.Contains("result.name = value[\"Name\"]", module);
        Assert.Contains("return { Name: this.name };", module);
        Assert.Contains("$workers$Item.$fromJSON(JSON.parse(", module);
        Assert.Contains("new $workers$Item(Object.hasOwn(value, \"Label\")", module);
    }

    [Theory]
    [InlineData("JsonSerializer.Serialize(new { value = 1 }, new JsonSerializerOptions())")]
    [InlineData("JsonDocument.Parse(\"{}\")")]
    [InlineData("value.GetRawText()")]
    [InlineData("value.TryGetProperty(\"name\", out var property)")]
    public void RejectsJsonApisOutsideTheNativeProfile(string operation)
    {
        var declaration = operation.Contains("value.", StringComparison.Ordinal)
            ? "var value = await request.JsonAsync<JsonElement>();"
            : "";
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Text.Json;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    {{declaration}}
                    return Response.Json({{operation}});
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

    [Theory]
    [InlineData("[JsonIgnore(Condition = JsonIgnoreCondition.Never)] public string Value { get; init; } = \"\";")]
    [InlineData("[JsonConverter(typeof(JsonStringEnumConverter))] public object Value { get; init; } = new();")]
    public void RejectsUnsupportedSerializationAttributeSemantics(string member)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Text.Json.Serialization;
            using Workers;
            public sealed class Model { {{member}} }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new Model());
            }
            """));

        Assert.StartsWith("WRK119:", error.Message);
    }
}
