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
                public static async Task<Response> Fetch(Request request, Env env, Context ctx)
                {
                    var service = env.Dispatcher("DISPATCH").Get("service");
                    var upstream = await service.FetchAsync("https://example.com");
                    env.Analytics("EVENTS").WriteDataPoint(
                        new AnalyticsEngineDataPoint(["index"], [1.0], ["blob"]));
                    var sent = await env.Email("MAIL").SendAsync(
                        new SendEmailMessage("from@example.com", ["to@example.com"], "subject", "text"));
                    var version = await env.Version("VERSION").GetAsync();
                    var ai = await env.Ai("AI").RunAsync<object, object>("model", new { prompt = "hello" });
                    var workflow = await env.Workflow("FLOW").CreateAsync(
                        new WorkflowInstanceCreateOptions { Id = "job" });
                    var info = await env.Images("IMAGES").InfoAsync(request.Body);
                    var contentType = await env.Media("MEDIA").Input(request.Body)
                        .Output(new MediaOutputOptions { Mode = "video" }).ContentTypeAsync();
                    var matches = await env.Vectorize("INDEX").QueryAsync([1.0, 2.0],
                        new VectorizeQueryOptions { TopK = 3 });
                    var secret = await env.SecretStore("SECRETS").GetAsync();
                    var database = await env.Hyperdrive("DB").GetConnectionInfoAsync();
                    return upstream;
                }
            }
            """);

        Assert.Contains("env[\"DISPATCH\"].get(\"service\")", module);
        Assert.Contains("env[\"EVENTS\"].writeDataPoint({ indexes: [\"index\"], doubles: [1], blobs: [\"blob\"] })", module);
        Assert.Contains("env[\"MAIL\"].send({ from: \"from@example.com\", to: [\"to@example.com\"], subject: \"subject\", text: \"text\" })", module);
        Assert.Contains("await env[\"VERSION\"]", module);
        Assert.Contains("env[\"AI\"].run(\"model\", { prompt: \"hello\" })", module);
        Assert.Contains("env[\"FLOW\"].create({ id: \"job\" })", module);
        Assert.Contains("env[\"IMAGES\"].info(request.body)", module);
        Assert.Contains("env[\"MEDIA\"].input(request.body).output({ mode: \"video\" }).contentType()", module);
        Assert.Contains("env[\"INDEX\"].query([1, 2], { topK: 3 })", module);
        Assert.Contains("env[\"SECRETS\"].get()", module);
        Assert.Contains("await env[\"DB\"]", module);
    }

}
