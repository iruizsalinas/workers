using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private INamedTypeSymbol? _generatedClassScope;

    private IDisposable EnterGeneratedClass(INamedTypeSymbol type)
    {
        var previous = _generatedClassScope;
        _generatedClassScope = type;
        return new RestoreGeneratedClassScope(() => _generatedClassScope = previous);
    }

    private sealed class RestoreGeneratedClassScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }

    private string EventName(string eventName) => _names.Get("event:" + eventName, eventName);

    private string GeneratedFieldName(IFieldSymbol field)
    {
        var name = UserIdentifier(field, field.Name);
        return name is "ctx" or "env" or "__proto__" or "constructor"
            || field.ContainingType.GetMembers().OfType<IMethodSymbol>().Any(method =>
                method.MethodKind == MethodKind.Ordinary && GeneratedInstanceMethodName(method) == name)
            ? _names.Get("runtime-field:" + field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "field$" + field.Name)
            : name;
    }

    private void EmitHtmlHandler(ClassDeclarationSyntax declaration)
    {
        _model = _compilation.GetSemanticModel(declaration.SyntaxTree);
        var type = _model.GetDeclaredSymbol(declaration)!;
        using var classScope = EnterGeneratedClass(type);
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
            EmitWithExpressionScope(2, () =>
            {
                foreach (var field in fields)
                    foreach (var variable in field.Declaration.Variables)
                        _output.Append("    this.").Append(GeneratedFieldName((IFieldSymbol)_model.GetDeclaredSymbol(variable)!)).Append(" = ")
                            .Append(variable.Initializer is null
                                ? DefaultFieldValue(_model.GetTypeInfo(field.Declaration.Type).Type!, variable)
                                : ValueExpression(variable.Initializer.Value)).AppendLine(";");
                if (constructor?.ExpressionBody is not null)
                    _output.Append("    ").Append(Expression(constructor.ExpressionBody.Expression)).AppendLine(";");
                else
                    EmitStatements(constructor?.Body?.Statements ?? [], 2);
            });
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
                    _output.Append("    return ").Append(ValueExpression(method.ExpressionBody.Expression)).AppendLine(";");
                else
                    EmitStatements(method.Body?.Statements ?? [], 2);
            });
            _output.AppendLine("  }");
        }
        _output.AppendLine("}").AppendLine();
    }

    // Source calls use the original C# implementation. Its exported RPC wrapper converts
    // results only when they actually leave the entrypoint or Durable Object.
    private void EmitRpcMethodBody(MethodDeclarationSyntax method, IMethodSymbol symbol)
    {
        if (IsEnumerableIterator(method, symbol))
        {
            EmitEnumerableIteratorBody(method, 2);
            return;
        }
        if (method.ExpressionBody is not null)
        {
            var value = Expression(method.ExpressionBody.Expression);
            _output.Append("    return ").Append(value).AppendLine(";");
            return;
        }
        EmitStatements(method.Body?.Statements ?? [], 2);
    }

    private bool RpcResultNeedsConversion(ITypeSymbol type) => type switch
    {
        { SpecialType: SpecialType.System_Object } => true,
        { TypeKind: TypeKind.TypeParameter } => true,
        { TypeKind: TypeKind.Interface, DeclaringSyntaxReferences.Length: > 0 } => true,
        IArrayTypeSymbol array => RpcResultNeedsConversion(array.ElementType),
        INamedTypeSymbol named when UsesKeyDictionary(named) => true,
        INamedTypeSymbol named when IsSet(named) && IsCompositeKey(named.TypeArguments[0]) => true,
        INamedTypeSymbol named when IsUserInstanceType(named) => true,
        INamedTypeSymbol named => named.TypeArguments.Any(RpcResultNeedsConversion),
        _ => false
    };

    private bool NeedsRpcResultWrapper(IMethodSymbol method) =>
        method.ContainingType.BaseType?.ToDisplayString() is "Workers.WorkerEntrypoint" or "Workers.DurableObject"
        && method.DeclaredAccessibility == Accessibility.Public
        && method.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is MethodDeclarationSyntax declaration
        && !IsIterator(declaration) && (RpcResultNeedsConversion(method.ReturnType)
            || method.Parameters.Any(parameter => RpcArgumentNeedsHydration(parameter.Type)));

    private string RpcMethodBodySlot(IMethodSymbol method) =>
        _names.Get("rpc-body:" + method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            $"rpc${method.ContainingType.Name}${method.Name}");

    private string GeneratedSourceInvocation(IMethodSymbol method, string receiver, string[] arguments, SyntaxNode source)
    {
        // JavaScript private names are valid only inside their declaring class.
        // Module functions and other generated classes use a source implementation.
        if (method.DeclaredAccessibility != Accessibility.Public
            && !SymbolEqualityComparer.Default.Equals(_generatedClassScope, method.ContainingType))
            return $"{QueueUserMethod(method, source)}.call({receiver}{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})";
        var name = GeneratedInstanceMethodName(method);
        if (!NeedsRpcResultWrapper(method))
            return $"{receiver}.{name}({string.Join(", ", arguments)})";
        var slot = RpcMethodBodySlot(method);
        if (receiver == "this") return $"this[{slot}]({string.Join(", ", arguments)})";
        // Env.Get<T> can expose a native service proxy under the source entrypoint type.
        // Only real local instances have the private implementation symbol.
        var key = $"rpc-call:{source.SyntaxTree.FilePath}:{source.SpanStart}";
        var target = _names.Get(key + ":receiver", "rpcTarget");
        var values = arguments.Select((_, index) => _names.Get($"{key}:arg:{index}", "rpcArg")).ToArray();
        var remoteArguments = arguments.Length == 0 ? "" : $"...{RequireHelperName(JavaScriptHelper.RpcArguments, "rpcArguments")}([{string.Join(", ", values)}])";
        var remoteCall = $"{target}.{name}({remoteArguments})";
        if (RpcArgumentReader(method.ReturnType, source) is { } reader)
            remoteCall = $"{reader}({remoteCall}, new WeakMap())";
        return $"(({string.Join(", ", new[] { target }.Concat(values))}) => {target} instanceof {RuntimeClassName(method.ContainingType)}"
            + $" ? {target}[{slot}]({string.Join(", ", values)}) : {remoteCall})"
            + $"({string.Join(", ", new[] { receiver }.Concat(arguments))})";
    }
}
