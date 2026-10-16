using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsUserRecordCopyConstructor(IMethodSymbol constructor) =>
        constructor.ContainingType.IsRecord && constructor.Parameters is [{ } parameter]
        && SymbolEqualityComparer.Default.Equals(parameter.Type, constructor.ContainingType);

    private void EmitUserRecordCopyConstructor(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        if (type.IsRecord && UsesUserHierarchy(type))
        {
            EmitHierarchyRecordCopy(type, declaration);
            return;
        }
        var constructor = declaration.Members.OfType<ConstructorDeclarationSyntax>()
            .SingleOrDefault(candidate => IsUserRecordCopyConstructor((IMethodSymbol)_model.GetDeclaredSymbol(candidate)!));
        if (constructor is null) return;
        _output.AppendLine("  static $copy(source) {");
        _output.AppendLine("    if (source == null) throw new TypeError(\"Cannot copy a null record.\");");
        _output.Append("    const result = Object.create(").Append(_userTypes[type]).AppendLine(".prototype);");
        _output.AppendLine("    result.$copyFrom(source);");
        _output.AppendLine("    return result;");
        _output.AppendLine("  }");
        _output.Append("  $copyFrom(").Append(string.Join(", ", constructor.ParameterList.Parameters.Select(ParameterDeclaration)))
            .AppendLine(") {");
        // A custom C# copy constructor starts with zero-initialized fields. Member initializers
        // belong to ordinary construction and do not run during record copying.
        foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>())
            foreach (var variable in field.Declaration.Variables)
            {
                var symbol = (IFieldSymbol)_model.GetDeclaredSymbol(variable)!;
                if (!symbol.IsStatic)
                    _output.Append("    ").Append(UserMemberAccess("this", symbol)).Append(" = ")
                        .Append(DefaultFieldValue(symbol.Type, variable)).AppendLine(";");
            }
        if (declaration is RecordDeclarationSyntax { ParameterList: { } parameters })
            foreach (var parameter in parameters.Parameters)
            {
                var property = type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().Single();
                _output.Append("    ").Append(UserMemberAccess("this", property)).Append(" = ")
                    .Append(DefaultFieldValue(property.Type, parameter)).AppendLine(";");
            }
        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>().Where(IsAutoProperty))
        {
            var symbol = (IPropertySymbol)_model.GetDeclaredSymbol(property)!;
            _output.Append("    ").Append(UserMemberAccess("this", symbol)).Append(" = ")
                .Append(DefaultFieldValue(symbol.Type, property)).AppendLine(";");
        }
        if (constructor.ExpressionBody is not null)
            _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
        else
            EmitStatements(constructor.Body?.Statements ?? [], 2);
        _output.AppendLine("  }");
    }

}
