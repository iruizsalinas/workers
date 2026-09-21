using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string EventName(string eventName) => _names.Get("event:" + eventName, eventName);

    private void EmitHtmlHandler(ClassDeclarationSyntax declaration)
    {
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var type = _model.GetDeclaredSymbol(declaration)!;
        ValidateGeneratedClass(declaration, _model, type.Name);
        ThrowIfDuplicateGeneratedMethods(declaration, _model, type.Name);
        _output.Append("class ").Append(UserIdentifier(type, declaration.Identifier)).AppendLine(" {");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1)
            throw Unsupported("WRK109", constructors[1]);
        var constructor = constructors.SingleOrDefault();
        var fields = declaration.Members.OfType<FieldDeclarationSyntax>()
            .Where(field => field.Declaration.Variables.Any(variable =>
                _model.GetDeclaredSymbol(variable) is IFieldSymbol { IsStatic: false, IsConst: false })).ToArray();
        if (constructor is not null || fields.Length != 0)
        {
            _output.Append("  constructor(")
                .Append(string.Join(", ", constructor?.ParameterList.Parameters.Select(ParameterDeclaration) ?? []))
                .AppendLine(") {");
            foreach (var field in fields)
                foreach (var variable in field.Declaration.Variables)
                    _output.Append("    this.").Append(UserIdentifier(_model.GetDeclaredSymbol(variable)!, variable.Identifier)).Append(" = ")
                        .Append(variable.Initializer is null
                            ? DefaultFieldValue(_model.GetTypeInfo(field.Declaration.Type).Type!, variable)
                            : Expression(variable.Initializer.Value)).AppendLine(";");
            if (constructor?.ExpressionBody is not null)
                _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in constructor?.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var name = GeneratedInstanceMethodName(_model.GetDeclaredSymbol(method)!);
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterDeclaration));
            var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = IsIterator(method);
            _output.Append("  ").Append(isAsync ? "async " : "").Append(isIterator ? "*" : "")
                .Append(name).Append('(').Append(parameters).AppendLine(") {");
            if (method.ExpressionBody is not null)
                _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in method.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }

    private void EmitDurableObject(ClassDeclarationSyntax declaration)
    {
        var model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var symbol = model.GetDeclaredSymbol(declaration)!;
        ValidateGeneratedClass(declaration, model, symbol.Name);
        ThrowIfDuplicateGeneratedMethods(declaration, model, symbol.Name);
        var attribute = symbol.GetAttributes().Single(item => item.AttributeClass?.ToDisplayString() == "Workers.DurableObjectAttribute");
        var exportName = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value?.ToString() ?? symbol.Name
            : symbol.Name;
        if (!IsLegalJavascriptIdentifier(exportName) || JavascriptReservedWords.Contains(exportName))
            throw Unsupported("WRK108", declaration);
        _model = model;
        var durableObjectBase = _imports.Require("cloudflare:workers", "DurableObject", "DurableObject");
        _output.Append("export class ").Append(exportName).Append(" extends ").Append(durableObjectBase).AppendLine(" {");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1)
            throw Unsupported("WRK109", constructors[1]);
        var constructor = constructors.SingleOrDefault();
        if (constructor is not null && constructor.ParameterList.Parameters.Count != 2)
            throw Unsupported("WRK109", constructor);
        if (constructor is not null && model.GetDeclaredSymbol(constructor) is IMethodSymbol constructorSymbol
            && (constructorSymbol.Parameters[0].Type.ToDisplayString() != "Workers.DurableObjectState"
                || constructorSymbol.Parameters[1].Type.ToDisplayString() != "Workers.Env"))
            throw Unsupported("WRK109", constructor);
        var stateName = constructor is null ? "state" : ParameterName(constructor.ParameterList.Parameters[0]);
        var envName = constructor is null ? "env" : ParameterName(constructor.ParameterList.Parameters[1]);
        _output.Append("  constructor(").Append(stateName).Append(", ").Append(envName).Append(") { super(")
            .Append(stateName).Append(", ").Append(envName).AppendLine(");");
        _output.Append("    this.state = ").Append(stateName).Append("; this.env = ").Append(envName).AppendLine(";");
        foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>().Where(field =>
                     field.Declaration.Variables.Any(variable =>
                         model.GetDeclaredSymbol(variable) is IFieldSymbol { IsStatic: false, IsConst: false })))
            foreach (var variable in field.Declaration.Variables)
                _output.Append("    this.").Append(UserIdentifier(model.GetDeclaredSymbol(variable)!, variable.Identifier)).Append(" = ")
                    .Append(variable.Initializer is null
                        ? DefaultFieldValue(model.GetTypeInfo(field.Declaration.Type).Type!, variable)
                        : Expression(variable.Initializer.Value)).AppendLine(";");
        if (constructor?.ExpressionBody is not null)
            _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
        else
            foreach (var statement in constructor?.Body?.Statements ?? []) EmitStatement(statement, 2);
        _output.AppendLine("  }");
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            _model = model;
            var name = GeneratedInstanceMethodName(model.GetDeclaredSymbol(method)!);
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterDeclaration));
            var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = IsIterator(method);
            _output.Append("  ").Append(isAsync ? "async " : "").Append(isIterator ? "*" : "")
                .Append(name).Append('(').Append(parameters).AppendLine(") {");
            if (method.ExpressionBody is not null)
                _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in method.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }

}
