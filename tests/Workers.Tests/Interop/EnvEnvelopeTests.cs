using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed class EnvEnvelopeTests
{
    [Fact]
    public void ConvertsPrimitiveBindings()
    {
        var envelope = JsonSerializer.Deserialize<EnvEnvelope>(
            """
            {
              "bindings": {
                "TEXT": "value",
                "ENABLED": true,
                "COUNT": 3,
                "RATIO": 1.5
              }
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var environment = envelope.ToEnvironment();

        Assert.Equal("value", environment.Get<string>("TEXT"));
        Assert.True(environment.Get<bool>("ENABLED"));
        Assert.Equal(3, environment.Get<long>("COUNT"));
        Assert.Equal(1.5, environment.Get<double>("RATIO"));
    }

    [Fact]
    public void ConvertsObjectVariableBindings()
    {
        var envelope = JsonSerializer.Deserialize<EnvEnvelope>(
            """
            {
              "bindings": {
                "CONFIG": {
                  "clientId": "frontend",
                  "permissions": ["read", "write"]
                }
              }
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var environment = envelope.ToEnvironment();
        var config = environment.ObjectVar<EnvironmentConfig>("CONFIG");

        Assert.Equal("frontend", config.ClientId);
        Assert.Equal(["read", "write"], config.Permissions);
    }

    private sealed class EnvironmentConfig
    {
        public required string ClientId { get; init; }

        public required IReadOnlyList<string> Permissions { get; init; }
    }
}
