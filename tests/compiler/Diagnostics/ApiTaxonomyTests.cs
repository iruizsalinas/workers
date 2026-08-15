namespace Workers.Compiler.Tests;

public sealed class ApiTaxonomyTests
{
    [Fact]
    public void EnvironmentContainsOnlyConfiguredValuesAndBindings()
    {
        var methods = typeof(global::Workers.Env)
            .GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Cache", methods);
        Assert.DoesNotContain("Crypto", methods);
        Assert.DoesNotContain("DelayAsync", methods);
        Assert.DoesNotContain("FetchAsync", methods);
        Assert.DoesNotContain("HtmlRewriter", methods);
        Assert.DoesNotContain("Log", methods);
        Assert.DoesNotContain("ConnectSocketAsync", methods);
        Assert.DoesNotContain("WebSocketPairAsync", methods);
    }

    [Fact]
    public void BindingAndRuntimeTypesRemainDistinct()
    {
        Assert.True(typeof(global::Workers.IBinding).IsAssignableFrom(
            typeof(global::Workers.IDurableObjectNamespace)));
        Assert.False(typeof(global::Workers.IBinding).IsAssignableFrom(
            typeof(global::Workers.ICache)));
    }

    [Fact]
    public void RuntimeCapabilitiesHaveFocusedCSharpApis()
    {
        var assembly = typeof(global::Workers.Env).Assembly;

        Assert.Null(assembly.GetType("Workers.WorkerRuntime"));
        Assert.Null(assembly.GetType("Workers.Log"));
        Assert.Null(assembly.GetType("Workers.Socket"));
        Assert.NotNull(assembly.GetType("Workers.Http"));
        Assert.NotNull(assembly.GetType("Workers.TcpSocket"));
    }
}
