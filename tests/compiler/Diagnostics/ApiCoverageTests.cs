using System.Reflection;

namespace Workers.Compiler.Tests;

public sealed class ApiCoverageTests
{
    [Fact]
    public void EveryPublicMethodOverloadHasAnExplicitCompilerClassification()
    {
        var missing = typeof(global::Workers.Response).Assembly
            .GetExportedTypes()
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Where(method => method.Name != "<Clone>$")
                .Where(method => method.Name is not ("Equals" or "GetHashCode" or "ToString" or "Deconstruct"))
                .Select(method => (Type: ApiTypeName(type), Method: method.Name, Signature: MethodSignature(method))))
            .Where(method => !BindingIntrinsicRegistry.IsClassified(method.Type, method.Method))
            .OrderBy(method => method.Type, StringComparer.Ordinal)
            .ThenBy(method => method.Method, StringComparer.Ordinal)
            .Select(method => method.Signature)
            .ToArray();

        Assert.True(missing.Length == 0, "Unclassified public API methods:\n" + string.Join("\n", missing));
    }

    private static string MethodSignature(MethodInfo method) =>
        $"{ApiTypeName(method.DeclaringType!)}.{method.Name}(" +
        string.Join(", ", method.GetParameters().Select(parameter => ApiTypeName(parameter.ParameterType))) + ")";

    private static string ApiTypeName(Type type)
    {
        if (type.IsByRef)
            return ApiTypeName(type.GetElementType()!) + "&";
        if (type.IsArray)
            return ApiTypeName(type.GetElementType()!) + "[]";
        if (type.IsGenericParameter)
            return type.Name;
        if (!type.IsGenericTypeDefinition)
        {
            if (!type.IsGenericType)
                return type.FullName!;
            var definition = type.GetGenericTypeDefinition();
            var definitionName = definition.FullName!;
            var baseName = definitionName[..definitionName.IndexOf('`')];
            return baseName + "<" + string.Join(", ", type.GetGenericArguments().Select(ApiTypeName)) + ">";
        }

        var fullName = type.FullName!;
        var name = fullName[..fullName.IndexOf('`')];
        return name + "<" + string.Join(", ", type.GetGenericArguments().Select(argument => argument.Name)) + ">";
    }
}
