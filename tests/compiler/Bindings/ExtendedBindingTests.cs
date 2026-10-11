namespace Workers.Compiler.Tests;

public sealed class ExtendedBindingTests
{
    [Fact]
    public void LowersExtendedBindingFamiliesWithoutGenericFallback()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var service = env.DispatchNamespace("DISPATCH").Get("service");
                    var upstream = await service.FetchAsync("https://example.com");
                    env.AnalyticsEngine("EVENTS").WriteDataPoint(
                        new AnalyticsEngineDataPoint { Indexes = ["index"], Doubles = [1.0], Blobs = ["blob"] });
                    var sent = await env.SendEmail("MAIL").SendAsync(new EmailMessageBuilder
                    {
                        From = "from@example.com", To = ["to@example.com"], Subject = "subject", Text = "text"
                    });
                    var raw = await env.SendEmail("MAIL").SendAsync(new EmailMessage("from@example.com", "to@example.com", "raw"));
                    var version = env.VersionMetadata("VERSION").Id;
                    var ai = await env.Ai("AI").RunAsync<object, object>("model", new { prompt = "hello" });
                    var workflow = await env.Workflow("FLOW").CreateAsync(
                        new WorkflowInstanceCreateOptions { Id = "job" });
                    var info = await env.Images("IMAGES").InfoAsync(request.Body!);
                    var contentType = await env.Media("MEDIA").Input(request.Body!)
                        .Output(new MediaTransformationOutputOptions { Mode = "video" }).ContentTypeAsync();
                    var matches = await env.Vectorize("INDEX").QueryAsync([1.0, 2.0],
                        new VectorizeQueryOptions { TopK = 3 });
                    var secret = await env.SecretsStoreSecret("SECRETS").GetAsync();
                    var database = env.Hyperdrive("DB").ConnectionString;
                    return upstream;
                }
            }
            """);

        Assert.Contains("env[\"DISPATCH\"].get(\"service\")", module);
        Assert.Contains("env[\"EVENTS\"].writeDataPoint({ indexes: [\"index\"], doubles: [1], blobs: [\"blob\"] })", module);
        Assert.Contains("env[\"MAIL\"].send({ from: \"from@example.com\", to: [\"to@example.com\"], subject: \"subject\", text: \"text\" })", module);
        Assert.Contains("import { EmailMessage as $workers$EmailMessage } from \"cloudflare:email\";", module);
        Assert.Contains("env[\"MAIL\"].send(new $workers$EmailMessage(\"from@example.com\", \"to@example.com\", \"raw\"))", module);
        Assert.Contains("let version = env[\"VERSION\"].id;", module);
        Assert.Contains("env[\"AI\"].run(\"model\", { prompt: \"hello\" })", module);
        Assert.Contains("env[\"FLOW\"].create({ id: \"job\" })", module);
        Assert.Contains("env[\"IMAGES\"].info(request.body)", module);
        Assert.Contains("env[\"MEDIA\"].input(request.body).output({ mode: \"video\" }).contentType()", module);
        Assert.Contains("env[\"INDEX\"].query([1, 2], { topK: 3 })", module);
        Assert.Contains("env[\"SECRETS\"].get()", module);
        Assert.Contains("let database = env[\"DB\"].connectionString;", module);
    }

}
