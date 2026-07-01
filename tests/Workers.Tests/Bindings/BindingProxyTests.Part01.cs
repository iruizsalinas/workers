using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task KvListRejectsInvalidLimit()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-kv-list-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Kv("CACHE").ListAsync(new KvListOptions { Limit = 0 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Kv("CACHE").ListAsync(new KvListOptions { Limit = 1001 }));
    }

    [Fact]
    public async Task KvListParsesRuntimeEnvelope()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "keys": [
                {
                  "name": "workers-rs-coverage",
                  "expiration": 2000000000,
                  "metadata": {
                    "kind": "test"
                  }
                }
              ],
              "listComplete": false,
              "cursor": "next"
            }
            """);
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-kv-list");

        var result = await environment.Kv("CACHE").ListAsync(new KvListOptions { Prefix = "workers-rs-" });

        Assert.False(result.ListComplete);
        Assert.Equal("next", result.Cursor);
        var key = Assert.Single(result.Keys);
        Assert.Equal("workers-rs-coverage", key.Name);
        Assert.Equal(2000000000ul, key.Expiration);
        Assert.Equal("test", key.Metadata!.Value.GetProperty("kind").GetString());

        using var payload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("workers-rs-", payload.RootElement.GetProperty("prefix").GetString());
    }

    [Fact]
    public async Task KvProxyDispatchesMetadataAndOptions()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "value": "cached",
              "metadata": {
                "version": 2,
                "kind": "profile"
              }
            }
            """,
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-kv-options");

        var value = await environment.Kv("CACHE").GetTextWithMetadataAsync(
            "profile:1",
            new KvGetOptions { CacheTtl = 120 });
        await environment.Kv("CACHE").PutTextAsync(
            "profile:1",
            "cached",
            new KvPutOptions
            {
                Expiration = 2000000000,
                ExpirationTtl = 3600,
                Metadata = new
                {
                    version = 2,
                    kind = "profile"
                }
            });

        Assert.Equal("cached", value.Value);
        Assert.Equal(2, value.Metadata!.Value.GetProperty("version").GetInt32());
        Assert.Equal("profile", value.MetadataAs<KvMetadata>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Kind);
        Assert.Equal(["kv.getTextWithMetadata", "kv.putText"], dispatcher.Invocations.Select(static call => call.Operation));

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("profile:1", getPayload.RootElement.GetProperty("key").GetString());
        Assert.Equal(120ul, getPayload.RootElement.GetProperty("options").GetProperty("cacheTtl").GetUInt64());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        var putOptions = putPayload.RootElement.GetProperty("options");
        Assert.Equal("profile:1", putPayload.RootElement.GetProperty("key").GetString());
        Assert.Equal("cached", putPayload.RootElement.GetProperty("value").GetString());
        Assert.Equal(2000000000ul, putOptions.GetProperty("expiration").GetUInt64());
        Assert.Equal(3600ul, putOptions.GetProperty("expirationTtl").GetUInt64());
        Assert.Equal(2, putOptions.GetProperty("metadata").GetProperty("version").GetInt32());
        Assert.Equal("profile", putOptions.GetProperty("metadata").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task KvGetRejectsInvalidCacheTtl()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-kv-get-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Kv("CACHE").GetTextAsync("key", new KvGetOptions { CacheTtl = 59 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Kv("CACHE").GetTextWithMetadataAsync("key", new KvGetOptions { CacheTtl = 59 }));
    }

    [Fact]
    public async Task KvGetTextReadsSimpleValueEnvelope()
    {
        var dispatcher = new CapturingDispatcher("""{"value":"cached"}""", """{"value":null}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-kv-get-text");

        var value = await environment.Kv("CACHE").GetTextAsync("key");
        var missing = await environment.Kv("CACHE").GetTextAsync("missing");

        Assert.Equal("cached", value);
        Assert.Null(missing);
        Assert.Equal(["kv.getText", "kv.getText"], dispatcher.Invocations.Select(static call => call.Operation));

        using var payload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("key", payload.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public async Task KvProxyDispatchesBinaryOperations()
    {
        var valueBase64 = Convert.ToBase64String([1, 2, 3]);
        var metadataValueBase64 = Convert.ToBase64String([4, 5, 6]);
        var dispatcher = new CapturingDispatcher(
            $$"""{"bodyBase64":"{{valueBase64}}"}""",
            """{"bodyBase64":null}""",
            $$"""
            {
              "bodyBase64": "{{metadataValueBase64}}",
              "metadata": {
                "kind": "avatar"
              }
            }
            """,
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-kv-bytes");

        var value = await environment.Kv("CACHE").GetBytesAsync("avatar:1", new KvGetOptions { CacheTtl = 60 });
        var missing = await environment.Kv("CACHE").GetBytesAsync("missing");
        var withMetadata = await environment.Kv("CACHE").GetBytesWithMetadataAsync("avatar:2");
        await environment.Kv("CACHE").PutBytesAsync(
            "avatar:3",
            new byte[] { 7, 8, 9 },
            new KvPutOptions
            {
                Metadata = new
                {
                    kind = "avatar"
                }
            });

        Assert.Equal([1, 2, 3], value);
        Assert.Null(missing);
        Assert.Equal([4, 5, 6], withMetadata.Value);
        Assert.Equal("avatar", withMetadata.Metadata!.Value.GetProperty("kind").GetString());
        Assert.Equal(["kv.getBytes", "kv.getBytes", "kv.getBytesWithMetadata", "kv.putBytes"], dispatcher.Invocations.Select(static call => call.Operation));

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("avatar:1", getPayload.RootElement.GetProperty("key").GetString());
        Assert.Equal(60ul, getPayload.RootElement.GetProperty("options").GetProperty("cacheTtl").GetUInt64());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("avatar:3", putPayload.RootElement.GetProperty("key").GetString());
        Assert.Equal(Convert.ToBase64String([7, 8, 9]), putPayload.RootElement.GetProperty("bodyBase64").GetString());
        Assert.Equal("avatar", putPayload.RootElement.GetProperty("options").GetProperty("metadata").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task KvProxyDispatchesJsonOperations()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dispatcher = new CapturingDispatcher(
            """
            {
              "value": {
                "id": 7,
                "name": "Ada"
              }
            }
            """,
            """{"value":null}""",
            """
            {
              "value": {
                "id": 8,
                "name": "Grace"
              },
              "metadata": {
                "source": "seed"
              }
            }
            """,
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-kv-json");

        var value = await environment.Kv("CACHE").GetJsonAsync<UserRow>("user:7", jsonOptions);
        var missing = await environment.Kv("CACHE").GetJsonAsync<UserRow>(
            "missing",
            new KvGetOptions { CacheTtl = 90 },
            jsonOptions);
        var withMetadata = await environment.Kv("CACHE").GetJsonWithMetadataAsync<UserRow>(
            "user:8",
            jsonOptions: jsonOptions);
        await environment.Kv("CACHE").PutJsonAsync(
            "user:9",
            new UserRow { Id = 9, Name = "Lin" },
            new KvPutOptions
            {
                Metadata = new
                {
                    source = "manual"
                }
            },
            jsonOptions);

        Assert.Equal(7, value!.Id);
        Assert.Equal("Ada", value.Name);
        Assert.Null(missing);
        Assert.Equal(8, withMetadata.Value!.Id);
        Assert.Equal("Grace", withMetadata.Value.Name);
        Assert.Equal("seed", withMetadata.Metadata!.Value.GetProperty("source").GetString());
        Assert.Equal(["kv.getJson", "kv.getJson", "kv.getJsonWithMetadata", "kv.putJson"], dispatcher.Invocations.Select(static call => call.Operation));

        using var cachedGetPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal(90ul, cachedGetPayload.RootElement.GetProperty("options").GetProperty("cacheTtl").GetUInt64());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("user:9", putPayload.RootElement.GetProperty("key").GetString());
        using var storedValue = JsonDocument.Parse(putPayload.RootElement.GetProperty("valueJson").GetString()!);
        Assert.Equal(9, storedValue.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Lin", storedValue.RootElement.GetProperty("name").GetString());
        Assert.Equal("manual", putPayload.RootElement.GetProperty("options").GetProperty("metadata").GetProperty("source").GetString());
    }

    [Fact]
    public async Task KvProxyDispatchesBulkTextAndJsonReads()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dispatcher = new CapturingDispatcher(
            """
            {
              "values": {
                "a": "alpha",
                "b": null
              }
            }
            """,
            """
            {
              "values": {
                "a": {
                  "value": "alpha",
                  "metadata": {
                    "group": "letters"
                  }
                },
                "b": {
                  "value": null,
                  "metadata": null
                }
              }
            }
            """,
            """
            {
              "values": {
                "user:1": {
                  "id": 1,
                  "name": "Ada"
                },
                "user:2": null
              }
            }
            """,
            """
            {
              "values": {
                "user:3": {
                  "value": {
                    "id": 3,
                    "name": "Lin"
                  },
                  "metadata": {
                    "source": "seed"
                  }
                }
              }
            }
            """);
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-kv-bulk");

        var text = await environment.Kv("CACHE").GetTextBulkAsync(["a", "b"], new KvGetOptions { CacheTtl = 60 });
        var textWithMetadata = await environment.Kv("CACHE").GetTextBulkWithMetadataAsync(["a", "b"]);
        var json = await environment.Kv("CACHE").GetJsonBulkAsync<UserRow>(["user:1", "user:2"], jsonOptions: jsonOptions);
        var jsonWithMetadata = await environment.Kv("CACHE").GetJsonBulkWithMetadataAsync<UserRow>(["user:3"], jsonOptions: jsonOptions);

        Assert.Equal("alpha", text["a"]);
        Assert.Null(text["b"]);
        Assert.Equal("alpha", textWithMetadata["a"].Value);
        Assert.Equal("letters", textWithMetadata["a"].Metadata!.Value.GetProperty("group").GetString());
        Assert.Null(textWithMetadata["b"].Value);
        Assert.Null(textWithMetadata["b"].Metadata);
        Assert.Equal(1, json["user:1"]!.Id);
        Assert.Null(json["user:2"]);
        Assert.Equal("Lin", jsonWithMetadata["user:3"].Value!.Name);
        Assert.Equal("seed", jsonWithMetadata["user:3"].Metadata!.Value.GetProperty("source").GetString());
        Assert.Equal(
            ["kv.getTextBulk", "kv.getTextBulkWithMetadata", "kv.getJsonBulk", "kv.getJsonBulkWithMetadata"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var textPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        var payloadKeys = textPayload.RootElement.GetProperty("keys").EnumerateArray().Select(static key => key.GetString()).ToArray();
        Assert.Collection(
            payloadKeys,
            static key => Assert.Equal("a", key),
            static key => Assert.Equal("b", key));
        Assert.Equal(60ul, textPayload.RootElement.GetProperty("options").GetProperty("cacheTtl").GetUInt64());
    }

    [Fact]
    public async Task KvBulkRejectsInvalidKeys()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-kv-bulk-invalid");
        var tooManyKeys = Enumerable.Range(0, 101).Select(static index => $"key:{index}");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Kv("CACHE").GetTextBulkAsync(tooManyKeys));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            environment.Kv("CACHE").GetJsonBulkAsync<UserRow>(["valid", ""]));
    }
}
