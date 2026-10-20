using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Text.Json;
internal sealed partial class JavaScriptEmitter
{
    private string ObjectCreation(IObjectCreationOperation operation)
    {
        var value = (BaseObjectCreationExpressionSyntax)operation.Syntax;
        var constructor = operation.Constructor is { } boundConstructor ? ResolveMethod(boundConstructor) : null;
        var type = constructor?.ContainingType;
        var arguments = value.ArgumentList?.Arguments.ToArray() ?? [];
        var typeName = type?.ToDisplayString();
        if (IsIndexType(type))
        {
            _helpers.Require(JavaScriptHelper.IndexRange);
            return PositionalObjectCreation(value, constructor, arguments,
                values => $"{_helpers.Name("indexCreate")}({string.Join(", ", values)})");
        }
        if (IsRangeType(type)) return PositionalObjectCreation(value, constructor, arguments, values => $"[{string.Join(", ", values)}]");
        if (type?.SpecialType == SpecialType.System_Object && arguments.Length == 0) return "{}";
        if (typeName is "Workers.Date" or "Workers.RegExp" or "Workers.AbortController" or "Workers.FormData"
            or "Workers.TextEncoder" or "Workers.WebSocketPair" or "Workers.WebSocketRequestResponsePair"
            or "Workers.TransformStream" or "Workers.IdentityTransformStream" or "Workers.FixedLengthStream"
            or "Workers.CompressionStream" or "Workers.DecompressionStream")
            return PositionalObjectCreation(value, constructor, arguments, values => $"new {type!.Name}({string.Join(", ", values)})");
        if (typeName == "Workers.UrlSearchParams")
            return PositionalObjectCreation(value, constructor, arguments, values => $"new URLSearchParams({string.Join(", ", values)})");
        if (typeName is "Workers.Blob" or "Workers.File" or "Workers.TextDecoder" or "Workers.DigestStream" or "Workers.EmailMessage")
            return PositionalObjectCreation(value, constructor, arguments, values => NativeObjectCreation(typeName, values));
        // KeyValuePair values are [key, value] arrays, the shape Object.entries and Map iteration produce.
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>"
            && constructor?.Parameters.Length == 2 && value.Initializer is null)
            return PositionalObjectCreation(value, constructor, arguments, values => $"[{values[0]}, {values[1]}]");
        if (typeName == "Workers.Response") return ResponseObject(operation, value, constructor, arguments);
        if (typeName is "Workers.Request" or "Workers.Headers")
            return NativeObjectInitializer(operation, PositionalObjectCreation(value, constructor, arguments,
                values => $"new {type!.Name}({string.Join(", ", values)})"));
        if (typeName == "Workers.HtmlRewriter") return "new HTMLRewriter()";
        if (typeName == "Workers.Router") return CreateRouter();
        if (typeName == "Workers.HttpError")
            return PositionalObjectCreation(value, constructor, arguments,
                values => $"new {_helpers.Require(JavaScriptHelper.HttpError)}({string.Join(", ", values)})");
        if (IsException(type) && IsUserInstanceType(type!))
            return UserObject(value, constructor, type!, arguments);
        if (IsException(type))
            return FrameworkException(constructor, type!, arguments);
        if (typeName == "Workers.Url") return CreateUrl(value, constructor, arguments);
        if (type is not null && (UsesKeyDictionary(type) || IsSet(type) && IsCompositeKey(type.TypeArguments[0])))
            return CreateKeyCollection(type, value, constructor!, arguments);
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && arguments.Length == 0)
            return CreateList($"[{string.Join(", ", value.Initializer?.Expressions.Select(ValueExpression) ?? [])}]");
        if (type?.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.List<T>" or "System.Collections.Generic.HashSet<T>"
            && arguments.Length == 1 && value.Initializer is null && constructor?.Parameters.Length == 1)
        {
            var set = type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>";
            if (set && !SupportsLinqEquality(type.TypeArguments[0])) throw UnsupportedSymbol(constructor, value);
            var parameter = constructor.Parameters[0].Type;
            if (parameter.SpecialType == SpecialType.System_Int32 && !set)
                return $"((capacity) => {{ if (capacity < 0) throw {NewBuiltInException("System.ArgumentOutOfRangeException", "capacity must be non-negative.")}; return {CreateList("[]")}; }})({Expression(arguments[0].Expression)})";
            if (parameter.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                var copy = Linq(JavaScriptHelper.LinqToArray, Expression(arguments[0].Expression), []);
                return set ? CreateSet(copy) : CreateList(CopySequenceValues(type.TypeArguments[0], copy));
            }
        }
        if (type?.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.Queue<T>" or
            "System.Collections.Generic.Stack<T>")
            return CreateQueueOrStack(value, constructor!, arguments,
                type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Stack<T>");
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
            && arguments.Length == 0 && SupportsLinqEquality(type.TypeArguments[0]))
            return CreateSet($"[{string.Join(", ", value.Initializer?.Expressions.Select(Expression) ?? [])}]");
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>"
            && type.TypeArguments[0].SpecialType == SpecialType.System_String && arguments.Length == 0)
        {
            var properties = string.Join(", ", value.Initializer?.Expressions.Select(DictionaryProperty) ?? []);
            return properties.Length == 0
                ? "Object.create(null)"
                : $"Object.assign(Object.create(null), {{ {properties} }})";
        }
        if (type is not null && type.BaseType?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler")
            return type.DeclaringSyntaxReferences.Length == 1
                   && type.DeclaringSyntaxReferences[0].GetSyntax() is ClassDeclarationSyntax
                ? PositionalObjectCreation(value, constructor, arguments,
                    values => $"new {UserIdentifier(type, type.Name)}({string.Join(", ", values)})")
                : throw UnsupportedSymbol(constructor, value);
        if (type is not null && BindingIntrinsicRegistry.IsStructuralType(type))
            return StructuralObject(value, constructor, arguments);
        if (type is not null && IsUserInstanceType(type))
            ValidateJsonAttributes(type);
        if (type is { IsRecord: true } && IsUserInstanceType(type) && !RequiresUserClass(type))
            return RecordObject(value, constructor!, arguments);
        if (type is not null && IsUserInstanceType(type))
            return UserObject(value, constructor, type, arguments);
        throw UnsupportedSymbol(constructor, value);
    }

    private string ResponseObject(IObjectCreationOperation operation, BaseObjectCreationExpressionSyntax syntax,
        IMethodSymbol? constructor, ArgumentSyntax[] arguments) =>
        PositionalObjectCreation(syntax, constructor, arguments, values =>
        {
            if (operation.Initializer is null) return $"new Response({string.Join(", ", values)})";
            var body = values.Count > 0 ? values[0] : "null";
            if (values.Count == 1 || values.Count == 0) return Create(body, null);
            // Capture the source once: its native properties are inherited, then initializers override them.
            var key = $"response:{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}";
            var capturedBody = _names.Get(key + ":body", "body");
            var source = _names.Get(key + ":source", "source");
            var creation = Create(capturedBody, source);
            return ContainsAwait(operation.Initializer)
                ? $"(await (async ({capturedBody}, {source}) => {creation})({body}, {values[1]}))"
                : $"(({capturedBody}, {source}) => {creation})({body}, {values[1]})";

            string Create(string bodyValue, string? source)
            {
                var settings = source is null ? new List<string>()
                    : new List<string> { $"status: {source}.status", $"statusText: {source}.statusText", $"headers: {source}.headers", $"webSocket: {source}.webSocket", $"cf: {source}.cf" };
                foreach (var item in operation.Initializer.Initializers)
                {
                    switch (item)
                    {
                        case ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment
                            when property.Property.ContainingType.ToDisplayString() == "Workers.Response":
                            settings.Add($"{LowerFirst(property.Property.Name)}: {Expression(assignment.Value)}");
                            break;
                        case IMemberInitializerOperation { InitializedMember: IPropertyReferenceOperation { Property.Name: "Headers" } } headers:
                            settings.Add($"headers: {NativeObjectInitializer(headers.Initializer, source is null ? "new Headers()" : $"new Headers({source}.headers)")}");
                            break;
                        default: throw Unsupported("WRK106", item.Syntax);
                    }
                }
                return $"new Response({bodyValue}, {{ {string.Join(", ", settings)} }})";
            }
        }, operation.Initializer is not null && ContainsAwait(operation.Initializer));

    // Constructors whose JavaScript form wraps arguments: blob parts are an array, and options are a dictionary.
    private string NativeObjectCreation(string type, IReadOnlyList<string> values)
    {
        string Option(int index, string name) => values.Count > index ? $"{{ {name}: ({values[index]}) ?? undefined }}" : "undefined";
        return type switch
        {
            "Workers.Blob" => $"new Blob([{values[0]}], {Option(1, "type")})",
            "Workers.File" => $"new File([{values[0]}], {values[1]}, {Option(2, "type")})",
            "Workers.TextDecoder" => $"new TextDecoder({(values.Count > 0 ? values[0] : "undefined")}, {{ fatal: {(values.Count > 1 ? values[1] : "false")}, ignoreBOM: {(values.Count > 2 ? values[2] : "false")} }})",
            "Workers.DigestStream" => $"new globalThis.crypto.DigestStream({string.Join(", ", values)})",
            _ => $"new {_imports.Require("cloudflare:email", "EmailMessage", "EmailMessage")}({string.Join(", ", values)})"
        };
    }

    private string NativeObjectInitializer(IObjectCreationOperation operation, string creation)
    {
        if (operation.Initializer is null) return creation;
        return NativeObjectInitializer(operation.Initializer, creation);
    }

    private string NativeObjectInitializer(IObjectOrCollectionInitializerOperation initializer, string creation)
    {
        var source = initializer.Syntax;
        var receiver = _names.Get($"initializer:{source.SyntaxTree.FilePath}:{source.SpanStart}", "value");
        var statements = Initialize(initializer, receiver);
        return ContainsAwait(initializer)
            ? $"(await (async ({receiver}) => {{ {statements}return {receiver}; }})({creation}))"
            : $"(({receiver}) => {{ {statements}return {receiver}; }})({creation})";

        string Initialize(IObjectOrCollectionInitializerOperation initializer, string target) =>
            string.Concat(initializer.Initializers.Select(item => item switch
            {
                IMemberInitializerOperation { InitializedMember: IPropertyReferenceOperation property } nested
                    when property.Property.Name == "Headers" => Initialize(nested.Initializer, $"{target}.headers"),
                ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment
                    when property.Property.IsIndexer && property.Property.ContainingType.ToDisplayString() == "Workers.Headers" =>
                    $"{target}.set({Expression(property.Arguments.Single().Value)}, {Expression(assignment.Value)}); ",
                _ => throw Unsupported("WRK106", item.Syntax)
            }));
    }

    private string FrameworkException(IMethodSymbol? constructor, INamedTypeSymbol type, ArgumentSyntax[] arguments)
    {
        var values = arguments.Select(ArgumentValue).ToArray();
        var temporaries = arguments.Select((_, index) => _names.Get($"exception-argument:{arguments[index].SyntaxTree.FilePath}:{arguments[index].SpanStart}", "argument")).ToArray();
        string? Argument(string name) => arguments.Select((argument, index) => (Name: BoundParameter(argument).Name, Value: temporaries[index]))
            .Where(item => item.Name == name).Select(item => item.Value).SingleOrDefault();
        if (arguments.Any(argument => BoundParameter(argument).Name is not ("message" or "paramName" or "innerException" or "actualValue")))
            throw UnsupportedSymbol(constructor, arguments[0]);
        var fallback = JsonSerializer.Serialize(DefaultExceptionMessage(type));
        var message = Argument("message") is { } text ? $"({text} ?? {fallback})" : fallback;
        if (Argument("paramName") is { } parameter)
            message = $"((message, parameter) => parameter == null ? message : `${{message}} (Parameter '${{parameter}}')`)({message}, {parameter})";
        var inner = Argument("innerException");
        var result = $"new ({BuiltInExceptionType(type)})({message}, {(inner is null ? "undefined" : "{ cause: " + inner + " }")}, {Argument("paramName") ?? "undefined"})";
        if (Argument("actualValue") is { } actual) result = $"Object.assign({result}, {{ actualValue: {actual} }})";
        return arguments.Length == 0 ? result : $"(({string.Join(", ", temporaries)}) => {result})({string.Join(", ", values)})";
    }

    internal static string DefaultExceptionMessage(INamedTypeSymbol type) => type.ToDisplayString() switch
    {
        "System.InvalidOperationException" => "Operation is not valid due to the current state of the object.",
        "System.ArgumentException" => "Value does not fall within the expected range.",
        "System.ArgumentNullException" => "Value cannot be null.",
        "System.ArgumentOutOfRangeException" => "Specified argument was out of the range of valid values.",
        "System.NotSupportedException" => "Specified method is not supported.",
        "System.NotImplementedException" => "The method or operation is not implemented.",
        "System.FormatException" => "One of the identified items was in an invalid format.",
        "System.Collections.Generic.KeyNotFoundException" => "The given key was not present in the dictionary.",
        "System.OperationCanceledException" or "System.Threading.Tasks.TaskCanceledException" => "The operation was canceled.",
        "System.TimeoutException" => "The operation has timed out.",
        "System.UnauthorizedAccessException" => "Attempted to perform an unauthorized operation.",
        var name => $"Exception of type '{name}' was thrown."
    };

}
