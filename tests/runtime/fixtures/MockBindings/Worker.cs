using Workers;

namespace MockBindings;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Path == "/service")
            return await environment.Service("SERVICE").FetchAsync("https://service.test/request");

        if (request.Path == "/assets")
            return await environment.Assets("ASSETS").FetchAsync("https://assets.test/logo.svg");

        if (request.Path == "/mtls")
            return await environment.Mtls("MTLS").FetchAsync("https://mtls.test/private");

        if (request.Path == "/dynamic")
            return await environment.Dispatcher("DISPATCH").Get("tenant-a").FetchAsync("https://tenant.test/request");

        if (request.Path == "/rate")
        {
            var outcome = await environment.RateLimiter("RATE").LimitAsync("user-42");
            return Response.Json(new { success = outcome.Success });
        }

        if (request.Path == "/analytics")
        {
            environment.Analytics("ANALYTICS").WriteDataPoint(
                new AnalyticsEngineDataPoint(["tenant-a"], [1.5], ["request"]));
            return Response.Text("written");
        }

        if (request.Path == "/email")
        {
            var result = await environment.Email("EMAIL").SendAsync(
                new SendEmailMessage("from@example.test", ["to@example.test"], "Subject", "Body"));
            return Response.Text(result.MessageId);
        }

        if (request.Path == "/metadata")
        {
            var metadata = await environment.Version("VERSION").GetAsync();
            return Response.Json(new { id = metadata.Id, tag = metadata.Tag });
        }

        if (request.Path == "/ai")
        {
            var result = await environment.Ai("AI").RunAsync<object, string>("model-a", new { prompt = "hello" });
            return Response.Text(result ?? "missing");
        }

        if (request.Path == "/workflow")
        {
            var instance = await environment.Workflow("WORKFLOW").CreateAsync(
                new WorkflowInstanceCreateOptions { Id = "workflow-1", Params = new { value = 42 } });
            var status = await instance.StatusAsync();
            return Response.Json(new { id = instance.Id, status = status.Status });
        }

        if (request.Path == "/images")
        {
            var info = await environment.Images("IMAGES").InfoAsync(request.Body);
            return Response.Json(new { format = info.Format, width = info.Width, height = info.Height });
        }

        if (request.Path == "/media")
        {
            var output = environment.Media("MEDIA")
                .Input(request.Body)
                .Transform(new { width = 320 })
                .Output(new MediaOutputOptions { Mode = "video", Format = "mp4" });
            return Response.Text(await output.ContentTypeAsync());
        }

        if (request.Path == "/vectorize")
        {
            var result = await environment.Vectorize("VECTORIZE").QueryAsync(
                [0.25, 0.75],
                new VectorizeQueryOptions { TopK = 3, ReturnValues = true });
            return Response.Json(new { matches = result.Matches });
        }

        if (request.Path == "/secret")
        {
            var secret = await environment.SecretStore("SECRET").GetAsync();
            return Response.Text(secret ?? "missing");
        }

        if (request.Path == "/hyperdrive")
        {
            var connection = await environment.Hyperdrive("HYPERDRIVE").GetConnectionInfoAsync();
            return Response.Json(new { host = connection.Host, port = connection.Port, database = connection.Database });
        }

        var greeting = await environment.Service("SERVICE").InvokeAsync<string>("greet", ["Ada"]);
        return Response.Text(greeting ?? "missing");
    }
}
