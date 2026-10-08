using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// System.Text.Json.Nodes maps onto plain JSON values (see HelperSource.JsonNode): JsonObject is a
// null-prototype object, JsonArray an array and JsonValue a string, boolean or number. Every mutation
// goes through a helper that keeps the single-parent rule, so trees never share or cycle. Members that
// need a parent pointer from a value node (Parent, Root, GetPath, ReplaceWith) are not supported.
internal sealed partial class JavaScriptEmitter
{
    private const int JsonNodeAny = 0;
    private const int JsonNodeObject = 1;
    private const int JsonNodeArray = 2;
    private const int JsonNodeValue = 3;

    // 0 JsonNode, 1 JsonObject, 2 JsonArray, 3 JsonValue, or -1 for other types.
    private static int JsonNodeKind(ITypeSymbol? type) =>
        type is INamedTypeSymbol { ContainingNamespace: { } ns } named && ns.ToDisplayString() == "System.Text.Json.Nodes"
            ? named.Name switch
            {
                "JsonNode" => JsonNodeAny,
                "JsonObject" => JsonNodeObject,
                "JsonArray" => JsonNodeArray,
                "JsonValue" => JsonNodeValue,
                _ => -1
            }
            : -1;

    private static bool IsJsonNodeType(ITypeSymbol? type) => JsonNodeKind(type) >= 0;

    private string JsonNodeHelper(string function, params string[] arguments) =>
        $"{RequireHelperName(JavaScriptHelper.JsonNode, function)}({string.Join(", ", arguments)})";

    // Applies JsonNode's implicit conversions from C# values, which Roslyn records on the converted expression.
    private string ImplicitJsonNodeConversion(ExpressionSyntax expression, string emitted) =>
        _model.GetConversion(expression) is { IsUserDefined: true, MethodSymbol: { Name: "op_Implicit" } conversion }
        && IsJsonNodeType(conversion.ContainingType)
            ? JsonNodeFromValue(conversion.Parameters[0].Type, emitted, expression)
            : emitted;

    // The JsonValue a C# value becomes, written the way System.Text.Json writes that type.
    private string JsonNodeFromValue(ITypeSymbol type, string value, SyntaxNode source)
    {
        var underlying = UnwrapNullable(type);
        if (IsJsonNodeType(underlying)) return value;
        if (underlying.ToDisplayString() == "System.Text.Json.JsonElement")
            return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
                ? $"((element) => element == null ? null : {JsonNodeHelper("jsonNodeImport", "element", JsonNodeValue.ToString())})({value})"
                : JsonNodeHelper("jsonNodeImport", value, JsonNodeValue.ToString());
        return underlying.SpecialType switch
        {
            SpecialType.System_String or SpecialType.System_Boolean or SpecialType.System_Char
                or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
                or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
                or SpecialType.System_Int64 or SpecialType.System_UInt64 => value,
            SpecialType.System_Double => JsonNodeHelper("jsonNodeNumber", value),
            SpecialType.System_Single => JsonNodeHelper("jsonNodeSingle", value),
            _ when underlying.ToDisplayString() == "System.Guid" || IsDateOrTimeOnly(underlying) => value,
            _ when underlying.ToDisplayString() is "System.DateTimeOffset" or "System.DateTime" =>
                JsonNodeHelper("jsonNodeDate", value, underlying.ToDisplayString() == "System.DateTimeOffset" ? "true" : "false"),
            _ => throw new NotSupportedException(
                $"WRK105: Converting '{type.ToDisplayString()}' to a JsonNode is outside the supported Workers C# profile.")
        };
    }

