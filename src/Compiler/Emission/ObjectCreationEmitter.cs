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
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>" && arguments.Length == 0)
            return $"new Set([{string.Join(", ", value.Initializer?.Expressions.Select(Expression) ?? [])}])";
        if (type?.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>"
            && type.TypeArguments[0].SpecialType == SpecialType.System_String)
            return "{ " + string.Join(", ", value.Initializer?.Expressions.Select(DictionaryProperty) ?? []) + " }";
        if (type is not null && type.BaseType?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler")
            return type.DeclaringSyntaxReferences.Length == 1
                   && type.DeclaringSyntaxReferences[0].GetSyntax() is ClassDeclarationSyntax
                ? PositionalObjectCreation(value, constructor, arguments,
                    values => $"new {UserIdentifier(type, type.Name)}({string.Join(", ", values)})")
                : throw UnsupportedSymbol(constructor, value);
        if (type is not null && BindingIntrinsicRegistry.IsStructuralType(type))
            return StructuralObject(value, constructor, arguments);
        if (type is { IsRecord: true } && type.DeclaringSyntaxReferences.Length != 0)
            return RecordObject(constructor!, arguments);
        throw UnsupportedSymbol(constructor, value);
    }

    private string CreateUrl(SyntaxNode source, IMethodSymbol? constructor, ArgumentSyntax[] arguments) =>
        PositionalObjectCreation(source, constructor, arguments, values => values.Count switch
        {
            1 => $"new URL({values[0]})",
            2 when constructor?.ContainingType.ToDisplayString() == "System.Uri" =>
                $"new URL({values[1]}, {values[0]})",
            2 => $"new URL({values[0]}, {values[1]})",
            _ => throw UnsupportedSymbol(constructor, source)
        });

    private string StructuralObject(
        BaseObjectCreationExpressionSyntax value,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        var properties = arguments.Select((argument, index) =>
        {
            var parameter = ArgumentParameter(constructor!, argument, index);
            return $"{LowerFirst(parameter.Name)}: {OptionalValue(argument.Expression)}";
        }).ToList();
        if (value.Initializer is not null)
            properties.AddRange(value.Initializer.Expressions.Select(StructuralInitializerProperty));
        return "{ " + string.Join(", ", properties) + " }";
    }

    private string RecordObject(IMethodSymbol constructor, ArgumentSyntax[] arguments)
    {
        var supplied = arguments.Select((argument, index) =>
            (Parameter: ArgumentParameter(constructor, argument, index), Value: Expression(argument.Expression))).ToArray();
        var properties = supplied.Select(argument => $"{LowerFirst(argument.Parameter.Name)}: {argument.Value}").ToList();
        var source = constructor.DeclaringSyntaxReferences.Single().GetSyntax();
        properties.AddRange(constructor.Parameters
            .Where(parameter => parameter.HasExplicitDefaultValue
                                && supplied.All(argument => !SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter)))
            .Select(parameter => $"{LowerFirst(parameter.Name)}: {LiteralConstant(parameter.ExplicitDefaultValue, source)}"));
        return "{ " + string.Join(", ", properties) + " }";
    }

    private string PositionalObjectCreation(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments,
        Func<IReadOnlyList<string>, string> emit)
    {
        var values = arguments.Select(argument => Expression(argument.Expression)).ToArray();
        if (constructor is null || arguments.Length == 0)
            return emit(values);

        var ordinals = arguments.Select((argument, index) =>
            ArgumentParameter(constructor, argument, index).Ordinal).ToArray();
        if (ordinals.SequenceEqual(Enumerable.Range(0, ordinals.Length)))
            return emit(values);

        var key = $"constructor:{source.SyntaxTree.FilePath}:{source.SpanStart}";
        var temporaries = values.Select((_, index) => _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
        var ordered = Enumerable.Repeat("undefined", ordinals.Max() + 1).ToArray();
        for (var index = 0; index < ordinals.Length; index++)
            ordered[ordinals[index]] = temporaries[index];
        return $"(({string.Join(", ", temporaries)}) => {emit(ordered)})({string.Join(", ", values)})";
    }

    private static IParameterSymbol ArgumentParameter(
        IMethodSymbol constructor,
        ArgumentSyntax argument,
        int position) => argument.NameColon is { } name
        ? constructor.Parameters.Single(parameter => parameter.Name == name.Name.Identifier.ValueText)
        : constructor.Parameters[position];

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

    private static bool IsException(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (current.ToDisplayString() == "System.Exception") return true;
        return false;
    }
}
