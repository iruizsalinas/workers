using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private void EmitWorkerEntrypoint(ClassDeclarationSyntax declaration)
    {
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var symbol = _model.GetDeclaredSymbol(declaration)!;
        var attribute = symbol.GetAttributes().Single(item => item.AttributeClass?.ToDisplayString() == "Workers.WorkerEntrypointAttribute");
        var exportName = attribute.ConstructorArguments.Length == 0 ? null : attribute.ConstructorArguments[0].Value?.ToString();
        var baseClass = _imports.Require("cloudflare:workers", "WorkerEntrypoint", "WorkerEntrypoint");
        _output.Append(exportName is null ? "export default class " : "export class ")
            .Append(exportName ?? symbol.Name).Append(" extends ").Append(baseClass).AppendLine(" {");
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>().Where(item => item.Modifiers.Any(SyntaxKind.PublicKeyword)))
        {
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(parameter =>
                parameter.Default is null ? ParameterName(parameter) : $"{ParameterName(parameter)} = {Expression(parameter.Default.Value)}"));
            _output.Append("  ").Append(method.Modifiers.Any(SyntaxKind.AsyncKeyword) ? "async " : "")
                .Append(LowerNativeMethodName(method.Identifier.Text))
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
