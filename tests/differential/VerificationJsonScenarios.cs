using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

// Configuration requests and telemetry exports exercise both successful contracts and rejection
// paths. This file executes unchanged on the CLR and inside the Worker.
public static class VerificationJsonScenarios
{
    public static Dictionary<string, string> Run()
    {
        var result = new Dictionary<string, string>();
        result["privateRequired"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationPrivateRequired>("{}"));
        result["ignoredCollision"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationIgnoredCollision>("{}"));
        result["privateCollision"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationPrivateCollision>("{}"));
        result["privateFirstCollision"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationPrivateFirstCollision>("{}"));
        result["ignoredFirstCollision"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationIgnoredFirstCollision>("{}"));
        foreach (var entry in RuntimeContracts()) result[entry.Key] = entry.Value;
        return result;
    }

    public static Dictionary<string, string> RuntimeContracts()
    {
        var result = new Dictionary<string, string>();
        result["privateGetter"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationPrivateGetter>("{}"));
        result["copyMissing"] = CopyContract("{}");
        result["copyValid"] = CopyContract("{\"Enabled\":false}");
        result["emptyCopyMissing"] = EmptyCopyContract("{}");
        result["emptyCopyValid"] = EmptyCopyContract("{\"Enabled\":false}");
        result["customCopyWith"] = CustomCopyWith();
        result["positionalCopyMissing"] = PositionalCopyContract("{\"Name\":\"alpha\"}");
        result["positionalCopyValid"] = PositionalCopyContract("{\"Name\":\"alpha\",\"Enabled\":false}");
        result["nullable"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationNullableConfig>(
            "{\"Threshold\":null,\"Deadline\":null,\"Counts\":[null,0,2]}"));
        result["boxedFinite"] = ExportTelemetry(12.5);
        result["boxedNan"] = ExportTelemetry(double.NaN);
        result["boxedInfinity"] = ExportTelemetry(double.PositiveInfinity);
        result["boxedNegativeInfinity"] = ExportTelemetry(double.NegativeInfinity);
        result["nestedNonfinite"] = ExportTelemetry(new Dictionary<string, object?> { ["sample"] = double.NaN });
        foreach (var json in DateCases()) result[$"date:{json}"] = DateContract(json);
        return result;
    }

    public static string[] ConfigCases() =>
    [
        "{}",
        "{\"name\":\"alpha\"}",
        "{\"enabled\":false}",
        "{\"name\":\"alpha\",\"enabled\":false}",
        "{\"NAME\":\"alpha\",\"ENABLED\":true}",
        "{\"name\":null,\"enabled\":false}",
        "{\"name\":\"alpha\",\"enabled\":null}",
        "{\"name\":\"alpha\",\"enabled\":\"true\"}",
        "{\"name\":\"alpha\",\"enabled\":true,\"quota\":\"12\"}",
        "{\"name\":\"alpha\",\"enabled\":true,\"quota\":null}",
        "{\"name\":\"alpha\",\"enabled\":true,\"nodes\":[{}]}",
        "{\"name\":\"alpha\",\"enabled\":true,\"nodes\":[{\"node-id\":\"one\"}]}",
        "{\"name\":\"alpha\",\"enabled\":true,\"nodes\":[{\"node-id\":null}]}",
        "{\"name\":\"alpha\",\"enabled\":true,\"nodes\":null}"
    ];

