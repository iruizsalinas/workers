using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string FormEntryMember(SyntaxNode member, IPropertySymbol property, string receiver)
    {
        var value = _names.Get(
            $"form-entry:{member.SyntaxTree.FilePath}:{member.SpanStart}",
            "formEntry");
        return property.Name switch
        {
            "File" => $"(({value}) => {value} instanceof File ? {value} : null)({receiver})",
            "Text" => $"(({value}) => typeof {value} === \"string\" ? {value} : null)({receiver})",
            _ => throw UnsupportedSymbol(property, member)
        };
    }

    private static bool IsDictionary(ITypeSymbol? type) => type is INamedTypeSymbol named
        && (named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.Dictionary<TKey, TValue>" or
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"));

    private static bool IsSet(ITypeSymbol? type) => type is INamedTypeSymbol named
        && (named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.HashSet<T>" or
                "System.Collections.Generic.ISet<T>" or
                "System.Collections.Generic.IReadOnlySet<T>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.ISet<T>" or
                "System.Collections.Generic.IReadOnlySet<T>"));

    private static void ThrowIfUnsupportedFoundationMember(ISymbol? symbol, SyntaxNode source)
    {
        if (symbol?.ContainingNamespace.ToDisplayString() is { } @namespace
            && (@namespace == "System" || @namespace.StartsWith("System.", StringComparison.Ordinal)))
            throw UnsupportedSymbol(symbol, source);
    }

    private static bool IsMethodGroup(ExpressionSyntax syntax)
    {
        var target = syntax.Parent is MemberAccessExpressionSyntax access && access.Name == syntax ? access : syntax;
        return !(target.Parent is InvocationExpressionSyntax invocation && invocation.Expression == target);
    }

    // A method group converted to a delegate, such as items.Select(Format).
    private string MethodGroup(ExpressionSyntax syntax, IMethodSymbol method, ExpressionSyntax? receiver)
    {
        method = _model.GetSymbolInfo(syntax).Symbol as IMethodSymbol ?? method;
        method = (IMethodSymbol)StaticInterfaceImplementation(method, receiver is null ? null : _model.GetTypeInfo(receiver).Type, syntax);
        var reduced = method.ReducedFrom is not null;
        method = ResolveMethod(method.GetConstructedReducedFrom() ?? method);
        var target = _names.Get($"delegate-target:{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}", "delegateTarget");
        var core = MethodGroupCore(syntax, method, receiver, target, reduced);
        string identity;
        var localThis = false;
        var localValues = false;
        if (method.MethodKind == MethodKind.LocalFunction) identity = LocalDelegateIdentity(method, syntax, out localThis, out localValues);
        else if (method.IsGenericMethod && !method.IsStatic) identity = receiver is BaseExpressionSyntax ? QueueUserMethod(method, syntax) : GenericMethodTarget(method);
        else if (method.IsStatic) identity = QueueUserMethod(method, syntax);
        else if (IsSourceInterface(method.ContainingType) && _runtimeInterfaceCalls.TryGetValue(method, out var dispatcher)) identity = dispatcher;
        else identity = receiver is BaseExpressionSyntax
            ? $"{QueueUserType(method.ContainingType, syntax)}.prototype.{UserInstanceMethodName(method)}"
            : $"{target}.{UserInstanceMethodName(method)}";
        var instance = reduced || !method.IsStatic ? receiver is null ? "this" : Expression(receiver) : "null";
        if (method.MethodKind == MethodKind.LocalFunction && !localThis && !localValues) instance = "null";
        if (!reduced && !method.IsStatic && method.MethodKind != MethodKind.LocalFunction)
        {
            var exception = method.IsVirtual || method.IsOverride || method.IsAbstract ? NullReferenceException()
                : NewBuiltInException("System.ArgumentException", "Delegate to an instance method cannot have null 'this'.");
            // Validate before reading the method identity from the target's prototype.
            return $"(({target}) => {{ if ({target} == null) throw {exception}; return {DelegateHelper("delegateCreate")}({target}, {identity}, {core}); }})({instance})";
        }
        return $"(({target}) => {DelegateHelper("delegateCreate")}({target}, {identity}, {core}))({instance})";
    }

    private string MethodGroupCore(ExpressionSyntax syntax, IMethodSymbol method, ExpressionSyntax? receiver, string receiverValue, bool reduced)
    {
        if (method.MethodKind == MethodKind.LocalFunction)
            return $"{LocalFunctionTarget(method, syntax)}.bind({receiverValue})";
        if (!method.IsStatic && method.IsGenericMethod && method.DeclaringSyntaxReferences.Length != 0)
        {
            var helper = receiver is BaseExpressionSyntax ? QueueUserMethod(method, syntax) : GenericMethodTarget(method);
            return $"{helper}.bind({receiverValue})";
        }
        if (reduced && method.DeclaringSyntaxReferences.Length != 0)
        {
            var helper = QueueUserMethod(method, syntax);
            var key = $"extension-group:{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}";
            var target = _names.Get(key + ":target", "target");
            var parameters = method.Parameters.Skip(1).Select((_, index) =>
                _names.Get($"{key}:{index}", $"arg{index + 1}")).ToArray();
            return $"(({target}) => ({string.Join(", ", parameters)}) => " +
                $"{helper}({string.Join(", ", new[] { target }.Concat(parameters))}))" +
                $"({receiverValue})";
        }
        if (method.IsStatic && method.MethodKind == MethodKind.Ordinary
            && method.DeclaringSyntaxReferences is [{ } reference]
            && reference.GetSyntax() is MethodDeclarationSyntax)
            return QueueUserMethod(method, syntax);
        if (!method.IsStatic && method.MethodKind == MethodKind.Ordinary
            && method.ContainingType is { } type
            && (IsSourceInterface(type) || IsUserInstanceType(type) && RequiresUserClass(type)))
        {
            if (!IsSourceInterface(type)) QueueUserType(type, syntax);
            var key = $"method-group:{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}";
            var parameterValues = method.Parameters.Select((_, index) => _names.Get($"{key}:{index}", $"arg{index + 1}")).ToArray();
            var parameters = string.Join(", ", parameterValues);
            var call = IsSourceInterface(type) ? SourceInterfaceInvocation(method, receiverValue, parameterValues, syntax)
                : receiver is BaseExpressionSyntax
                ? $"{QueueUserType(type, syntax)}.prototype.{UserInstanceMethodName(method)}.call({receiverValue}{(parameters.Length == 0 ? "" : ", " + parameters)})"
                : $"{receiverValue}.{UserInstanceMethodName(method)}({parameters})";
            return $"({parameters}) => {call}";
        }
        throw new NotSupportedException(
            $"WRK105: Converting the method group '{method.ToDisplayString()}' to a delegate is not supported; use a lambda instead.");
    }

    private string LiteralConstant(object? value, SyntaxNode source) => value switch
    {
        null => "null",
        string text => JavaScriptStringLiteral(text),
        char character => JavaScriptStringLiteral(character.ToString()),
        bool boolean => boolean ? "true" : "false",
        // A float constant is the nearest single-precision value; JavaScript numbers are doubles.
        float single => ((double)single).ToString("R", CultureInfo.InvariantCulture),
        byte or sbyte or short or ushort or int or uint or double =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        long signed when Math.Abs((double)signed) <= 9_007_199_254_740_991d =>
            signed.ToString(CultureInfo.InvariantCulture),
        ulong unsigned when unsigned <= 9_007_199_254_740_991UL =>
            unsigned.ToString(CultureInfo.InvariantCulture),
        _ => throw Unsupported("WRK108", source)
    };
}
