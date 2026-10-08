using Workers;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, Context context)
    {
        if (request.Path == "/json-audit")
            return Response.Json(ApplicationScenarios.AuditJson(await request.TextAsync()));
        if (request.Path == "/telemetry-export")
            return Response.Json(JsonApplicationScenarios.TelemetryExports());
        if (request.Path == "/constructor-contracts")
            return Response.Json(JsonApplicationScenarios.ConstructorContracts());
        if (request.Path == "/language")
            return Response.Json(ApplicationLanguageScenarios.Run());
        if (request.Path == "/verification-json")
            return Response.Json(VerificationJsonScenarios.Run());
        if (request.Path == "/numeric-verification")
            return Response.Json(VerificationNumericScenarios.Run());
        if (request.Path == "/collection-verification")
            return Response.Json(VerificationCollectionScenarios.Run());
        if (request.Path == "/verification-config" && request.Method == "POST")
        {
            try
            {
                var input = await request.JsonAsync<VerificationConfigInput>();
                var result = VerificationJsonScenarios.Summarize(input);
                if (result.Status == 201)
                    await env.Kv("KV").PutJsonAsync($"verification-config:{result.Name}", input);
                return Response.Json(result, result.Status);
            }
            catch (Exception)
            {
                return Response.Json(VerificationJsonScenarios.Rejected(), 400);
            }
        }
        if (request.Path.StartsWith("/verification-config/") && request.Method == "GET")
        {
            var input = await env.Kv("KV").GetJsonAsync<VerificationConfigInput>(
                $"verification-config:{request.Path.Substring(21)}");
            return input is null ? Response.Empty(404) : Response.Json(VerificationJsonScenarios.Summarize(input));
        }
        if (request.Path == "/provisioning")
        {
            var result = JsonApplicationScenarios.Provision(await request.TextAsync());
            if (result.Status == 201)
                await env.D1("DB").Prepare("INSERT INTO probe_tenants (id, enabled) VALUES (?, ?)")
                    .Bind(result.TenantId, result.Enabled ? 1 : 0).RunAsync();
            return Response.Json(result, result.Status);
        }
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
        if (request.Path == "/collection-applications")
            return Response.Json(CollectionScenarios.Run());
        if (request.Path == "/regressions")
            return Response.Json(RegressionScenarios.Run());
        if (request.Path == "/regex")
            return Response.Json(RegexScenarios.Run());
        return Response.Text("Not found", 404);
    }
}
