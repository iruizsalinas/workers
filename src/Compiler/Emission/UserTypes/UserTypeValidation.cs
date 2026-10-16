using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static void ValidateUserType(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var exception = IsUserException(type);
        if (type.IsStatic
            || type.BaseType?.SpecialType != SpecialType.System_Object && !exception
                && !(type.BaseType is { } parent && IsUserInstanceType(parent)))
            throw new NotSupportedException(
                $"WRK119: User type '{type}' must be a class or record with a supported source-defined base type. " +
                "Exception types may derive from System.Exception.");
        ValidateJsonAttributes(type);
        ValidateUserEnumerationProtocol(type);
        if (declaration is ClassDeclarationSyntax { ParameterList: not null })
            throw new NotSupportedException($"WRK119: Primary class constructors are outside the Workers profile; use an explicit constructor on '{type}'.");
        if (declaration is RecordDeclarationSyntax { ParameterList: { } recordParameters }
            && recordParameters.Parameters.Any(parameter =>
                LowerFirst(parameter.Identifier.ValueText) == "toJSON"))
            throw new NotSupportedException($"WRK119: User type '{type}' contains a property that conflicts with generated JavaScript behavior.");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Any(constructor =>
                constructor.Modifiers.Any(SyntaxKind.ExternKeyword)
                || constructor.ParameterList.Parameters.Any(parameter => parameter.Modifiers.Any(modifier =>
                    modifier.IsKind(SyntaxKind.RefKeyword)
                    || modifier.IsKind(SyntaxKind.OutKeyword)
                    || modifier.IsKind(SyntaxKind.InKeyword)
                    || modifier.IsKind(SyntaxKind.ParamsKeyword)))))
            throw new NotSupportedException($"WRK119: User type '{type}' contains an unsupported constructor.");

        var unsupported = declaration.Members.FirstOrDefault(member => member switch
        {
            FieldDeclarationSyntax => false,
            ConstructorDeclarationSyntax => false,
            MethodDeclarationSyntax method => !IsSupportedUserMethod(method),
            PropertyDeclarationSyntax property => !IsSupportedUserProperty(property),
            _ => true
        });
        if (unsupported is not null)
            throw new NotSupportedException($"WRK119: User type '{type}' contains an unsupported member: {unsupported}");
    }

    private static void ValidateUserEnumerationProtocol(INamedTypeSymbol type)
    {
        if (type.AllInterfaces.Any(contract => contract.OriginalDefinition.ToDisplayString() is
                "System.Collections.IEnumerable" or "System.Collections.Generic.IEnumerable<T>"
                or "System.Collections.Generic.IAsyncEnumerable<T>"))
            throw new NotSupportedException(
                $"WRK119: User type '{type}' implements an unsupported enumeration protocol. " +
                "Source-defined IEnumerable and IAsyncEnumerable implementations are outside the Workers profile; " +
                "use iterator methods returning IEnumerable<T> or IAsyncEnumerable<T> instead.");
    }

    private static bool IsUserException(INamedTypeSymbol type) =>
        type.BaseType?.ToDisplayString() == "System.Exception" && !type.IsRecord;

    private static bool IsSupportedUserMethod(MethodDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Any(SyntaxKind.ExternKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return false;
        return declaration.ParameterList.Parameters.All(parameter =>
            parameter.Modifiers.All(modifier => !modifier.IsKind(SyntaxKind.RefKeyword)
                && !modifier.IsKind(SyntaxKind.InKeyword)
                && !modifier.IsKind(SyntaxKind.ParamsKeyword)));
    }

    private static bool IsSupportedUserProperty(PropertyDeclarationSyntax property)
    {
        var emittedName = LowerFirst(property.Identifier.ValueText);
        if (emittedName is "constructor" or "toJSON") return false;
        if (IsAutoProperty(property)) return true;
        if (property.Initializer is not null) return false;
        if (property.ExpressionBody is not null) return true;
        return property.AccessorList?.Accessors is [{ } getter]
            && getter.IsKind(SyntaxKind.GetAccessorDeclaration)
            && (getter.Body is not null || getter.ExpressionBody is not null);
    }
}
