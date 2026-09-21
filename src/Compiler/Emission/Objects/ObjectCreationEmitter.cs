using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        if (typeName is "Workers.Request" or "Workers.Response")
            return PositionalObjectCreation(value, constructor, arguments,
                values => $"new {type!.Name}({string.Join(", ", values)})");
        if (typeName == "Workers.AbortController") return "new AbortController()";
        if (typeName == "Workers.HtmlRewriter") return "new HTMLRewriter()";
        if (typeName == "Workers.Headers") return "new Headers()";
        if (typeName == "Workers.WebSocketAutoResponse")
            return PositionalObjectCreation(value, constructor, arguments,
                values => $"new WebSocketRequestResponsePair({string.Join(", ", values)})");
        if (IsException(type))
            return $"new Error({(arguments.Length == 0 ? "undefined" : Expression(arguments[0].Expression))})";
        if (typeName is "System.Uri" or "Workers.Url") return CreateUrl(value, constructor, arguments);
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && arguments.Length == 0)
            return $"[{string.Join(", ", value.Initializer?.Expressions.Select(Expression) ?? [])}]";
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

}
