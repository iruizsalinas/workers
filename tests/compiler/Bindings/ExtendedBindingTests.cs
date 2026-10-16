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

        Assert.Contains("$workers$notNull($workers$notNull(env)[\"DISPATCH\"]).get(\"service\")", module);
        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).writeDataPoint($workers$arg1))($workers$notNull(env)[\"EVENTS\"], { indexes: [\"index\"], doubles: [1], blobs: [\"blob\"] })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).send($workers$arg1$2))($workers$notNull(env)[\"MAIL\"], { from: \"from@example.com\", to: [\"to@example.com\"], subject: \"subject\", text: \"text\" })", module);
        Assert.Contains("import { EmailMessage as $workers$EmailMessage } from \"cloudflare:email\";", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3) => $workers$notNull($workers$receiver$3).send($workers$arg1$3))($workers$notNull(env)[\"MAIL\"], new $workers$EmailMessage(\"from@example.com\", \"to@example.com\", \"raw\"))", module);
        Assert.Contains("let version = $workers$notNull($workers$notNull(env)[\"VERSION\"]).id;", module);
        Assert.Contains("(($workers$receiver$4, $workers$arg1$4, $workers$arg2) => $workers$notNull($workers$receiver$4).run($workers$arg1$4, $workers$arg2))($workers$notNull(env)[\"AI\"], \"model\", { prompt: \"hello\" })", module);
        Assert.Contains("(($workers$receiver$5, $workers$arg1$5) => $workers$notNull($workers$receiver$5).create($workers$arg1$5))($workers$notNull(env)[\"FLOW\"], { id: \"job\" })", module);
        Assert.Contains("(($workers$receiver$6, $workers$arg1$6) => $workers$notNull($workers$receiver$6).info($workers$arg1$6))($workers$notNull(env)[\"IMAGES\"], $workers$notNull(request).body)", module);
        Assert.Contains("$workers$notNull((($workers$receiver$7, $workers$arg1$7) => $workers$notNull($workers$receiver$7).output($workers$arg1$7))((($workers$receiver$7, $workers$arg1$7) => $workers$notNull($workers$receiver$7).input($workers$arg1$7))($workers$notNull(env)[\"MEDIA\"], $workers$notNull(request).body), { mode: \"video\" })).contentType()", module);
        Assert.Contains("(($workers$receiver$8, $workers$arg1$8, $workers$arg2$2) => $workers$notNull($workers$receiver$8).query($workers$arg1$8, $workers$arg2$2))($workers$notNull(env)[\"INDEX\"], [1, 2], { topK: 3 })", module);
        Assert.Contains("$workers$notNull($workers$notNull(env)[\"SECRETS\"]).get()", module);
        Assert.Contains("let database = $workers$notNull($workers$notNull(env)[\"DB\"]).connectionString;", module);
    }

}
