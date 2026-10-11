using System.Reflection;

namespace Workers.Compiler.Tests;

public sealed class ApiTaxonomyTests
{
    private static readonly Assembly Api = typeof(global::Workers.Env).Assembly;

    [Fact]
    public void PublicClassesExposeUsableMembersRatherThanEmptyPlaceholders()
    {
        var empty = Api.GetExportedTypes()
            .Where(type => type.IsClass && !typeof(Attribute).IsAssignableFrom(type))
            // Timer handles are opaque tokens consumed by Timers.ClearTimeout and Timers.ClearInterval.
            .Where(type => type != typeof(global::Workers.TimerHandle))
            // Specialized streams such as FixedLengthStream inherit their whole surface from the base stream.
            .Where(type => type.BaseType == typeof(object))
            .Where(type => !type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(member => member is PropertyInfo { GetMethod: { IsPublic: true } or { IsFamily: true } }
                    or FieldInfo { IsPublic: true } or FieldInfo { IsFamily: true }
                    or MethodInfo { IsPublic: true } or MethodInfo { IsFamily: true }))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(empty);
    }

    // Types the runtime creates, such as bindings and results, cannot be constructed by Worker code, so a
    // public constructor would only advertise an API the compiler rejects.
    [Fact]
    public void RuntimeOwnedClassesHaveNoPublicConstructor()
    {
        var constructible = Api.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !typeof(Attribute).IsAssignableFrom(type)
                && !typeof(Exception).IsAssignableFrom(type))
            .Where(type => type.GetConstructors().Length != 0)
            .Where(type => !BindingClassification.IsStructural(ApiTypeName(type)))
            .Where(type => type.GetProperties().All(property => property.SetMethod is null))
            .Select(type => ApiTypeName(type))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            "Workers.AbortController", "Workers.Blob", "Workers.CompressionStream", "Workers.Date",
            "Workers.DecompressionStream", "Workers.DigestStream", "Workers.EmailMessage", "Workers.File",
            "Workers.FixedLengthStream", "Workers.FormData", "Workers.HtmlRewriter",
            "Workers.IdentityTransformStream", "Workers.Request", "Workers.Router",
            "Workers.TextDecoder", "Workers.TextEncoder", "Workers.TransformStream", "Workers.UrlSearchParams",
            "Workers.WebSocketPair", "Workers.WebSocketRequestResponsePair"
        ], constructible);
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
        Assert.DoesNotContain("FetchAsync", methods);
        Assert.DoesNotContain("HtmlRewriter", methods);
        Assert.DoesNotContain("Log", methods);
    }

    [Fact]
    public void BindingsAreRuntimeClassesNamedLikeTheirCloudflareTypes()
    {
        var bindings = typeof(global::Workers.Env).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.ReturnType.Namespace == "Workers")
            .Select(method => method.ReturnType)
            .Distinct()
            .ToArray();

        Assert.All(bindings, binding => Assert.False(binding.IsInterface, binding.Name));
        Assert.Contains(typeof(global::Workers.KVNamespace), bindings);
        Assert.Contains(typeof(global::Workers.Fetcher), bindings);
        Assert.Null(Api.GetType("Workers.IBinding"));
    }

    private static string ApiTypeName(Type type) => type.IsGenericTypeDefinition
        ? $"{type.FullName![..type.FullName!.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name))}>"
        : type.FullName!;
}
