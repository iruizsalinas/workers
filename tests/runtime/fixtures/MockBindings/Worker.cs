using Workers;

namespace MockBindings;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        ExecutionContext context)
    {
        var path = new Url(request.Url).Pathname;
        if (path == "/typed-json-bytes")
            return new Response(new TextDecoder().Decode((await request.JsonAsync<byte[]>())!));
        if (path == "/ai-typed")
        {
            var result = await environment.Ai("AI").RunAsync<AiInput, AiOutput>("model-a", new AiInput("hello"));
            return new Response(result!.Format());
        }
        if (path == "/service")
            return await environment.Service("SERVICE").FetchAsync("https://service.test/request");

        if (path == "/assets")
            return await environment.Assets("ASSETS").FetchAsync("https://assets.test/logo.svg");

        if (path == "/mtls")
            return await environment.MtlsCertificate("MTLS").FetchAsync("https://mtls.test/private");

        if (path == "/dynamic")
            return await environment.DispatchNamespace("DISPATCH").Get("tenant-a").FetchAsync("https://tenant.test/request");

        if (path == "/rate")
        {
            var outcome = await environment.RateLimit("RATE").LimitAsync(new RateLimitOptions { Key = "user-42" });
            return Response.Json(new { success = outcome.Success });
        }

        if (path == "/analytics")
        {
            environment.AnalyticsEngine("ANALYTICS").WriteDataPoint(
                new AnalyticsEngineDataPoint { Indexes = ["tenant-a"], Doubles = [1.5], Blobs = ["request"] });
            return new Response("written");
        }

        if (path == "/email")
        {
            var result = await environment.SendEmail("EMAIL").SendAsync(new EmailMessageBuilder
            {
                From = "from@example.test", To = ["to@example.test"], Subject = "Subject", Text = "Body"
            });
            return new Response(result.MessageId);
        }

        if (path == "/metadata")
        {
            var metadata = environment.VersionMetadata("VERSION");
            return Response.Json(new { id = metadata.Id, tag = metadata.Tag });
        }

        if (path == "/ai")
        {
            var result = await environment.Ai("AI").RunAsync<object, string>("model-a", new { prompt = "hello" });
            return new Response(result ?? "missing");
        }

        if (path == "/workflow")
        {
            var instance = await environment.Workflow("WORKFLOW").CreateAsync(
                new WorkflowInstanceCreateOptions { Id = "workflow-1", Params = new { value = 42 } });
            var status = await instance.StatusAsync();
            return Response.Json(new { id = instance.Id, status = status.Status });
        }

        if (path == "/images")
        {
            var info = await environment.Images("IMAGES").InfoAsync(request.Body!);
            return Response.Json(new { format = info.Format, width = info.Width, height = info.Height });
        }

        if (path == "/media")
        {
            var output = environment.Media("MEDIA")
                .Input(request.Body!)
                .Transform(new { width = 320 })
                .Output(new MediaTransformationOutputOptions { Mode = "video", Format = "mp4" });
            return new Response(await output.ContentTypeAsync());
        }

        if (path == "/vectorize")
        {
            var result = await environment.Vectorize("VECTORIZE").QueryAsync(
                [0.25, 0.75],
                new VectorizeQueryOptions { TopK = 3, ReturnValues = true });
            return Response.Json(new { matches = result.Matches });
        }

        if (path == "/secret")
        {
            var secret = await environment.SecretsStoreSecret("SECRET").GetAsync();
            return new Response(secret ?? "missing");
        }

        if (path == "/hyperdrive")
        {
            var connection = environment.Hyperdrive("HYPERDRIVE");
            return Response.Json(new { host = connection.Host, port = connection.Port, database = connection.Database });
        }

        var greeting = await environment.Service("SERVICE").InvokeAsync<string>("greet", "Ada");
        return new Response(greeting ?? "missing");
    }
}

public sealed record AiInput(string Prompt);
public sealed class AiOutput
{
    [JsonName("answer_text")] public string Answer { get; init; } = "";
    public string Format() => Answer.ToUpperInvariant();
}
