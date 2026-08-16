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
        if (typeName is "Workers.Request" or "Workers.Response")
            return $"new {type!.Name}({Arguments(arguments)})";
        if (typeName == "Workers.AbortController") return "new AbortController()";
        if (typeName == "Workers.HtmlRewriter") return "new HTMLRewriter()";
        if (typeName == "Workers.Headers") return "new Headers()";
        if (typeName == "Workers.WebSocketAutoResponse")
            return $"new WebSocketRequestResponsePair({Expression(arguments[0].Expression)}, {Expression(arguments[1].Expression)})";
        if (IsException(type))
            return $"new Error({(arguments.Length == 0 ? "undefined" : Expression(arguments[0].Expression))})";
        if (typeName is "System.Uri" or "Workers.Url") return CreateUrl(value, constructor, arguments);
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && arguments.Length == 0)
            return "[]";
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>"
            && type.TypeArguments[0].SpecialType == SpecialType.System_String)
            return "{ " + string.Join(", ", value.Initializer?.Expressions.Select(DictionaryProperty) ?? []) + " }";
        if (type is not null && type.BaseType?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler")
            return type.DeclaringSyntaxReferences.Length == 1
                   && type.DeclaringSyntaxReferences[0].GetSyntax() is ClassDeclarationSyntax
                ? $"new {UserIdentifier(type, type.Name)}({Arguments(arguments)})"
                : throw UnsupportedSymbol(constructor, value);
        if (type is not null && BindingIntrinsicRegistry.IsStructuralType(type))
            return StructuralObject(value, constructor, arguments);
        if (type is { IsRecord: true } && type.DeclaringSyntaxReferences.Length != 0)
            return RecordObject(constructor!, arguments);
        throw UnsupportedSymbol(constructor, value);
    }

    private string CreateUrl(SyntaxNode source, IMethodSymbol? constructor, ArgumentSyntax[] arguments) =>
        arguments.Length switch
        {
            1 => $"new URL({Expression(arguments[0].Expression)})",
            2 => $"new URL({Expression(arguments[1].Expression)}, {Expression(arguments[0].Expression)})",
            _ => throw UnsupportedSymbol(constructor, source)
        };

    private string StructuralObject(
        BaseObjectCreationExpressionSyntax value,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        var properties = arguments.Select((argument, index) =>
        {
            var name = argument.NameColon?.Name.Identifier.Text ?? constructor!.Parameters[index].Name;
            return $"{LowerFirst(name)}: {OptionalValue(argument.Expression)}";
        }).ToList();
        if (value.Initializer is not null)
            properties.AddRange(value.Initializer.Expressions.Select(StructuralInitializerProperty));
        return "{ " + string.Join(", ", properties) + " }";
    }

    private string RecordObject(IMethodSymbol constructor, ArgumentSyntax[] arguments) => "{ "
        + string.Join(", ", arguments.Select((argument, index) =>
            $"{LowerFirst(constructor.Parameters[index].Name)}: {Expression(argument.Expression)}")) + " }";

    private string StructuralInitializerProperty(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax name =>
            $"{LowerFirst(name.Identifier.Text)}: {StructuralValue(assignment)}",
        _ => throw Unsupported("WRK106", expression)
    };

    private string StructuralValue(AssignmentExpressionSyntax assignment)
    {
        if (assignment.Right.IsKind(SyntaxKind.NullLiteralExpression)) return "undefined";
        var value = Expression(assignment.Right);
        var symbol = _model.GetSymbolInfo(assignment.Right).Symbol;
        var type = _model.GetTypeInfo(assignment.Right);
        return type.Nullability.FlowState == NullableFlowState.MaybeNull
               || type.Type?.NullableAnnotation == NullableAnnotation.Annotated
               || symbol is ILocalSymbol { NullableAnnotation: NullableAnnotation.Annotated }
               || symbol is IParameterSymbol { NullableAnnotation: NullableAnnotation.Annotated }
            ? $"({value} ?? undefined)"
            : value;
    }

    private string OptionalValue(ExpressionSyntax expression)
    {
        if (expression.IsKind(SyntaxKind.NullLiteralExpression)) return "undefined";
        var value = Expression(expression);
        var type = _model.GetTypeInfo(expression);
        return type.Nullability.FlowState == NullableFlowState.MaybeNull
               || type.Type?.NullableAnnotation == NullableAnnotation.Annotated
            ? $"({value} ?? undefined)"
            : value;
    }

    private string DictionaryProperty(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax
        {
            Left: ImplicitElementAccessSyntax { ArgumentList.Arguments: [{ Expression: var key }] },
            Right: var value
        } => $"[{Expression(key)}]: {Expression(value)}",
        _ => throw Unsupported("WRK106", expression)
    };

    private string Arguments(IEnumerable<ArgumentSyntax> arguments) =>
        string.Join(", ", arguments.Select(argument => Expression(argument.Expression)));

    private static bool IsException(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (current.ToDisplayString() == "System.Exception") return true;
        return false;
    }
}
