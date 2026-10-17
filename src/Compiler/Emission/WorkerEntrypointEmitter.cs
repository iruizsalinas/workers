using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string RuntimeClassName(INamedTypeSymbol type) =>
        _names.Get("runtime-class:" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), type.Name);

    // Durable Objects and Worker entrypoints extend the runtime base classes from cloudflare:workers, which store
    // the constructor's ctx and env. They export under the C# class name, or as the default export when marked.
    private void EmitRuntimeClass(ClassDeclarationSyntax declaration, string baseClass)
    {
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var symbol = _model.GetDeclaredSymbol(declaration)!;
        using var classScope = EnterGeneratedClass(symbol);
        ValidateGeneratedClass(declaration, _model, symbol.Name);
        ThrowIfDuplicateGeneratedMethods(declaration, _model, symbol.Name);
        var isDefault = symbol.GetAttributes().Any(item => item.AttributeClass?.ToDisplayString() == "Workers.DefaultExportAttribute");
        if (!isDefault && (!IsLegalJavascriptIdentifier(symbol.Name) || JavascriptReservedWords.Contains(symbol.Name)))
            throw new NotSupportedException($"WRK117: '{symbol.Name}' is not a valid JavaScript export name.");
        var baseName = _imports.Require("cloudflare:workers", baseClass, baseClass);
        foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodSymbol = _model.GetDeclaredSymbol(method)!;
            if (NeedsRpcResultWrapper(methodSymbol))
                _output.Append("const ").Append(RpcMethodBodySlot(methodSymbol)).AppendLine(" = Symbol();");
        }
        _output.Append(isDefault ? "export default class " : "class ")
            .Append(RuntimeClassName(symbol)).Append(" extends ").Append(baseName).AppendLine(" {");
        var constructors = declaration.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
        if (constructors.Length > 1 || constructors.FirstOrDefault()?.ParameterList.Parameters.Count > 0)
            throw Unsupported("WRK109", constructors.Length > 1 ? constructors[1] : constructors[0]);
        var fields = declaration.Members.OfType<FieldDeclarationSyntax>()
            .Where(field => field.Declaration.Variables.Any(variable =>
                _model.GetDeclaredSymbol(variable) is IFieldSymbol { IsStatic: false, IsConst: false })).ToArray();
        var constructor = constructors.SingleOrDefault();
        if (fields.Length != 0 || constructor is not null)
        {
            _output.AppendLine("  constructor(ctx, env) {").AppendLine("    super(ctx, env);");
            foreach (var field in fields)
                foreach (var variable in field.Declaration.Variables)
                    _output.Append("    this.").Append(GeneratedFieldName((IFieldSymbol)_model.GetDeclaredSymbol(variable)!)).Append(" = ")
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
            var methodSymbol = _model.GetDeclaredSymbol(method)!;
            var isEnumerable = IsEnumerableIterator(method, methodSymbol);
            var parameters = string.Join(", ", method.ParameterList.Parameters.Select(ParameterDeclaration));
            var rpcWrapper = NeedsRpcResultWrapper(methodSymbol);
            if (rpcWrapper)
            {
                var readers = methodSymbol.Parameters.Select(parameter => RpcArgumentReader(parameter.Type, method)).ToArray();
                var seen = _names.Get("rpc-seen:" + methodSymbol.ToDisplayString(), "rpcSeen");
                var arguments = method.ParameterList.Parameters.Select((parameter, index) => readers[index] is { } reader
                    ? $"{reader}({ParameterName(parameter)}, {seen})" : ParameterName(parameter));
                var call = $"this[{RpcMethodBodySlot(methodSymbol)}]({string.Join(", ", arguments)})";
                var result = RpcResultNeedsConversion(methodSymbol.ReturnType)
                    ? $"{RequireHelperName(JavaScriptHelper.RpcArguments, "rpcValue")}({call})" : call;
                _output.Append("  ").Append(GeneratedInstanceMethodName(methodSymbol)).Append('(').Append(parameters).AppendLine(") {");
                if (readers.Any(reader => reader is not null)) _output.Append("    const ").Append(seen).AppendLine(" = new WeakMap();");
                _output.Append("    return ").Append(result).AppendLine(";");
                _output.AppendLine("  }");
            }
            _output.Append("  ").Append(method.Modifiers.Any(SyntaxKind.AsyncKeyword) && !isEnumerable ? "async " : "")
                .Append(IsIterator(method) && !isEnumerable ? "*" : "")
                .Append(rpcWrapper ? "[" + RpcMethodBodySlot(methodSymbol) + "]" : GeneratedInstanceMethodName(methodSymbol))
                .Append('(').Append(parameters).AppendLine(") {");
            EmitWithExpressionScope(2, () => EmitRpcMethodBody(method, methodSymbol));
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
        if (!isDefault)
            _output.Append("export { ").Append(RuntimeClassName(symbol)).Append(" as ").Append(symbol.Name).AppendLine(" };").AppendLine();
    }
}
