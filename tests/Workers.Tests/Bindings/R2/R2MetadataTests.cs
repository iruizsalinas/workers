using System.Collections.ObjectModel;
using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed class R2MetadataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ObjectCustomMetadataIsReadOnlySnapshot()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["owner"] = "stored"
        };

        var obj = R2Object.FromEnvelope(new R2ObjectEnvelope
        {
            Key = "file.bin",
            Version = "v1",
            Size = 1,
            Etag = "etag",
            HttpEtag = "\"etag\"",
            CustomMetadata = metadata
        });
        metadata["owner"] = "changed";

        Assert.Equal("stored", obj.CustomMetadata["owner"]);
        Assert.IsType<ReadOnlyDictionary<string, string>>(obj.CustomMetadata);
    }

    [Fact]
    public void PutOptionsEnvelopeCopiesCustomMetadata()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["owner"] = "stored"
        };

        var request = R2PutRequest.From(
            "file.bin",
            Body.Text("body"),
            new R2PutOptions { CustomMetadata = metadata });
        metadata["owner"] = "changed";

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var payload = JsonDocument.Parse(json);

        Assert.Equal("stored", payload.RootElement
            .GetProperty("options")
            .GetProperty("customMetadata")
            .GetProperty("owner")
            .GetString());
    }
}
