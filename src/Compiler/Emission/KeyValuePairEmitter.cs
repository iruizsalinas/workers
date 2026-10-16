using Microsoft.CodeAnalysis;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsKeyValuePair(ITypeSymbol type) => type is INamedTypeSymbol named
        && named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>";

    private string KeyValuePairText(INamedTypeSymbol type, string value, SyntaxNode source) =>
        $"((pair) => \"[\" + {RecordMemberText(type.TypeArguments[0], "pair[0]", source)} + \", \" + {RecordMemberText(type.TypeArguments[1], "pair[1]", source)} + \"]\")({value})";
}
