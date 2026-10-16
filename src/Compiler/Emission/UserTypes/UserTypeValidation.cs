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
        if (type.GetMembers().OfType<IPropertySymbol>().Any(property => property.IsIndexer
            && (property.ReturnsByRef || property.ReturnsByRefReadonly || property.Parameters.Any(parameter => parameter.RefKind != RefKind.None)
                || property.GetMethod?.IsExtern == true || property.SetMethod?.IsExtern == true)))
            throw new NotSupportedException($"WRK119: User indexers on '{type}' require value parameters, value returns, and source-defined accessors.");
        if (type.GetMembers().OfType<IEventSymbol>().Any(symbol => symbol.AddMethod?.IsExtern == true || symbol.RemoveMethod?.IsExtern == true))
            throw new NotSupportedException($"WRK119: User events on '{type}' require source-defined accessors.");
        if (type.InstanceConstructors.Any(constructor => constructor.Parameters.Any(parameter => parameter.RefKind != RefKind.None
                || parameter.IsParams && parameter.Type is not IArrayTypeSymbol))
            || type.GetMembers().OfType<IMethodSymbol>().Any(method => method.Parameters.Any(parameter => parameter.IsParams && parameter.Type is not IArrayTypeSymbol)))
            throw new NotSupportedException($"WRK119: User type '{type}' requires supported value parameters and array params parameters.");
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
                    || modifier.IsKind(SyntaxKind.InKeyword)))))
            throw new NotSupportedException($"WRK119: User type '{type}' contains an unsupported constructor.");

        var unsupported = declaration.Members.FirstOrDefault(member => member switch
        {
            FieldDeclarationSyntax => false,
            ConstructorDeclarationSyntax => false,
            MethodDeclarationSyntax method => !IsSupportedUserMethod(method),
            PropertyDeclarationSyntax property => !IsSupportedUserProperty(property),
            IndexerDeclarationSyntax => false,
            OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax => false,
            EventDeclarationSyntax or EventFieldDeclarationSyntax => false,
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
        if (declaration.ReturnType is RefTypeSyntax || declaration.Modifiers.Any(SyntaxKind.ExternKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return false;
        return declaration.ParameterList.Parameters.All(parameter =>
            parameter.Modifiers.All(modifier => !modifier.IsKind(SyntaxKind.RefKeyword)
                && !modifier.IsKind(SyntaxKind.InKeyword)));
    }

    private static bool IsSupportedUserProperty(PropertyDeclarationSyntax property)
    {
        var emittedName = LowerFirst(property.Identifier.ValueText);
        if (emittedName is "constructor" or "toJSON") return false;
        if (IsAutoProperty(property)) return true;
        if (property.Initializer is not null) return false;
        if (property.ExpressionBody is not null) return true;
        return property.AccessorList?.Accessors.All(accessor =>
            (accessor.IsKind(SyntaxKind.GetAccessorDeclaration) || accessor.IsKind(SyntaxKind.SetAccessorDeclaration) || accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
            && (accessor.Body is not null || accessor.ExpressionBody is not null)) == true;
    }
}