    // GetValue<T>, TryGetValue<T> and the explicit conversions read values the way a parsed JsonValue does.
    private (int Kind, string Arguments) JsonNodeValueKind(ITypeSymbol target, SyntaxNode source)
    {
        var name = JsonText(target.ToDisplayString() switch
        {
            "string" => "System.String",
            "bool" => "System.Boolean",
            "char" => "System.Char",
            "sbyte" => "System.SByte",
            "byte" => "System.Byte",
            "short" => "System.Int16",
            "ushort" => "System.UInt16",
            "int" => "System.Int32",
            "uint" => "System.UInt32",
            "long" => "System.Int64",
            "ulong" => "System.UInt64",
            "float" => "System.Single",
            "double" => "System.Double",
            var other => other
        });
        string Range(string minimum, string maximum) => $"{name}, {minimum}, {maximum}";
        return target.SpecialType switch
        {
            SpecialType.System_String => (0, name),
            SpecialType.System_Boolean => (1, name),
            SpecialType.System_SByte => (2, Range("-128", "127")),
            SpecialType.System_Byte => (2, Range("0", "255")),
            SpecialType.System_Int16 => (2, Range("-32768", "32767")),
            SpecialType.System_UInt16 => (2, Range("0", "65535")),
            SpecialType.System_Int32 => (2, Range("-2147483648", "2147483647")),
            SpecialType.System_UInt32 => (2, Range("0", "4294967295")),
            SpecialType.System_Int64 => (2, Range("-9007199254740991", "9007199254740991")),
            SpecialType.System_UInt64 => (2, Range("0", "9007199254740991")),
            SpecialType.System_Single => (3, name),
            SpecialType.System_Double => (4, name),
            SpecialType.System_Char => (5, name),
            _ => target.ToDisplayString() switch
            {
                "System.DateTimeOffset" or "System.DateTime" => (7, name),
                "System.Guid" => (8, name),
                "System.Text.Json.JsonElement" => (11, name),
                _ => throw new NotSupportedException(
                    $"WRK105: Reading a JsonValue as '{target.ToDisplayString()}' is outside the supported Workers C# profile.")
            }
        };
    }

    private string JsonNodeGetValue(ITypeSymbol target, string node, SyntaxNode source)
    {
        var underlying = UnwrapNullable(target);
        var (kind, arguments) = JsonNodeValueKind(underlying, source);
        var read = JsonNodeHelper("jsonNodeValue", "node", kind.ToString(), arguments);
        // Nullable targets and string map a null node to null; other targets dereference it.
        return target.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || underlying.SpecialType == SpecialType.System_String
            ? $"((node) => node == null ? null : {read})({node})"
            : $"((node) => {read})({node})";
    }

    private string JsonNodeMember(MemberAccessExpressionSyntax member, IPropertySymbol property)
    {
        var receiverKind = JsonNodeKind(_model.GetTypeInfo(member.Expression).Type);
        return (receiverKind, property.Name) switch
        {
            (JsonNodeObject, "Count") => $"Object.keys({Expression(member.Expression)}).length",
            (JsonNodeArray, "Count") => $"{Expression(member.Expression)}.length",
            _ => throw UnsupportedSymbol(property, member)
        };
    }

    private string JsonNodeElementAccess(ElementAccessExpressionSyntax value, string? assigned = null)
    {
        var receiverKind = JsonNodeKind(_model.GetTypeInfo(value.Expression).Type);
        if (value.ArgumentList.Arguments is not [{ Expression: var index }]) throw Unsupported("WRK108", value);
        var receiver = Expression(value.Expression);
        var key = Expression(index);
        var text = _model.GetTypeInfo(index).Type?.SpecialType == SpecialType.System_String;
        var function = (receiverKind, text) switch
        {
            (JsonNodeObject, true) => "jsonObject",
            (JsonNodeArray, false) => "jsonArray",
            (_, true) => "jsonNodeGet",
            (_, false) => "jsonNodeAt"
        };
        if (function is "jsonObject" or "jsonArray")
            return JsonNodeHelper(function + (assigned is null ? "Get" : "Set"), assigned is null ? [receiver, key] : [receiver, key, assigned]);
        if (assigned is null) return JsonNodeHelper(function, receiver, key);
        return JsonNodeHelper(text ? "jsonNodeSet" : "jsonNodeSetAt", receiver, key, assigned);
    }

