using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Dictionary<string, T>, List<T> and HashSet<T> members beyond indexing and enumeration. Dictionaries
// are null-prototype objects, lists are arrays and sets are Set instances.
internal sealed partial class JavaScriptEmitter
{
    private string? CollectionMemberInvocation(
        SyntaxNode source, IMethodSymbol? method, string receiver, string name, string[] arguments)
    {
        if (method is null || method.IsStatic && !method.IsExtensionMethod) return null;
        var definition = (method.ReducedFrom ?? method).ContainingType.OriginalDefinition.ToDisplayString();
        var container = method.ReducedFrom is null ? method.ContainingType : method.ReceiverType as INamedTypeSymbol;
        if (container is null) return null;
        if (KeyCollectionInvocation(source, container, receiver, name, arguments) is { } keyed) return keyed;
        string Helper(string helper, params string[] values)
        {
            _helpers.Require(JavaScriptHelper.CollectionMembers);
            return $"{_helpers.Name(helper)}({string.Join(", ", values)})";
        }

        if (IsDictionary(container) && container.TypeArguments is [{ SpecialType: SpecialType.System_String }, _]
            || IsDictionary(container) && container.AllInterfaces.Any(item => item.TypeArguments is [{ SpecialType: SpecialType.System_String }, _]))
        {
            var valueType = container.TypeArguments.Length == 2
                ? container.TypeArguments[1]
                : container.AllInterfaces.First(item => item.TypeArguments.Length == 2).TypeArguments[1];
            var mutable = definition is "System.Collections.Generic.Dictionary<TKey, TValue>"
                or "System.Collections.Generic.IDictionary<TKey, TValue>";
            return (name, arguments.Length) switch
            {
                ("ContainsKey", 1) => Helper("dictionaryContainsKey", receiver, arguments[0]),
                ("Add", 2) when mutable => Helper("dictionaryAdd", receiver, arguments[0], arguments[1]),
                ("TryAdd", 2) => Helper("dictionaryTryAdd", receiver, arguments[0], arguments[1]),
                ("Remove", 1) when mutable => Helper("dictionaryRemove", receiver, arguments[0]),
                ("Clear", 0) when mutable => Helper("dictionaryClear", receiver),
                ("GetValueOrDefault", 1) => Helper("dictionaryGetValueOrDefault", receiver, arguments[0], DefaultValueText(valueType, source)),
                ("GetValueOrDefault", 2) => Helper("dictionaryGetValueOrDefault", receiver, arguments[0], arguments[1]),
                _ => null
            };
        }

        if (definition == "System.Collections.Generic.List<T>")
        {
            var element = method.ContainingType.TypeArguments[0];
            string Predicate(string mode) => Helper("listPredicate", receiver, arguments[0], $"\"{mode}\"", DefaultValueText(element, source),
                $"value => {CopyValue(element, "value")}");
            return (name, arguments.Length) switch
            {
                ("Remove", 1) => Helper("listRemove", receiver, arguments[0], EqualityFunction(element, source)),
                ("IndexOf", 1) => Helper("listIndexOf", receiver, arguments[0], EqualityFunction(element, source)),
                ("Contains", 1) when !SupportsLinqEquality(element) =>
                    $"({Helper("listIndexOf", receiver, arguments[0], EqualityFunction(element, source))} >= 0)",
                ("RemoveAt", 1) => Helper("listRemoveAt", receiver, arguments[0]),
                ("Insert", 2) => Helper("listInsert", receiver, arguments[0], arguments[1]),
                ("Clear", 0) => $"{RequireHelperName(JavaScriptHelper.ListState, "listClear")}({receiver})",
                ("AddRange", 1) => CopyValue(element, "item") == "item" ? Helper("listAddRange", receiver, arguments[0])
                    : Helper("listAddRange", receiver, arguments[0], $"item => {CopyValue(element, "item")}"),
                ("RemoveAll", 1) => Helper("listRemoveAll", receiver, arguments[0]),
                ("Find", 1) => Predicate("find"),
                ("FindAll", 1) => CreateList(Predicate("all")),
                ("FindIndex", 1) => Predicate("index"),
                ("Exists", 1) => Predicate("exists"),
                ("TrueForAll", 1) => Predicate("every"),
                ("GetRange", 2) => CopyValue(element, "item") == "item" ? Helper("listGetRange", receiver, arguments[0], arguments[1])
                    : CreateList(CopySequenceValues(element, Helper("listGetRange", receiver, arguments[0], arguments[1]))),
                ("ToArray", 0) => MaterializeArray(method.ReturnType, $"{receiver}.slice()"),
                ("Reverse", 0) => $"{RequireHelperName(JavaScriptHelper.ListState, "listReverse")}({receiver})",
                ("Sort", 0) when UnwrapNullable(element).SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double
                    && element.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T =>
                    Helper("listSort", receiver),
                ("Sort", 1) when method.Parameters[0].Type is INamedTypeSymbol { DelegateInvokeMethod: not null } =>
                    Helper("listSort", receiver, arguments[0]),
                ("Sort", 1) when element.SpecialType == SpecialType.System_String
                    && arguments[0] is "\"Ordinal\"" or "\"OrdinalIgnoreCase\"" =>
                    Helper("listSort", receiver, arguments[0]),
                _ => null
            };
        }

        if (definition == "System.Collections.Generic.HashSet<T>" && SupportsLinqEquality(method.ContainingType.TypeArguments[0]))
            return (name, arguments.Length) switch
            {
                ("Remove", 1) => $"{receiver}.delete({arguments[0]})",
                ("Clear", 0) => $"{receiver}.clear()",
                ("UnionWith", 1) => Helper("setUnionWith", receiver, arguments[0]),
                ("IntersectWith", 1) => Helper("setIntersectWith", receiver, arguments[0]),
                ("ExceptWith", 1) => Helper("setExceptWith", receiver, arguments[0]),
                _ => null
            };
        return null;
    }

    // EqualityComparer<T>.Default semantics as a JavaScript function.
    private string EqualityFunction(ITypeSymbol type, SyntaxNode source) =>
        $"(left, right) => {ValueEquality(type, "left", "right", source)}";

    private string DefaultValueText(ITypeSymbol type, SyntaxNode source) =>
        type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? "null"
            : DefaultFieldValue(type, source);
}
