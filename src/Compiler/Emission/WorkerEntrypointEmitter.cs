using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private void EmitWorkerEntrypoint(ClassDeclarationSyntax declaration)
    {
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var symbol = _model.GetDeclaredSymbol(declaration)!;
        ThrowIfDuplicateGeneratedMethods(declaration, _model, symbol.Name);
        var attribute = symbol.GetAttributes().Single(item => item.AttributeClass?.ToDisplayString() == "Workers.WorkerEntrypointAttribute");
        var exportName = attribute.ConstructorArguments.Length == 0 ? null : attribute.ConstructorArguments[0].Value?.ToString();
        var baseClass = _imports.Require("cloudflare:workers", "WorkerEntrypoint", "WorkerEntrypoint");
        _output.Append(exportName is null ? "export default class " : "export class ")
            .Append(exportName ?? symbol.Name).Append(" extends ").Append(baseClass).AppendLine(" {");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1 || constructors.FirstOrDefault()?.ParameterList.Parameters.Count > 0)
            throw Unsupported("WRK109", constructors.Length > 1 ? constructors[1] : constructors[0]);
        var fields = declaration.Members.OfType<FieldDeclarationSyntax>()
            .Where(field => !field.Modifiers.Any(SyntaxKind.StaticKeyword)).ToArray();
        var constructor = constructors.SingleOrDefault();
        if (fields.Length != 0 || constructor is not null)
        {
            _output.AppendLine("  constructor(ctx, env) { super(ctx, env);");
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
            var methodSymbol = _model.GetDeclaredSymbol(method)!;
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(parameter =>
                parameter.Default is null ? ParameterName(parameter) : $"{ParameterName(parameter)} = {Expression(parameter.Default.Value)}"));
            _output.Append("  ").Append(method.Modifiers.Any(SyntaxKind.AsyncKeyword) ? "async " : "")
                .Append(GeneratedInstanceMethodName(methodSymbol))
                .Append('(').Append(parameters).AppendLine(") {");
            if (method.ExpressionBody is not null)
                _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
            else
                foreach (var statement in method.Body?.Statements ?? []) EmitStatement(statement, 2);
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }
}
