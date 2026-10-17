using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private readonly Stack<string> _initializerReceivers = new();

    private string BoundInitializers(IObjectOrCollectionInitializerOperation initializer, string receiver)
    {
        _initializerReceivers.Push(receiver);
        try
        {
            return string.Join(" ", initializer.Initializers.Select(operation =>
            {
                if (operation is IMemberInitializerOperation member)
                {
                    if (member.InitializedMember is IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
                    {
                        var captures = indexer.Arguments.Select((_, i) => _names.Get($"initializer-index:{member.Syntax.SyntaxTree.FilePath}:{member.Syntax.SpanStart}:{i}", "index")).ToArray();
                        var arguments = indexer.Arguments.Select(argument => ValueExpression(argument.Value)).ToArray();
                        var instance = Expression(indexer.Instance!);
                        var indexedAccess = IsUserIndexer(indexer.Property) ? UserIndexerReference(indexer, instance, captures) + ".value"
                            : IsDictionary(indexer.Instance?.Type) ? UsesKeyDictionary(indexer.Instance?.Type) ? $"{instance}.get({captures.Single()})"
                                : HelperInvocation(JavaScriptHelper.DictionaryIndex, [instance, captures.Single()])
                            : throw Unsupported("WRK106", member.Syntax);
                        var body = BoundInitializers(member.Initializer, indexedAccess);
                        var asynchronous = ContainsAwait(member.Initializer);
                        var initialization = $"({(asynchronous ? "async " : "")}({string.Join(", ", captures)}) => {{ {body} }})({string.Join(", ", arguments)})";
                        return asynchronous ? $"await {initialization};" : initialization + ";";
                    }
                    var access = Expression(member.InitializedMember);
                    // C# reevaluates the initialized member for each nested element.
                    // Capturing a getter here would change both side effects and storage.
                    return BoundInitializers(member.Initializer, access);
                }
                if (operation is IInvocationOperation call) return InitializerCall(call) + ";";
                return Expression(operation) + ";";
            }));
        }
        finally { _initializerReceivers.Pop(); }
    }

    private string InitializerCall(IInvocationOperation operation)
    {
        var method = ResolveMethod(operation.TargetMethod);
        var receiver = operation.Instance is null ? null : Expression(operation.Instance);
        var arguments = operation.Arguments.OrderBy(argument => argument.Parameter!.Ordinal)
            .Select(argument => ValueExpression(argument.Value)).ToArray();
        if (!method.IsStatic && NeedsNullCheck(operation.Instance))
        {
            var parameters = arguments.Select((_, i) => $"arg{i}").ToArray();
            return $"((receiver{(parameters.Length == 0 ? "" : ", " + string.Join(", ", parameters))}) => {InitializerCallCore(operation, method, NotNull("receiver"), parameters)})"
                + $"({receiver}{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})";
        }
        return InitializerCallCore(operation, method, receiver, arguments);
    }

    private string InitializerCallCore(IInvocationOperation operation, IMethodSymbol method, string? receiver, string[] arguments)
    {
        var type = method.ContainingType;
        if (type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && method.Name == "Add")
            return $"{RequireHelperName(JavaScriptHelper.ListState, "listAdd")}({receiver}, {arguments.Single()})";
        if (IsSet(type) && method.Name == "Add") return $"{receiver}.add({arguments.Single()})";
        if (CollectionMemberInvocation(operation.Syntax, method, receiver!, method.Name, arguments) is { } collection) return collection;
        if (method.DeclaringSyntaxReferences.Length != 0)
        {
            if (method.IsStatic) return $"{QueueUserMethod(method, operation.Syntax)}({string.Join(", ", arguments)})";
            QueueUserType(type, operation.Syntax);
            if (method.IsGenericMethod) return $"{GenericMethodTarget(method)}.call({receiver}{(arguments.Length == 0 ? "" : ", " + string.Join(", ", arguments))})";
            return $"{receiver}.{UserInstanceMethodName(method)}({string.Join(", ", arguments)})";
        }
        throw UnsupportedSymbol(method, operation.Syntax);
    }
}
