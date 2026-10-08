using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

// A provisioning API must reject incomplete contracts before creating tenant resources.
// Run these decisions on both the CLR and the generated Worker.
public static class JsonApplicationScenarios
{
    public static Dictionary<string, string> TelemetryExports()
    {
        var result = new Dictionary<string, string>();
        result["finite"] = ExportReading(12.5);
        result["nan"] = ExportReading(double.NaN);
        result["positiveInfinity"] = ExportReading(double.PositiveInfinity);
        result["negativeInfinity"] = ExportReading(double.NegativeInfinity);
        return result;
    }

    public static Dictionary<string, string> ConstructorContracts()
    {
        var result = new Dictionary<string, string>();
        result["constructorDefaults"] = JsonSerializer.Deserialize<ProvisioningDefaults>("{}")!.Label;
        try { JsonSerializer.Deserialize<ProvisioningAttributedDefaults>("{}"); result["attributedDefaults"] = "accepted"; }
        catch (Exception) { result["attributedDefaults"] = "rejected"; }
        try { JsonSerializer.Deserialize<ProvisioningRecord>("{}"); result["record"] = "accepted"; }
        catch (Exception) { result["record"] = "rejected"; }
        try { JsonSerializer.Deserialize<ProvisioningPositionalRecord>("{\"Name\":\"alpha\"}"); result["positionalRecord"] = "accepted"; }
        catch (Exception) { result["positionalRecord"] = "rejected"; }
        result["validRecord"] = JsonSerializer.Deserialize<ProvisioningRecord>("{\"Enabled\":false}")!.Enabled ? "enabled" : "disabled";
        return result;
    }

    private static string ExportReading(double reading)
    {
        try { return JsonSerializer.Serialize(new { Reading = reading }); }
        catch (Exception) { return "rejected"; }
    }

    public static string[] ProvisioningCases() =>
    [
        "{}",
        "{\"TenantId\":\"alpha\"}",
        "{\"Enabled\":false}",
        "{\"TenantId\":\"alpha\",\"Enabled\":false}",
        "{\"TenantId\":null,\"Enabled\":true}",
        "{\"tenantId\":\"alpha\",\"enabled\":true}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Callback\":{}}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Callback\":{\"Endpoint\":\"https://callback.test\"}}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Callback\":{\"Endpoint\":null}}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Callback\":null}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Nodes\":[{}]}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Nodes\":[{\"Endpoint\":\"https://node.test\"}]}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-02-28T10:00:00Z\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-02-28T10:00:00.Z\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-02-28T10:00:00.+00:00\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-02-30T10:00:00Z\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-04-31\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"0000-01-01\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-01-01T24:00:00Z\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2026-01-01T10:00:00+14:01\"}",
        "{\"TenantId\":\"alpha\",\"Enabled\":true,\"Deadline\":\"2024-02-29T10:00:00Z\"}"
    ];

    public static ProvisioningResult Provision(string json)
    {
        try
        {
            var input = JsonSerializer.Deserialize<ProvisioningInput>(json);
            if (input is null || string.IsNullOrEmpty(input.TenantId))
                return new ProvisioningResult(400, "", false, 0, "");
            return new ProvisioningResult(201, input.TenantId, input.Enabled,
                input.Nodes.Length, input.Deadline?.ToString("O") ?? "");
        }
        catch (Exception)
        {
            return new ProvisioningResult(400, "", false, 0, "");
        }
    }
}

public sealed class ProvisioningInput
{
    public required string TenantId { get; init; }
    public required bool Enabled { get; init; }
    public ProvisioningEndpoint? Callback { get; init; }
    public ProvisioningEndpoint[] Nodes { get; init; } = [];
    public DateTimeOffset? Deadline { get; init; }
}

public sealed class ProvisioningEndpoint
{
    [JsonRequired]
    public string? Endpoint { get; init; }
}

public sealed record ProvisioningResult(int Status, string TenantId, bool Enabled, int NodeCount, string Deadline);

public sealed class ProvisioningDefaults
{
    public required string Label { get; init; }
    [SetsRequiredMembers]
    public ProvisioningDefaults() { Label = "default"; }
}

public sealed class ProvisioningAttributedDefaults
{
    [JsonRequired]
    public required string Label { get; init; }
    [SetsRequiredMembers]
    public ProvisioningAttributedDefaults() { Label = "default"; }
}

public sealed record ProvisioningRecord
{
    public required bool Enabled { get; init; }
}

public sealed record ProvisioningPositionalRecord(string Name)
{
    public required bool Enabled { get; init; }
}
