using System.Text.Json;
using System.Text.Json.Serialization;

// These routines are compiled both by the CLR and by Workers. They model business
// decisions, so a mismatch affects the application's externally visible behavior.
public static class ApplicationScenarios
{
    public static string[] JsonAuditCases() =>
    [
        "{}",
        "{\"Text\":null,\"Limit\":null,\"Tags\":null,\"Matrix\":null,\"Children\":null,\"Data\":null}",
        "{\"Text\":\"ok\",\"Flag\":false,\"Count\":-2147483648,\"Unsigned\":4294967295,\"Amount\":1.5,\"Ratio\":0.1,\"Marker\":\"a\",\"Limit\":0,\"Tags\":[null,\"x\"],\"Matrix\":[[1,null],null,[]],\"Children\":[{\"Label\":\"child\"}],\"Data\":\"AQID\",\"Defaults\":{}}",
        "{\"Defaults\":{\"Count\":0,\"Label\":null,\"Enabled\":false,\"Ignored\":99}}",
        "{\"Text\":1}",
        "{\"Flag\":null}",
        "{\"Flag\":\"true\"}",
        "{\"Count\":2147483648}",
        "{\"Count\":-2147483649}",
        "{\"Unsigned\":-1}",
        "{\"Unsigned\":4294967296}",
        "{\"Amount\":\"1.5\"}",
        "{\"Ratio\":null}",
        "{\"Marker\":\"\"}",
        "{\"Marker\":\"ab\"}",
        "{\"Limit\":1.5}",
        "{\"Tags\":{\"length\":0}}",
        "{\"Matrix\":[[\"1\"]]}",
        "{\"Children\":[1]}",
        "{\"Data\":[1,2,3]}",
        "{\"Data\":\"AQI\"}",
        "{\"Data\":\"AQI=\"}",
        "{\"Data\":\"\"}",
        "{\"Data\":\" AQ I= \"}",
        "{\"Data\":\"AQI===\"}",
        "{\"Data\":\"AQ_I\"}",
        "{\"Data\":\"AQI=\\f\"}"
    ];

    public static JsonAuditResult AuditJson(string json)
    {
        try
        {
            var input = JsonSerializer.Deserialize<JsonAuditInput>(json)!;
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, int>>("{\"__proto__\":5,\"widget\":9}")!;
            return new JsonAuditResult(true, JsonSerializer.Serialize(new
            {
                text = input.Text, flag = input.Flag, count = input.Count,
                unsigned = input.Unsigned, amount = input.Amount,
                ratioMatches = input.Ratio == 0.1f, marker = input.Marker, limit = input.Limit,
                tags = input.Tags, matrix = input.Matrix,
                children = input.Children is null ? null : input.Children.Select(child => child.Label).ToArray(),
                data = input.Data is null ? null : Convert.ToHexString(input.Data),
                defaultCount = input.Defaults.Count, defaultLabel = input.Defaults.Label,
                defaultEnabled = input.Defaults.Enabled, ignored = input.Defaults.Ignored,
                defaultRatioMatches = input.Defaults.Ratio == JsonAuditSettings.ExpectedRatio,
                dictionaryTotal = dictionary["__proto__"] + dictionary["widget"]
            }));
        }
        catch (Exception)
        {
            return new JsonAuditResult(false, "");
        }
    }

    public static string[] ShipmentCases() =>
    [
        "{\"Account\":{\"Enabled\":true,\"CreditCents\":500},\"Lines\":[{\"Sku\":\"widget\",\"Quantity\":2,\"PriceCents\":125}]}",
        "{\"Account\":{\"Enabled\":true,\"CreditCents\":500},\"Lines\":[{\"Sku\":\"widget\",\"Quantity\":2,\"PriceCents\":125}],\"Priority\":null,\"DelaySeconds\":5}",
        "{\"Account\":{\"Enabled\":false,\"CreditCents\":500},\"Lines\":[]}",
        "{\"Account\":{\"Enabled\":\"false\",\"CreditCents\":500},\"Lines\":[]}",
        "{\"Account\":{\"Enabled\":true,\"CreditCents\":500},\"Lines\":[{\"Sku\":\"widget\",\"Quantity\":\"2\",\"PriceCents\":125}]}",
        "{\"Account\":{\"Enabled\":true,\"CreditCents\":500},\"Lines\":{\"length\":0}}",
        "{\"Account\":{\"Enabled\":true,\"CreditCents\":500},\"Lines\":[],\"Priority\":\"urgent\"}",
        "{\"Account\":null,\"Lines\":[]}",
        "{\"Account\":{\"Enabled\":true,\"CreditCents\":200},\"Lines\":[{\"Sku\":\"widget\",\"Quantity\":2,\"PriceCents\":125}]}"
    ];

