namespace Workers.Compiler.Tests;

public sealed class EnvironmentTests
{
    [Theory]
    [InlineData("Variable")]
    [InlineData("Secret")]
    [InlineData("Raw")]
    [InlineData("Kv")]
    [InlineData("R2")]
    [InlineData("Queue")]
    [InlineData("D1")]
    [InlineData("Service")]
    [InlineData("Assets")]
    [InlineData("Mtls")]
    [InlineData("Dispatcher")]
    [InlineData("DurableObject")]
    [InlineData("RateLimiter")]
    [InlineData("Analytics")]
    [InlineData("Email")]
    [InlineData("Version")]
    [InlineData("Ai")]
    [InlineData("Workflow")]
    [InlineData("Images")]
    [InlineData("Media")]
    [InlineData("Vectorize")]
    [InlineData("SecretStore")]
    [InlineData("Hyperdrive")]
    public void ErasesBindingAccessorsToNativeEnvironmentLookup(string accessor)
    {
        var module = Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var binding = env.{{accessor}}("BINDING");
                    return Response.Text("ok");
                }
            }
            """);

        Assert.Contains("let binding = env[\"BINDING\"];", module);
    }

}
