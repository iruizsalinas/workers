// Application decisions compiled both by the CLR and by Workers. The callbacks model
// validation and audit work that must run once for each row the query actually visits.
public static class CollectionScenarios
{
    public static Dictionary<string, string> Run()
    {
        var results = new Dictionary<string, string>();
        results["nullableDispatchPriority"] = DispatchPriorities();
        results["mergedShipmentValidation"] = MergedShipments();
        results["pairedInvoiceAudit"] = PairedInvoices();
        results["emptyTenantJoinAudit"] = EmptyTenantJoin();
        results["tenantJoinEvaluationOrder"] = TenantJoinOrder();
        results["inventoryBatchImport"] = InventoryBatchImport();
        results["textAndTenantWorkQueues"] = WorkQueues();
        return results;
    }

    private static string DispatchPriorities()
    {
        var shipments = new List<CollectionShipment>
        {
            new("unscheduled", null), new("standard", 5), new("urgent", 1), new("later", null)
        };
        var unknown = new List<CollectionShipment> { new("first", null), new("second", null) };
        var missing = new List<CollectionShipment>();
        return $"{shipments.MinBy(shipment => shipment.Priority)!.Id} "
            + $"{shipments.MaxBy(shipment => shipment.Priority)!.Id} "
            + $"{unknown.MinBy(shipment => shipment.Priority)!.Id} "
            + $"{missing.MinBy(shipment => shipment.Priority) is null}";
    }

    private static string MergedShipments()
    {
        var validated = new List<string>();
        var current = new List<string> { "a", "b", "a" };
        var incoming = new List<string> { "b", "c" };
        var query = current.UnionBy(incoming, id => ValidateShipment(id, validated));
        var before = validated.Count;
        var merged = query.ToArray();
        return $"{before} {string.Join(",", merged)} {string.Join(",", validated)}";
    }

    private static string ValidateShipment(string id, List<string> validated)
    {
        validated.Add(id);
        return id;
    }

    private static string PairedInvoices()
    {
        var audited = new List<int>();
        var invoices = new List<int> { 1, 2, 3 }.Select(id => AuditInvoice(id, audited));
        var payments = new List<int> { 10 };
        var pairs = payments.Zip(invoices, (payment, invoice) => payment + invoice).ToArray();
        var result = $"{string.Join(",", pairs.Select(id => id.ToString()))} "
            + string.Join(",", audited.Select(id => id.ToString()));
        audited.Clear();
        var noPayments = new List<int>();
        var empty = noPayments.Zip(invoices, (payment, invoice) => payment + invoice).ToArray();
        return result + $" | {empty.Length} {audited.Count}";
    }

    private static int AuditInvoice(int id, List<int> audited)
    {
        audited.Add(id);
        return id;
    }

    private static string EmptyTenantJoin()
    {
        var audited = new List<string>();
        var sales = new List<string> { "alpha", "beta" }.Select(id => ValidateShipment(id, audited));
        var tenants = new List<string>();
        var joined = tenants.Join(sales, tenant => tenant, sale => sale, (tenant, sale) => sale).ToArray();
        var result = $"{joined.Length} {audited.Count}";
        audited.Clear();
        var grouped = tenants.GroupJoin(sales, tenant => tenant, sale => sale,
            (tenant, matches) => matches.Count()).ToArray();
        return result + $" | {grouped.Length} {audited.Count}";
    }

    private static string InventoryBatchImport()
    {
        var incoming = new int[180000];
        for (var index = 0; index < incoming.Length; index++) incoming[index] = index;
        var inventory = new List<int>();
        var imported = true;
        try { inventory.AddRange(incoming); }
        catch (Exception) { imported = false; }
        var reservations = new List<int> { 7, 8 };
        reservations.AddRange(reservations);
        var partial = new List<int>();
        try { partial.AddRange(new List<int> { 1, 2, 3 }.Select(ValidateImportRow)); }
        catch (Exception) { }
        return $"{imported} {inventory.Count} {inventory.LastOrDefault()} "
            + string.Join(",", reservations.Select(id => id.ToString())) + " | "
            + string.Join(",", partial.Select(id => id.ToString()));
    }

    private static int ValidateImportRow(int id)
    {
        if (id == 3) throw new InvalidOperationException("Invalid inventory row.");
        return id;
    }

    private static string TenantJoinOrder()
    {
        var events = new List<string>();
        var tenants = new List<string> { "a", "b" }.Select(id => AuditTenant(id, "outer-", events));
        var sales = new List<string> { "a", "b" }.Select(id => AuditTenant(id, "inner-", events));
        var joined = tenants.Join(sales, tenant => AuditTenant(tenant, "key-", events), sale => sale,
            (tenant, sale) => sale).ToArray();
        var result = string.Join(",", joined) + " " + string.Join(",", events);
        events.Clear();
        var noSales = new List<string>();
        var empty = tenants.Join(noSales, tenant => AuditTenant(tenant, "key-", events), sale => sale,
            (tenant, sale) => sale).ToArray();
        result += $" | {empty.Length} {string.Join(",", events)}";
        events.Clear();
        var grouped = tenants.GroupJoin(noSales, tenant => AuditTenant(tenant, "key-", events), sale => sale,
            (tenant, matches) => matches.Count()).ToArray();
        return result + " | " + string.Join(",", grouped.Select(count => count.ToString()))
            + " " + string.Join(",", events);
    }

    private static string AuditTenant(string id, string marker, List<string> events)
    {
        events.Add(marker + id);
        return id;
    }

    private static string WorkQueues()
    {
        var text = "A\uD83D\uDE00B";
        var characters = new Queue<char>(text);
        var reverse = new Stack<char>(text);
        var tenants = new Dictionary<string, int> { ["alpha"] = 2, ["beta"] = 3 };
        var queue = new Queue<KeyValuePair<string, int>>(tenants);
        var stack = new Stack<KeyValuePair<string, int>>(tenants);
        return $"{characters.Count} {reverse.Count} "
            + string.Join(",", characters.Select(character => ((int)character).ToString())) + " | "
            + string.Join(",", reverse.Select(character => ((int)character).ToString())) + " | "
            + string.Join(",", queue.Select(tenant => tenant.Key + "=" + tenant.Value)) + " | "
            + string.Join(",", stack.Select(tenant => tenant.Key + "=" + tenant.Value));
    }
}

public sealed record CollectionShipment(string Id, int? Priority);
