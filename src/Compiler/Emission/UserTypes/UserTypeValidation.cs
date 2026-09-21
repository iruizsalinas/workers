using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private static void ValidateUserType(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        if (type.IsAbstract || type.IsStatic || type.Arity != 0
            || type.BaseType?.SpecialType != SpecialType.System_Object)
            throw new NotSupportedException(
                $"WRK119: User type '{type}' must be a non-abstract, non-generic class or record without inheritance.");
        ValidateJsonAttributes(type);
        if (declaration is RecordDeclarationSyntax { ParameterList: { } recordParameters }
            && recordParameters.Parameters.Any(parameter =>
                LowerFirst(parameter.Identifier.ValueText) == "toJSON"))
            throw new NotSupportedException($"WRK119: User type '{type}' contains a property that conflicts with generated JavaScript behavior.");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1 || constructors.Any(constructor =>
                constructor.Initializer is not null
                || constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
                || constructor.Modifiers.Any(SyntaxKind.ExternKeyword)
                || constructor.ParameterList.Parameters.Any(parameter => parameter.Modifiers.Any(modifier =>
                    modifier.IsKind(SyntaxKind.RefKeyword)
                    || modifier.IsKind(SyntaxKind.OutKeyword)
                    || modifier.IsKind(SyntaxKind.InKeyword)
                    || modifier.IsKind(SyntaxKind.ParamsKeyword)))))
            throw new NotSupportedException($"WRK119: User type '{type}' must have at most one constructor without constructor chaining.");

        var unsupported = declaration.Members.FirstOrDefault(member => member switch
        {
            FieldDeclarationSyntax field => field.Declaration.Variables.Any(variable =>
                variable.Parent?.Parent is not FieldDeclarationSyntax
                || field.Modifiers.Any(SyntaxKind.ConstKeyword)
                || field.Modifiers.Any(SyntaxKind.StaticKeyword)),
            ConstructorDeclarationSyntax => false,
            MethodDeclarationSyntax method => !IsSupportedUserMethod(method),
            PropertyDeclarationSyntax property => !IsSupportedUserProperty(property),
            _ => true
        });
        if (unsupported is not null)
            throw new NotSupportedException($"WRK119: User type '{type}' contains an unsupported member: {unsupported}");
    }

    private static bool IsSupportedUserMethod(MethodDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || declaration.Modifiers.Any(SyntaxKind.ExternKeyword)
            || declaration.Modifiers.Any(SyntaxKind.VirtualKeyword)
            || declaration.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return false;
        return declaration.ParameterList.Parameters.All(parameter =>
            parameter.Modifiers.All(modifier => !modifier.IsKind(SyntaxKind.RefKeyword)
                && !modifier.IsKind(SyntaxKind.OutKeyword)
                && !modifier.IsKind(SyntaxKind.InKeyword)
                && !modifier.IsKind(SyntaxKind.ParamsKeyword)));
    }

    private static bool IsSupportedUserProperty(PropertyDeclarationSyntax property)
    {
        var emittedName = LowerFirst(property.Identifier.ValueText);
        if (emittedName is "constructor" or "toJSON") return false;
        if (property.Modifiers.Any(SyntaxKind.StaticKeyword)
            || property.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || property.Modifiers.Any(SyntaxKind.VirtualKeyword)
            || property.Modifiers.Any(SyntaxKind.OverrideKeyword))
            return false;
        if (IsAutoProperty(property)) return true;
        if (property.Initializer is not null) return false;
        if (property.ExpressionBody is not null) return true;
        return property.AccessorList?.Accessors is [{ } getter]
            && getter.IsKind(SyntaxKind.GetAccessorDeclaration)
            && (getter.Body is not null || getter.ExpressionBody is not null);
    }
}
