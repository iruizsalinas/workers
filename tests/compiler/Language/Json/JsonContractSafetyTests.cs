namespace Workers.Compiler.Tests;

public sealed class JsonContractSafetyTests
{
    [Theory]
    [InlineData("[JsonIgnore] public required string TenantId { get; init; }")]
    [InlineData("[JsonRequired] public string TenantId { get; } = \"default\";")]
    public void RejectsRequiredContractsThatCannotBeDeserialized(string member)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Text.Json;
            using System.Text.Json.Serialization;
            using Workers;
            public sealed class Tenant { {{member}} }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(JsonSerializer.Deserialize<Tenant>("{}"));
            }
            """));
        Assert.StartsWith("WRK119:", error.Message);
        Assert.Contains("Required JSON property", error.Message);
    }
}