    // The CLR baseline uses the same web defaults as Request.JsonAsync and KV JSON reads.
    public static VerificationConfigResult ReadWebOnClr(string json)
    {
        try { return Summarize(JsonSerializer.Deserialize<VerificationConfigInput>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))); }
        catch (Exception) { return Rejected(); }
    }

    public static VerificationConfigResult Summarize(VerificationConfigInput? input)
    {
        if (input is null || string.IsNullOrEmpty(input.Name) || input.Nodes is null)
            return Rejected();
        return new VerificationConfigResult(201, input.Name, input.Enabled, input.Quota, input.Nodes.Length);
    }

    public static VerificationConfigResult Rejected() => new(400, "", false, null, 0);

    private static string CopyContract(string json)
    {
        try { return JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationCopyConfig>(json)); }
        catch (Exception) { return "rejected"; }
    }

    private static string PositionalCopyContract(string json)
    {
        try { return JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationPositionalCopyConfig>(json)); }
        catch (Exception) { return "rejected"; }
    }

    private static string EmptyCopyContract(string json)
    {
        try { return JsonSerializer.Serialize(JsonSerializer.Deserialize<VerificationEmptyCopyConfig>(json)); }
        catch (Exception) { return "rejected"; }
    }

    private static string ExportTelemetry(object? reading)
    {
        try { return JsonSerializer.Serialize(reading); }
        catch (Exception) { return "rejected"; }
    }

    private static string CustomCopyWith()
    {
        var source = new VerificationCustomCopyConfig { Name = "alpha", Revision = 99, Marker = 42, Tags = ["one"] };
        var copy = source with { Name = $"copy:{source.Tags.Count}" };
        return $"{source.Name}|{source.Revision}|{source.Marker}|{string.Join(",", source.Tags)};{copy.Name}|{copy.Revision}|{copy.Marker}|{string.Join(",", copy.Tags)}";
    }

    public static string[] DateCases() =>
    [
        "\"0001-01-01T00:00:00Z\"", "\"0001-01-01T00:00:00+00:01\"",
        "\"0001-01-01T14:00:00+14:00\"", "\"9999-12-31T23:59:59.999Z\"",
        "\"9999-12-31T23:59:59-00:01\"", "\"2024-02-29T00:00:00Z\"",
        "\"1900-02-29T00:00:00Z\"", "\"2000-02-29T00:00:00Z\"",
        "\"2026-01-01T12:34Z\"", "\"2026-01-01T12:34:56.Z\"",
        "\"2026-01-01T12:34:56.+00:00\"", "\"2026-01-01T12:34:56.1234567890123456Z\"",
        "\"2026-01-01T12:34:56.12345678901234567Z\"", "\"2026-01-01T23:59:60Z\"",
        "\"2026-01-01T24:00Z\"", "\"2026-01-01T12:34:56+14:01\""
    ];

    private static string DateContract(string json)
    {
        try { JsonSerializer.Deserialize<DateTimeOffset>(json); return "accepted"; }
        catch (Exception) { return "rejected"; }
    }
}

public sealed class VerificationPrivateRequired
{
    public string Name { get; init; } = "default";
    [JsonRequired] private string Internal { get; init; } = "internal";
}

public sealed class VerificationIgnoredCollision
{
    [JsonPropertyName("name")] public string Name { get; init; } = "public";
    [JsonIgnore, JsonPropertyName("name")] public string Internal { get; init; } = "private";
}

public sealed class VerificationPrivateCollision
{
    [JsonPropertyName("name")] public string Name { get; init; } = "public";
    [JsonPropertyName("name")] private string Internal { get; init; } = "private";
}

public sealed class VerificationPrivateGetter
{
    public string Name { get; init; } = "visible";
    public string Token { private get; init; } = "hidden";
}

public sealed class VerificationPrivateFirstCollision
{
    private string Internal { get; init; } = "hidden";
    [JsonPropertyName("internal")] public string Name { get; init; } = "visible";
}

public sealed class VerificationIgnoredFirstCollision
{
    [JsonIgnore] public string Internal { get; init; } = "hidden";
    [JsonPropertyName("internal")] public string Name { get; init; } = "visible";
}

public sealed record VerificationCopyConfig
{
    public required bool Enabled { get; init; }
    [SetsRequiredMembers] private VerificationCopyConfig(VerificationCopyConfig original) { Enabled = original.Enabled; }
}

public sealed record VerificationEmptyCopyConfig
{
    public required bool Enabled { get; init; }
    [SetsRequiredMembers] private VerificationEmptyCopyConfig(VerificationEmptyCopyConfig original) { }
}

public sealed record VerificationCustomCopyConfig
{
    public string Name { get; init; } = "default";
    public int Revision { get; init; } = 7;
    public int Marker = 9;
    public List<string> Tags { get; init; } = [];

    private VerificationCustomCopyConfig(VerificationCustomCopyConfig original)
    {
        Name = original.Name;
        Tags = new List<string>(original.Tags);
        original.Tags.Add("copied");
    }
}

public sealed record VerificationPositionalCopyConfig(string Name)
{
    public required bool Enabled { get; init; }
    [SetsRequiredMembers] private VerificationPositionalCopyConfig(VerificationPositionalCopyConfig original)
    {
        Name = original.Name;
        Enabled = original.Enabled;
    }
}

public sealed class VerificationNullableConfig
{
    public double? Threshold { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public int?[] Counts { get; init; } = [];
}

public sealed class VerificationConfigInput
{
    public required string? Name { get; init; }
    public required bool Enabled { get; init; }
    public int? Quota { get; init; }
    public VerificationConfigNode[]? Nodes { get; init; } = [];
    [JsonIgnore] public string Secret { get; init; } = "internal";
}

public sealed record VerificationConfigNode([property: JsonRequired, JsonPropertyName("node-id")] string? Id);
public sealed record VerificationConfigResult(int Status, string Name, bool Enabled, int? Quota, int NodeCount);
