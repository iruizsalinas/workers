using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// C# 14 field-backed properties: accessors that use `field`, or that mix an auto accessor with a
// custom one, share a compiler-generated backing field. The property stays a JavaScript accessor
// pair and the backing field gets separate storage, which initializers and copies write directly.
internal sealed partial class JavaScriptEmitter
{
    private static IFieldSymbol? FieldKeywordBackingField(IPropertySymbol property)
    {
        property = property.OriginalDefinition;
        if (property.DeclaringSyntaxReferences is not [{ } reference]
            || reference.GetSyntax() is not PropertyDeclarationSyntax declaration
            || IsAutoProperty(declaration))
            return null;
        return property.ContainingType.GetMembers().OfType<IFieldSymbol>()
            .FirstOrDefault(field => SymbolEqualityComparer.Default.Equals(field.AssociatedSymbol, property));
    }

    private static bool IsFieldKeywordBackingField(ISymbol? symbol) =>
        symbol is IFieldSymbol { AssociatedSymbol: IPropertySymbol property } && FieldKeywordBackingField(property) is not null;

    // Where an instance backing field lives: auto-properties store their value under the property name.
    private string BackingStorage(string receiver, IFieldSymbol field) =>
        field.AssociatedSymbol is IPropertySymbol property && !IsFieldKeywordBackingField(field)
            ? UserPropertyStorage(receiver, property)
            : UserMemberAccess(receiver, field);

    private static bool IsAutoAccessor(AccessorDeclarationSyntax accessor) => accessor.Body is null && accessor.ExpressionBody is null;

    // An auto accessor of a field-backed property reads or writes the backing storage.
    private static string AutoAccessorStatement(bool getter, string storage, string value) =>
        getter ? $"return {storage};" : $"{storage} = {value};";

    private string StaticBackingStorage(IFieldSymbol field) =>
        $"{StaticHolder(field.ContainingType)}(){StaticMemberKey(field)}";
}
