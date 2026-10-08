namespace Workers.Compiler.Tests;

public sealed class ApiTaxonomyTests
{
    [Fact]
    public void PublicClassesExposeUsableMembersRatherThanEmptyPlaceholders()
    {
        var empty = typeof(global::Workers.Env).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && !typeof(Attribute).IsAssignableFrom(type))
            // Timeout handles are opaque tokens consumed by Timers.ClearTimeout.
            .Where(type => type != typeof(global::Workers.TimerHandle))
            .Where(type => !type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.DeclaredOnly)
                .Any(member => member is System.Reflection.PropertyInfo { GetMethod: { IsPublic: true } or { IsFamily: true } }
                    or System.Reflection.FieldInfo { IsPublic: true } or System.Reflection.FieldInfo { IsFamily: true }
                    or System.Reflection.MethodInfo { IsPublic: true } or System.Reflection.MethodInfo { IsFamily: true }))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(empty);
    }

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
