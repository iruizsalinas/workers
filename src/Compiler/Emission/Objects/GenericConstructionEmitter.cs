using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string GenericConstruction(ITypeParameterObjectCreationOperation operation)
    {
        var type = ResolveType(operation.Type!);
        if (type.SpecialType == SpecialType.System_Object) return "{}";
        if (type.IsValueType) return DefaultFieldValue(type, operation.Syntax);
        if (type is INamedTypeSymbol named && IsUserInstanceType(named))
        {
            var constructor = named.InstanceConstructors.Single(candidate => candidate.Parameters.Length == 0);
            return UserObject((BaseObjectCreationExpressionSyntax)operation.Syntax, constructor, named, []);
        }
        throw UnsupportedSymbol(type, operation.Syntax);
    }
}
