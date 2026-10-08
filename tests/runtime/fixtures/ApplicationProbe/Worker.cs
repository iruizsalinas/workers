using Workers;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, Context context)
    {
        if (request.Path == "/json-audit")
            return Response.Json(ApplicationScenarios.AuditJson(await request.TextAsync()));
        if (request.Path == "/checkout")
        {
            var result = ApplicationScenarios.Checkout(await request.TextAsync());
            return Response.Json(result, result.Status);
        }
        if (request.Path == "/shipments")
        {
            var result = ApplicationScenarios.Shipment(await request.TextAsync());
            if (result.Status == 202)
            {
                var id = Guid.NewGuid().ToString();
                await env.D1("DB").Prepare("INSERT INTO probe_shipments (id, total, priority, delay) VALUES (?, ?, ?, ?)")
                    .Bind(id, result.TotalCents, result.Priority, result.DelaySeconds).RunAsync();
                context.WaitUntil(env.Kv("KV").PutJsonAsync($"shipment:{id}", result));
                return Response.Json(result, 202).WithHeader("location", $"/shipments/{id}");
            }
            return Response.Json(result, result.Status);
        }
        if (request.Path.StartsWith("/shipments/") && request.Method == "GET")
        {
            var cached = await env.Kv("KV").GetJsonAsync<ShipmentResult>($"shipment:{request.Path.Substring(11)}");
            return cached is null ? Response.Empty(404) : Response.Json(cached);
        }
        if (request.Path == "/inventory")
            return Response.Json(ApplicationScenarios.Inventory());
        if (request.Path == "/reporting")
            return Response.Json(ApplicationScenarios.Reporting());
        return Response.Text("Not found", 404);
    }
}