    public static ShipmentResult Shipment(string json)
    {
        try
        {
            var input = JsonSerializer.Deserialize<ShipmentInput>(json);
            if (input is null || input.Account is null || !input.Account.Enabled || input.Lines is null
                || input.Lines.Any(line => line is null || line.Quantity < 1 || line.PriceCents < 0))
                return new ShipmentResult(400, 0, 0, 0);
            var total = input.Lines.Sum(line => line.Quantity * line.PriceCents);
            if (total > input.Account.CreditCents)
                return new ShipmentResult(409, total, 0, 0);
            return new ShipmentResult(202, total, input.Priority ?? 0, input.DelaySeconds);
        }
        catch (Exception)
        {
            return new ShipmentResult(400, 0, 0, 0);
        }
    }

    public static CheckoutResult Checkout(string json)
    {
        try
        {
            var input = JsonSerializer.Deserialize<CheckoutInput>(json);
            if (input is null || input.Quantity < 1 || input.Quantity > 100
                || input.UnitPriceCents < 0 || input.Sku is null || input.Sku.Length == 0
                || input.Tags is null)
                return new CheckoutResult(400, 0, 0);
            return new CheckoutResult(201, input.Quantity * input.UnitPriceCents, input.Tags.Length);
        }
        catch (Exception)
        {
            return new CheckoutResult(400, 0, 0);
        }
    }

    public static InventoryResult Inventory()
    {
        var stock = new Dictionary<string, int> { ["widget"] = 5 };
        var nullKeyRejected = false;
        try { stock[null!] = 100; }
        catch (Exception) { nullKeyRejected = true; }
        var pending = new List<int> { 10, 20, 30 };
        var snapshot = Enumerable.ToArray(pending);
        pending[1] = 25;
        var invalidWriteRejected = false;
        try { pending[10] = 40; }
        catch (Exception) { invalidWriteRejected = true; }
        stock["widget"] = stock["widget"] - 2;
        return new InventoryResult(stock["widget"], stock.Count,
            nullKeyRejected, invalidWriteRejected, Enumerable.ToArray(pending), snapshot);
    }

    public static ReportingResult Reporting()
    {
        var sales = new List<Sale>
        {
            new Sale("alpha", 100, true),
            new Sale(null, 20, true),
            new Sale("alpha", 50, false),
            new Sale("beta", 80, true),
            new Sale(null, 30, true)
        };
        var paid = sales.Where(sale => sale.Paid);
        // Reports must observe new rows when a deferred query is enumerated.
        sales.Add(new Sale("alpha", 25, true));
        var totals = paid.GroupBy(sale => sale.Tenant)
            .Select(group => new TenantTotal(group.Key, group.Sum(sale => sale.Cents)))
            .OrderByDescending(total => total.Cents).ToArray();
        string?[] tenants = ["alpha", null, "beta"];
        var joined = paid.Join(tenants, sale => sale.Tenant, tenant => tenant,
            (sale, tenant) => sale.Cents).ToArray();
        var grouped = tenants.GroupJoin(paid, tenant => tenant, sale => sale.Tenant,
            (tenant, matches) => new TenantTotal(tenant, matches.Sum(sale => sale.Cents))).ToArray();
        return new ReportingResult(paid.Count(), totals, joined, grouped);
    }
}

public sealed record CheckoutInput(string Sku, int Quantity, int UnitPriceCents, string[] Tags);
public sealed record CheckoutResult(int Status, int TotalCents, int TagCount);
public sealed record InventoryResult(int Stock, int KeyCount,
    bool NullKeyRejected, bool InvalidWriteRejected, int[] Pending, int[] Snapshot);
public sealed record Sale(string? Tenant, int Cents, bool Paid);
public sealed record TenantTotal(string? Tenant, int Cents);
public sealed record ReportingResult(int PaidCount, TenantTotal[] Totals, int[] Joined, TenantTotal[] Grouped);
public sealed record ShipmentInput(ShipmentAccount Account, List<ShipmentLine> Lines,
    int? Priority = null, int DelaySeconds = 30);
public sealed class ShipmentAccount
{
    public bool Enabled { get; init; }
    public int CreditCents { get; init; }
}
public sealed record ShipmentLine(string Sku, int Quantity, int PriceCents);
public sealed record ShipmentResult(int Status, int TotalCents, int Priority, int DelaySeconds);
public sealed record JsonAuditResult(bool Accepted, string Detail);
public sealed class JsonAuditInput
{
    public string? Text { get; init; } = "seed";
    public bool Flag { get; init; } = true;
    public int Count { get; init; } = 1;
    public uint Unsigned { get; init; } = 2;
    public double Amount { get; init; } = 1.5;
    public float Ratio { get; init; } = 0.1f;
    public char Marker { get; init; } = 'X';
    public int? Limit { get; init; } = 3;
    public string?[]? Tags { get; init; } = ["initial"];
    public int?[]?[]? Matrix { get; init; }
    public List<JsonAuditChild>? Children { get; init; }
    public byte[]? Data { get; init; }
    public JsonAuditDefaults Defaults { get; init; } = new JsonAuditDefaults();
}
public sealed record JsonAuditChild(string Label = "child");
public sealed record JsonAuditDefaults(int Count = 7, string? Label = "default", bool Enabled = true,
    [property: JsonIgnore] int Ignored = 11, float Ratio = 0.1f);
public static class JsonAuditSettings
{
    public const float ExpectedRatio = 0.1f;
}
