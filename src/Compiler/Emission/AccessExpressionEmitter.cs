using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private readonly Stack<(IConditionalAccessOperation Access, string Receiver)> _conditionalReceivers = new();
    private readonly Stack<List<string>> _expressionTemporaryScopes = new();

    private void EmitWithExpressionScope(int depth, Action emit)
    {
        var temporaries = new List<string>();
        var start = _output.Length;
        _expressionTemporaryScopes.Push(temporaries);
        try { emit(); }
        finally { _expressionTemporaryScopes.Pop(); }
        if (temporaries.Count != 0)
            _output.Insert(start, new string(' ', depth * 2) + $"let {string.Join(", ", temporaries)};" + Environment.NewLine);
    }

    private string ConditionalAccess(IConditionalAccessOperation operation)
    {
        var receiver = Expression(operation.Operation);
        var source = operation.Syntax;
        var temporary = _names.Get($"conditional:{source.SyntaxTree.FilePath}:{source.SpanStart}", "receiver");
        _conditionalReceivers.Push((operation, temporary));
        try
        {
            var body = Expression(operation.WhenNotNull);
            if (ContainsAwait(operation.WhenNotNull))
            {
                if (!_expressionTemporaryScopes.TryPeek(out var temporaries))
                    throw Unsupported("WRK108", operation.Syntax);
                temporaries.Add(temporary);
                // Keep await in its original function. An async wrapper would unwrap returned
                // tasks and suspend even when a null receiver skips the awaited argument.
                return $"(({temporary} = {receiver}), {temporary} == null ? null : ({body} ?? null))";
            }
            return $"(({temporary}) => {temporary} == null ? null : ({body} ?? null))({receiver})";
        }
        finally
        {
            _conditionalReceivers.Pop();
        }
    }

    private string ConditionalReceiver(IConditionalAccessInstanceOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
            if (parent is IConditionalAccessOperation access)
                foreach (var entry in _conditionalReceivers)
                    if (ReferenceEquals(entry.Access, access)) return entry.Receiver;
        throw Unsupported("WRK101", operation.Syntax);
    }

    private string ElementAccess(IPropertyReferenceOperation operation)
    {
        if (IsUserIndexer(operation.Property)) return UserIndexerReference(operation) + ".value";
        if (operation.Instance is null) throw Unsupported("WRK101", operation.Syntax);
        var receiver = Expression(operation.Instance);
        var arguments = operation.Arguments.Select(argument => ValueExpression(argument.Value)).ToArray();
        var type = operation.Instance.Type;
        if (UsesKeyDictionary(type)) return $"((receiver, key) => {NotNull("receiver")}.get(key))({receiver}, {arguments.Single()})";
        if (type?.ToDisplayString() == "Workers.Headers")
            return $"((receiver, key) => {NotNull("receiver")}.get(key))({receiver}, {arguments.Single()})";
        if (type is INamedTypeSymbol lookup && lookup.OriginalDefinition.ToDisplayString() == "System.Linq.ILookup<TKey, TElement>")
            return $"((receiver, key) => {NotNull("receiver")}.get(key))({receiver}, {arguments.Single()})";
        if (IsSequenceType(type)) return HelperInvocation(JavaScriptHelper.SequenceIndex, [receiver, arguments.Single()]);
        if (IsDictionary(type)) return HelperInvocation(JavaScriptHelper.DictionaryIndex, [receiver, arguments.Single()]);
        if (arguments.Length != 1) throw Unsupported("WRK101", operation.Syntax);
        return $"{receiver}[{arguments[0]}]";
    }

    private bool IsSequenceType(ITypeSymbol? type)
    {
        type = type is null ? null : ResolveType(type);
        if (type?.SpecialType == SpecialType.System_String || type is IArrayTypeSymbol) return true;
        return type is INamedTypeSymbol named
            && (named.OriginalDefinition.ToDisplayString() is
                    "System.Collections.Generic.List<T>" or
                    "System.Collections.Generic.IList<T>" or
                    "System.Collections.Generic.IReadOnlyList<T>"
                || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                    "System.Collections.Generic.IList<T>" or
                    "System.Collections.Generic.IReadOnlyList<T>"));
    }
}
