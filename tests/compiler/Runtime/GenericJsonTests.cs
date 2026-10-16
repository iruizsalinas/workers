namespace Workers.Compiler.Tests;

public sealed class GenericJsonTests
{
    [Fact]
    public void InheritsJsonAttributesOnOverriddenProperties()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public class Base
            {
                [JsonName("wire_name")] public virtual string Name { get; set; } = "base";
                [JsonIgnore] public virtual string Secret { get; set; } = "secret";
            }
            public sealed class Child : Base
            {
                public override string Name { get; set; } = "child";
                public override string Secret { get; set; } = "hidden";
            }
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request) =>
                    Response.Json(Json.Parse<Child>("""{"wire_name":"read","secret":"leak"}""")!);
            }
            """");
        Assert.Equal("read", result.GetProperty("wire_name").GetString());
        Assert.False(result.TryGetProperty("name", out _));
        Assert.False(result.TryGetProperty("secret", out _));
    }
    [Fact]
    public void HydratesInheritedPositionalRecordProperties()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public abstract record Base(string Label);
            public sealed record Child(string Label, int Value) : Base(Label);
            public static class Worker
            {
                [Fetch] public static Response Fetch(Request request)
                {
                    var child = Json.Parse<Child>("""{"label":"ok","value":7}""")!;
                    return Response.Json(new { child.Label, child.Value });
                }
            }
            """");
        Assert.Equal("ok", result.GetProperty("label").GetString());
        Assert.Equal(7, result.GetProperty("value").GetInt32());
    }
    [Fact]
    public void HydratesEachConstructedTypeWithItsOwnDefaultsAndNestedReaders()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public sealed record Envelope<T>(T Value, int Count = 7);
            public sealed record User(string Name);
            public class Base<T> { public T Value { get; set; } }
            public sealed class Derived<T> : Base<T>
            {
                public int Count { get; set; } = 3;
                public Derived() { }
                public Derived(int count) { Count = count; }
            }
            public static class Worker
            {
                private static T Read<T>(string source) => Json.Parse<T>(source)!;
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var a = Read<Envelope<int>>("{}");
                    var b = Read<Envelope<bool>>("{}");
                    var c = Read<Envelope<User>>("""{"value":{"name":"Ada"}}""");
                    var d = Read<Derived<User>>("""{"value":{"name":"Grace"}}""");
                    return Response.Json(new { a = a.Value, b = b.Value, c = c.Value.Name,
                        count = a.Count, d = d.Value.Name, otherCount = d.Count, serialized = Json.Stringify(d) });
                }
            }
            """");
        Assert.Equal(0, result.GetProperty("a").GetInt32());
        Assert.False(result.GetProperty("b").GetBoolean());
        Assert.Equal("Ada", result.GetProperty("c").GetString());
        Assert.Equal("Grace", result.GetProperty("d").GetString());
        Assert.Equal(7, result.GetProperty("count").GetInt32());
        Assert.Equal(3, result.GetProperty("otherCount").GetInt32());
        Assert.Contains("Grace", result.GetProperty("serialized").GetString());
    }
}
