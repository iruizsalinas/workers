using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
internal sealed partial class JavaScriptEmitter
{
    private string ObjectCreation(BaseObjectCreationExpressionSyntax value)
    {
        var constructor = _model.GetSymbolInfo(value).Symbol as IMethodSymbol;
        var type = constructor?.ContainingType;
        var arguments = value.ArgumentList?.Arguments.ToArray() ?? [];
        var typeName = type?.ToDisplayString();
        if (typeName == "System.TimeSpan")
            return CreateTimeSpan(value, constructor, arguments);
        if (typeName == "System.DateTimeOffset")
            return CreateDateTimeOffset(value, constructor, arguments);
        if (typeName == "System.Threading.CancellationTokenSource" && arguments.Length == 0)
            return "new AbortController()";
        if (typeName == "System.Text.StringBuilder")
            return CreateStringBuilder(value, constructor, arguments);
        if (typeName == "System.Text.RegularExpressions.Regex")
            return CreateRegex(value, constructor, arguments);
        if (typeName is "Workers.Request" or "Workers.Response")
            return PositionalObjectCreation(value, constructor, arguments,
                values => $"new {type!.Name}({string.Join(", ", values)})");
        if (typeName == "Workers.AbortController") return "new AbortController()";
        if (typeName == "Workers.HtmlRewriter") return "new HTMLRewriter()";
        if (typeName == "Workers.Headers") return "new Headers()";
        if (typeName == "Workers.WebSocketAutoResponse")
            return PositionalObjectCreation(value, constructor, arguments,
                values => $"new WebSocketRequestResponsePair({string.Join(", ", values)})");
        if (IsException(type) && IsUserInstanceType(type!))
            return UserObject(value, constructor, type!, arguments);
        if (IsException(type))
            return FrameworkException(constructor, type!, arguments);
        if (typeName is "System.Uri" or "Workers.Url") return CreateUrl(value, constructor, arguments);
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && arguments.Length == 0)
            return $"[{string.Join(", ", value.Initializer?.Expressions.Select(Expression) ?? [])}]";
        if (type?.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.List<T>" or "System.Collections.Generic.HashSet<T>"
            && arguments.Length == 1 && value.Initializer is null && constructor?.Parameters.Length == 1)
        {
            var set = type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>";
            if (set && !SupportsLinqEquality(type.TypeArguments[0])) throw UnsupportedSymbol(constructor, value);
            var parameter = constructor.Parameters[0].Type;
            if (parameter.SpecialType == SpecialType.System_Int32 && !set)
                return $"((capacity) => {{ if (capacity < 0) throw new RangeError(\"capacity must be non-negative.\"); return []; }})({Expression(arguments[0].Expression)})";
            if (parameter.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                var copy = Linq(JavaScriptHelper.LinqToArray, Expression(arguments[0].Expression), []);
                return set ? $"new Set({copy})" : copy;
            }
        }
        if (type?.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.Queue<T>" or
            "System.Collections.Generic.Stack<T>")
            return CreateQueueOrStack(value, constructor!, arguments,
                type.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Stack<T>");
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>"
            && arguments.Length == 0 && SupportsLinqEquality(type.TypeArguments[0]))
            return $"new Set([{string.Join(", ", value.Initializer?.Expressions.Select(Expression) ?? [])}])";
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

    private string FrameworkException(IMethodSymbol? constructor, INamedTypeSymbol type, ArgumentSyntax[] arguments)
    {
        ExpressionSyntax? Argument(string name)
        {
            if (constructor is null) return null;
            for (var index = 0; index < arguments.Length; index++)
                if (ArgumentParameter(constructor, arguments[index], index).Name == name)
                    return arguments[index].Expression;
            return null;
        }

        var fallback = JsonSerializer.Serialize(DefaultExceptionMessage(type));
        var message = Argument("message") switch
        {
            null => fallback,
            var text when _model.GetConstantValue(text) is { HasValue: true, Value: string constant } =>
                JsonSerializer.Serialize(constant),
            var text => $"(({Expression(text)}) ?? {fallback})"
        };
        if (Argument("paramName") is { } parameter)
            message = $"((message, parameter) => parameter == null ? message : `${{message}} (Parameter '${{parameter}}')`)({message}, {Expression(parameter)})";
        if (type.ToDisplayString() is "System.OperationCanceledException" or "System.Threading.Tasks.TaskCanceledException")
            return Argument("innerException") is { } cancellationInner
                ? $"Object.assign(new DOMException({message}, \"AbortError\"), {{ cause: {Expression(cancellationInner)} }})"
                : $"new DOMException({message}, \"AbortError\")";
        return Argument("innerException") is { } inner
            ? $"new Error({message}, {{ cause: {Expression(inner)} }})"
            : $"new Error({message})";
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
