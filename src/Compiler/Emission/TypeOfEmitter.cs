using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private string TypeOf(ITypeOfOperation operation)
    {
        var type = ResolveType(operation.TypeOperand);
        string Name(ITypeSymbol symbol) => symbol is IArrayTypeSymbol array
            ? Name(array.ElementType) + "[" + new string(',', array.Rank - 1) + "]"
            : symbol.MetadataName;
        var namespaceType = type;
        while (namespaceType is IArrayTypeSymbol array) namespaceType = array.ElementType;
        var ns = namespaceType.ContainingNamespace.IsGlobalNamespace ? "null" : JsonSerializer.Serialize(namespaceType.ContainingNamespace.ToDisplayString());
        var key = RuntimeTypeName(type);
        static string Boolean(bool value) => value ? "true" : "false";
        var fields = $"name: {JsonSerializer.Serialize(Name(type))}, namespace: {ns}, "
            + $"isArray: {Boolean(type is IArrayTypeSymbol)}, isValueType: {Boolean(type.IsValueType)}, isEnum: {Boolean(type.TypeKind == TypeKind.Enum)}, "
            + $"isInterface: {Boolean(type.TypeKind == TypeKind.Interface)}, isGenericType: {Boolean(type is INamedTypeSymbol { IsGenericType: true })}";
        return $"{_helpers.Require(JavaScriptHelper.TypeOf)}({JsonSerializer.Serialize(key)}, () => ({{ {fields}, text: {JsonSerializer.Serialize(key)} }}))";
    }
}