    private string JsonNodeInvocation(
        InvocationExpressionSyntax source,
        IMethodSymbol method,
        string receiver,
        string name,
        string[] arguments)
    {
        var owner = JsonNodeKind(method.ContainingType);
        var parameters = method.Parameters;
        bool Text(int index) => parameters.Length > index && parameters[index].Type.SpecialType == SpecialType.System_String;
        bool Integer(int index) => parameters.Length > index && parameters[index].Type.SpecialType == SpecialType.System_Int32;
        return (owner, name, arguments.Length) switch
        {
            (JsonNodeAny, "AsObject", 0) => JsonNodeHelper("jsonNodeAs", receiver, JsonNodeObject.ToString()),
            (JsonNodeAny, "AsArray", 0) => JsonNodeHelper("jsonNodeAs", receiver, JsonNodeArray.ToString()),
            (JsonNodeAny, "AsValue", 0) => JsonNodeHelper("jsonNodeAs", receiver, JsonNodeValue.ToString()),
            (JsonNodeAny, "GetValueKind", 0) => JsonNodeHelper("jsonNodeKind", receiver),
            (JsonNodeAny, "GetValue", 0) => JsonNodeGetValue(method.TypeArguments[0], receiver, source),
            (JsonNodeAny, "DeepClone", 0) => JsonNodeHelper("jsonNodeDeepClone", receiver),
            (JsonNodeAny, "ToJsonString", 0) => JsonNodeHelper("jsonNodeToJsonString", JsonNodeHelper("jsonNodeRequired", receiver)),
            (JsonNodeAny, "ToString", 0) => JsonNodeHelper("jsonNodeToString", receiver),
            (JsonNodeObject, "Add", 2) when Text(0) => JsonNodeHelper("jsonObjectAdd", receiver, arguments[0], arguments[1]),
            (JsonNodeObject, "Add", 1) =>
                $"((target, pair) => {JsonNodeHelper("jsonObjectAdd", "target", "pair[0]", "pair[1]")})({receiver}, {arguments[0]})",
            (JsonNodeObject, "TryAdd", 2) => JsonNodeHelper("jsonObjectTryAdd", receiver, arguments[0], arguments[1]),
            (JsonNodeObject, "Remove", 1) when Text(0) => JsonNodeHelper("jsonObjectRemove", receiver, arguments[0]),
            (JsonNodeObject, "ContainsKey", 1) => JsonNodeHelper("jsonObjectContainsKey", receiver, arguments[0]),
            (JsonNodeObject, "Clear", 0) => JsonNodeHelper("jsonObjectClear", receiver),
            (JsonNodeArray, "Add", 1) => JsonNodeHelper("jsonArrayAdd", receiver, method.IsGenericMethod
                ? JsonNodeFromValue(method.TypeArguments[0], arguments[0], source)
                : arguments[0]),
            (JsonNodeArray, "Insert", 2) when Integer(0) => JsonNodeHelper("jsonArrayInsert", receiver, arguments[0], arguments[1]),
            (JsonNodeArray, "RemoveAt", 1) => JsonNodeHelper("jsonArrayRemoveAt", receiver, arguments[0]),
            (JsonNodeArray, "RemoveRange", 2) => JsonNodeHelper("jsonArrayRemoveRange", receiver, arguments[0], arguments[1]),
            (JsonNodeArray, "RemoveAll", 1) => JsonNodeHelper("jsonArrayRemoveAll", receiver, arguments[0]),
            (JsonNodeArray, "Clear", 0) => JsonNodeHelper("jsonArrayClear", receiver),
            (JsonNodeArray, "GetValues", 0) =>
                $"Array.from({receiver}, item => {JsonNodeGetValue(method.TypeArguments[0], "item", source)})",
            _ => throw UnsupportedSymbol(method, source)
        };
    }

