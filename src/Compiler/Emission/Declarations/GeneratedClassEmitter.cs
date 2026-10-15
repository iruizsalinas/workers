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
                EmitStatements(constructor?.Body?.Statements ?? [], 2);
            _output.AppendLine("  }");
        }
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var symbol = _model.GetDeclaredSymbol(method)!;
            var name = GeneratedInstanceMethodName(symbol);
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterDeclaration));
            var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var isIterator = IsIterator(method);
            var isEnumerable = IsEnumerableIterator(method, symbol);
            _output.Append("  ").Append(isAsync && !isEnumerable ? "async " : "").Append(isIterator && !isEnumerable ? "*" : "")
                .Append(name).Append('(').Append(parameters).AppendLine(") {");
            EmitWithExpressionScope(2, () =>
            {
                if (isEnumerable)
                    EmitEnumerableIteratorBody(method, 2);
                else if (method.ExpressionBody is not null)
                    _output.Append("    return ").Append(Expression(method.ExpressionBody.Expression)).AppendLine(";");
                else
                    EmitStatements(method.Body?.Statements ?? [], 2);
            });
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }

    // Public methods of Worker entrypoints and Durable Objects are callable over RPC, which cannot
    // carry instances of compiled user classes; their results are converted to plain objects.
    private void EmitRpcMethodBody(MethodDeclarationSyntax method, IMethodSymbol symbol)
    {
        if (IsEnumerableIterator(method, symbol))
        {
            EmitEnumerableIteratorBody(method, 2);
            return;
        }
        var convert = symbol.DeclaredAccessibility == Accessibility.Public && !IsIterator(method)
            && ContainsUserInstanceType(symbol.ReturnType);
        var rpcValue = convert ? RequireHelperName(JavaScriptHelper.RpcArguments, "rpcValue") : null;
        if (method.ExpressionBody is not null)
        {
            var value = Expression(method.ExpressionBody.Expression);
            _output.Append("    return ").Append(rpcValue is null ? value : $"{rpcValue}({value})").AppendLine(";");
            return;
        }
        if (rpcValue is null)
        {
            EmitStatements(method.Body?.Statements ?? [], 2);
            return;
        }
        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        _output.Append("    return ").Append(rpcValue).AppendLine(isAsync ? "(await (async () => {" : "((() => {");
        EmitStatements(method.Body?.Statements ?? [], 3);
        _output.AppendLine("    })());");
    }

    private static bool ContainsUserInstanceType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => ContainsUserInstanceType(array.ElementType),
        INamedTypeSymbol named when IsUserInstanceType(named) => true,
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsUserInstanceType),
        _ => false
    };
}
