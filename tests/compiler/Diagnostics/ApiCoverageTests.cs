using System.Reflection;

namespace Workers.Compiler.Tests;

public sealed class ApiCoverageTests
{
    [Fact]
    public void EveryPublicMethodHasAnExplicitCompilerClassification()
    {
        var missing = typeof(global::Workers.Response).Assembly
            .GetExportedTypes()
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Where(method => method.Name != "<Clone>$")
                .Where(method => method.Name is not ("Equals" or "GetHashCode" or "ToString" or "Deconstruct"))
                .Select(method => (Type: ApiTypeName(type), Method: method.Name)))
            .Distinct()
            .Where(method => !BindingIntrinsicRegistry.IsClassified(method.Type, method.Method))
            .OrderBy(method => method.Type, StringComparer.Ordinal)
            .ThenBy(method => method.Method, StringComparer.Ordinal)
            .Select(method => $"{method.Type}.{method.Method}")
            .ToArray();

        Assert.True(missing.Length == 0, "Unclassified public API methods:\n" + string.Join("\n", missing));
    }

    private static string ApiTypeName(Type type)
    {
        if (!type.IsGenericTypeDefinition)
            return type.FullName!;

        var fullName = type.FullName!;
        var name = fullName[..fullName.IndexOf('`')];
        return name + "<" + string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name)) + ">";
    }
}