    private string? JsonNodeStaticInvocation(InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments)
    {
        var owner = JsonNodeKind(method.ContainingType);
        // Only the value argument is supported; JsonNodeOptions and JsonDocumentOptions are not.
        return (owner, method.Name, source.ArgumentList.Arguments.Count) switch
        {
            (JsonNodeAny, "Parse", 1) when method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                JsonNodeHelper("jsonNodeParse", arguments[0], JsonNodeAny.ToString()),
            (JsonNodeAny, "DeepEquals", 2) => JsonNodeHelper("jsonNodeDeepEquals", arguments[0], arguments[1]),
            (JsonNodeValue, "Create", 1) => JsonNodeFromValue(method.IsGenericMethod ? method.TypeArguments[0] : method.Parameters[0].Type,
                arguments[0], source),
            (JsonNodeObject or JsonNodeArray, "Create", 1) => JsonNodeHelper("jsonNodeImport", arguments[0], owner.ToString()),
            _ => null
        };
    }

    // new JsonObject { ["key"] = value, { "key", value } } and new JsonArray(items) { item }.
    private string CreateJsonNode(BaseObjectCreationExpressionSyntax value, IMethodSymbol? constructor, ArgumentSyntax[] arguments)
    {
        var kind = JsonNodeKind(constructor?.ContainingType);
        if (constructor is null || kind is not (JsonNodeObject or JsonNodeArray)) throw UnsupportedSymbol(constructor, value);
        var operation = _model.GetOperation(value) as Microsoft.CodeAnalysis.Operations.IObjectCreationOperation;
        string created;
        if (arguments.Length == 0)
            created = kind == JsonNodeObject ? "Object.create(null)" : "[]";
        else if (kind == JsonNodeObject && arguments.Length == 1 && constructor.Parameters[0].Type is INamedTypeSymbol
                 {
                     OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T
                 })
            created = JsonNodeHelper("jsonObjectFrom", $"{_helpers.Require(JavaScriptHelper.LinqValues)}({Expression(arguments[0].Expression)})");
        else if (kind == JsonNodeArray && constructor.Parameters is [{ IsParams: true }]
                 && operation?.Arguments is [{ ArgumentKind: not Microsoft.CodeAnalysis.Operations.ArgumentKind.Explicit }])
            created = JsonNodeHelper("jsonArrayFrom", "[" + string.Join(", ", arguments.Select(argument => Expression(argument.Expression))) + "]");
        else if (kind == JsonNodeArray && arguments.Length == 1 && constructor.Parameters is [{ Type: IArrayTypeSymbol }])
            created = JsonNodeHelper("jsonArrayFrom", $"{_helpers.Require(JavaScriptHelper.LinqValues)}({Expression(arguments[0].Expression)})");
        else
            throw UnsupportedSymbol(constructor, value);
        if (value.Initializer is null) return created;
        var target = _names.Get($"json-node-target:{value.SyntaxTree.FilePath}:{value.SpanStart}", "target");

        var steps = value.Initializer.Expressions.Select(expression => expression switch
        {
            AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax { ArgumentList.Arguments: [{ } key] } } assignment =>
                JsonNodeHelper(kind == JsonNodeObject ? "jsonObjectSet" : "jsonArraySet", target, Expression(key.Expression), Expression(assignment.Right)) + ";",
            InitializerExpressionSyntax { Expressions: [var key, var item] } when kind == JsonNodeObject =>
                JsonNodeHelper("jsonObjectAdd", target, Expression(key), Expression(item)) + ";",
            _ when value.Initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
                   && _model.GetCollectionInitializerSymbolInfo(expression).Symbol is IMethodSymbol add =>
                kind == JsonNodeObject
                    ? $"((pair) => {JsonNodeHelper("jsonObjectAdd", target, "pair[0]", "pair[1]")})({Expression(expression)});"
                    : JsonNodeHelper("jsonArrayAdd", target,
                        add.IsGenericMethod ? JsonNodeFromValue(add.TypeArguments[0], Expression(expression), expression) : Expression(expression)) + ";",
            _ => throw Unsupported("WRK106", expression)
        });
        return $"(({target}) => {{ {string.Join(" ", steps)} return {target}; }})({created})";
    }
}
