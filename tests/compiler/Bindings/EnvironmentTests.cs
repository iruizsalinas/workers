namespace Workers.Compiler.Tests;

public sealed class EnvironmentTests
{
    [Theory]
    [InlineData("Get<KVNamespace>")]
    [InlineData("Variable")]
    [InlineData("Secret")]
    [InlineData("Raw")]
    [InlineData("KV")]
    [InlineData("R2")]
    [InlineData("Queue")]
    [InlineData("D1")]
    [InlineData("Service")]
    [InlineData("Assets")]
    [InlineData("MtlsCertificate")]
    [InlineData("DispatchNamespace")]
    [InlineData("DurableObject")]
    [InlineData("RateLimit")]
    [InlineData("AnalyticsEngine")]
    [InlineData("SendEmail")]
    [InlineData("VersionMetadata")]
    [InlineData("Ai")]
    [InlineData("Workflow")]
    [InlineData("Images")]
    [InlineData("Media")]
    [InlineData("Vectorize")]
    [InlineData("SecretsStoreSecret")]
    [InlineData("Hyperdrive")]
    public void ErasesBindingAccessorsToNativeEnvironmentLookup(string accessor)
    {
        var module = Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var binding = env.{{accessor}}("BINDING");
                    return new Response("ok");
                }
            }
            """);

        Assert.Contains("let binding = $workers$notNull(env)[\"BINDING\"];", module);
    }

}
