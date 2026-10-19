using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static IMethodSymbol[] OrdinaryConstructors(INamedTypeSymbol type) =>
        type.InstanceConstructors.Where(constructor => !IsUserRecordCopyConstructor(constructor)).ToArray();

    private static bool UsesConstructorSelector(INamedTypeSymbol type) => UsesUserHierarchy(type) || OrdinaryConstructors(type).Length > 1;

    private static int ConstructorSelector(IMethodSymbol constructor) =>
        Array.FindIndex(OrdinaryConstructors(constructor.ContainingType), candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, constructor.OriginalDefinition));

    private static string UserConstruction(INamedTypeSymbol type, IMethodSymbol constructor,
        string typeName, IReadOnlyList<string> arguments) =>
        $"new {typeName}({string.Join(", ", UsesConstructorSelector(type)
            ? new[] { ConstructorSelector(constructor).ToString(System.Globalization.CultureInfo.InvariantCulture) }.Concat(arguments)
            : arguments)})";

    private void EmitUserConstructor(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        if (UsesConstructorSelector(type))
        {
            EmitConstructorDispatch(type, declaration);
            return;
        }
        var constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>()
            .SingleOrDefault(candidate => !candidate.Modifiers.Any(SyntaxKind.StaticKeyword)
                && !IsUserRecordCopyConstructor((IMethodSymbol)_model.GetDeclaredSymbol(candidate)!));
        var record = declaration as RecordDeclarationSyntax;
        var parameters = constructor?.ParameterList.Parameters ?? PrimaryParameters(declaration);
        _output.Append("  constructor(")
            .Append(parameters.Count == 0 ? "" : string.Join(", ", parameters.Select(ParameterDeclaration)))
            .AppendLine(") {");
        EmitExplicitStaticInitialization(type, 2);
        EmitWithExpressionScope(2, () =>
        {
            EmitPrimaryParameterInitialization(declaration);
            EmitUserInstanceInitializers(type, declaration, record?.ParameterList?.Parameters ?? default);
            EmitUserConstructorStatements(constructor);
        });
        _output.AppendLine("  }");
    }

    private void EmitUserInstanceInitializers(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        SeparatedSyntaxList<ParameterSyntax> positionalParameters)
    {
        foreach (var parameter in positionalParameters)
        {
            var parameterSymbol = (IParameterSymbol)_model.GetDeclaredSymbol(parameter)!;
            var property = type.GetMembers(parameterSymbol.Name).OfType<IPropertySymbol>().SingleOrDefault();
            if (property is null || property.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is PropertyDeclarationSyntax)) continue;
            _output.Append("    ").Append(UserMemberAccess("this", property)).Append(" = ")
                .Append(CopyValue(property.Type, ParameterName(parameter))).AppendLine(";");
        }

        foreach (var member in declaration.Members)
        {
            if (member is EventFieldDeclarationSyntax eventField)
                foreach (var variable in eventField.Declaration.Variables)
                {
                    var symbol = (IEventSymbol)_model.GetDeclaredSymbol(variable)!;
                    if (symbol.IsStatic) continue;
                    _output.Append("    ").Append(UserMemberAccess("this", symbol)).Append(" = ")
                        .Append(variable.Initializer is null ? "null" : Expression(variable.Initializer.Value)).AppendLine(";");
                }
            if (member is FieldDeclarationSyntax field)
                foreach (var variable in field.Declaration.Variables)
                {
                    var symbol = (IFieldSymbol)_model.GetDeclaredSymbol(variable)!;
                    if (symbol.IsStatic) continue;
                    _output.Append("    ").Append(UserMemberAccess("this", symbol)).Append(" = ")
                        .Append(variable.Initializer is null
                            ? DefaultFieldValue(symbol.Type, variable)
                            : ValueExpression(variable.Initializer.Value)).AppendLine(";");
                }
            else if (member is PropertyDeclarationSyntax property
                     && (IsAutoProperty(property) || FieldKeywordBackingField((IPropertySymbol)_model.GetDeclaredSymbol(property)!) is not null))
            {
                var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
                if (symbol.IsStatic) continue;
                var storage = FieldKeywordBackingField(symbol) is { } backing ? BackingStorage("this", backing) : UserPropertyStorage("this", symbol);
                _output.Append("    ").Append(storage).Append(" = ")
                    .Append(property.Initializer is null
                        ? DefaultFieldValue(symbol.Type, property)
                        : ValueExpression(property.Initializer.Value)).AppendLine(";");
            }
        }

    }

    private void EmitUserConstructorStatements(ConstructorDeclarationSyntax? constructor)
    {
        if (constructor?.ExpressionBody is not null)
            _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
        else
            EmitStatements(constructor?.Body?.Statements ?? [], 2);
    }

}
