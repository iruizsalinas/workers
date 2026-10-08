using System.Text.Json;

object result = args.Contains("--applications")
    ? new
    {
        Inventory = ApplicationScenarios.Inventory(),
        Reporting = ApplicationScenarios.Reporting(),
        Regressions = RegressionScenarios.Run(),
        JsonAudit = ApplicationScenarios.JsonAuditCases()
            .Select(json => new { Json = json, Result = ApplicationScenarios.AuditJson(json) }).ToArray(),
        Shipments = ApplicationScenarios.ShipmentCases()
            .Select(json => new { Json = json, Result = ApplicationScenarios.Shipment(json) }).ToArray(),
        Checkout = new[]
        {
            "{\"Sku\":\"widget\",\"Quantity\":2,\"UnitPriceCents\":125,\"Tags\":[\"gift\"]}",
            "{\"Sku\":\"widget\",\"Quantity\":\"2\",\"UnitPriceCents\":125,\"Tags\":[]}",
            "{\"Sku\":\"widget\",\"Quantity\":1.5,\"UnitPriceCents\":125,\"Tags\":[]}",
            "{\"Sku\":\"widget\",\"Quantity\":1,\"UnitPriceCents\":2147483648,\"Tags\":[]}",
            "{\"Sku\":\"widget\",\"Quantity\":2,\"UnitPriceCents\":null,\"Tags\":[]}",
            "{\"Sku\":123,\"Quantity\":2,\"UnitPriceCents\":125,\"Tags\":[]}",
            "{\"Sku\":\"widget\",\"Quantity\":2,\"UnitPriceCents\":125,\"Tags\":\"gift\"}",
            "{\"Sku\":\"widget\",\"Quantity\":2,\"UnitPriceCents\":125,\"Tags\":[123]}",
            "{\"Sku\":\"widget\",\"UnitPriceCents\":125,\"Tags\":[]}",
            "null"
        }.Select(json => new { Json = json, Result = ApplicationScenarios.Checkout(json) }).ToArray()
    }
    : CoreSemantics.Run();
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}));
