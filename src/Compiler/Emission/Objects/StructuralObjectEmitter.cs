using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
internal sealed partial class JavaScriptEmitter
{
    private string StructuralObject(
        BaseObjectCreationExpressionSyntax value,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        var type = _model.GetTypeInfo(value).Type?.ToDisplayString() ?? "";
        var assigned = value.Initializer?.Expressions
            .Select(expression => (expression as AssignmentExpressionSyntax)?.Left)
            .OfType<IdentifierNameSyntax>()
            .Select(name => LowerFirst(name.Identifier.ValueText))
            .ToHashSet() ?? [];
        var properties = CryptoFixedProperties(type).ToList();
        properties.AddRange(StructuralDefaultProperties(type)
            .Where(property => !assigned.Contains(property.Name))
            .Select(property => $"{property.Name}: {property.Value}"));
        properties.AddRange(arguments.Select((argument, index) =>
        {
            var parameter = BoundParameter(argument);
            return $"{JavaScriptObjectKey(LowerFirst(parameter.Name))}: {OptionalValue(argument.Expression)}";
        }));
        if (value.Initializer is not null)
            properties.AddRange(value.Initializer.Expressions.Select(StructuralInitializerProperty));
        return "{ " + string.Join(", ", properties) + " }";
    }

    // Property initializers of option types, which object literals would otherwise drop.
    internal static IEnumerable<(string Name, string Value)> StructuralDefaultProperties(string type) => type switch
    {
        "Workers.CookieOptions" => [("path", "\"/\""), ("httpOnly", "true"), ("secure", "true"), ("sameSite", "0"), ("partitioned", "false")],
        _ => []
    };

    private string RecordObject(
        BaseObjectCreationExpressionSyntax source,
        IMethodSymbol constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor.ContainingType.IsValueType && constructor.Parameters.Length == 0)
            return UserObjectInitializers(source, DefaultFieldValue(constructor.ContainingType, source));
        string Create(IEnumerable<string> properties) =>
            $"Object.defineProperty({{ {string.Join(", ", properties)} }}, {PlainRecordIdentitySlot}, {{ value: {PlainRecordIdentity(constructor.ContainingType)} }})";
        if (constructor.Parameters.LastOrDefault()?.IsParams == true)
            return UserObjectInitializers(source, PositionalObjectCreation(source, constructor, arguments,
                values => Create(constructor.Parameters.Select((parameter, index) =>
                    $"{JavaScriptObjectKey(LowerFirst(parameter.Name))}: {values[index]}"))));

        var supplied = arguments.Select((argument, index) =>
            (Parameter: BoundParameter(argument), Value: ArgumentValue(argument))).ToArray();
        var properties = supplied.Select(argument =>
            $"{JavaScriptObjectKey(LowerFirst(argument.Parameter.Name))}: {argument.Value}").ToList();
        properties.AddRange(constructor.Parameters
            .Where(parameter => parameter.HasExplicitDefaultValue
                                && supplied.All(argument => argument.Parameter.Ordinal != parameter.Ordinal))
            .Select(parameter =>
                $"{JavaScriptObjectKey(LowerFirst(parameter.Name))}: {ParameterDefaultValue(parameter, source)}"));
        return UserObjectInitializers(source, Create(properties));
    }

    private string PositionalObjectCreation(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments,
        Func<IReadOnlyList<string>, string> emit,
        bool isAsync = false)
    {
        var values = arguments.Select(ArgumentValue).ToArray();
        if (constructor is { DeclaringSyntaxReferences.Length: > 0 }
            && constructor.Parameters.LastOrDefault()?.IsParams == true)
        {
            var keyPrefix = $"params-constructor:{source.SyntaxTree.FilePath}:{source.SpanStart}";
            var captured = values.Select((_, index) => _names.Get($"{keyPrefix}:{index}", $"arg{index}")).ToArray();
            var body = emit(OrderCallArguments(constructor, arguments, captured, source));
            return isAsync ? $"(await (async ({string.Join(", ", captured)}) => {body})({string.Join(", ", values)}))"
                : $"(({string.Join(", ", captured)}) => {body})({string.Join(", ", values)})";
        }
        if (constructor is null || arguments.Length == 0)
            return emit(values);

        var ordinals = arguments.Select((argument, index) =>
            BoundParameter(argument).Ordinal).ToArray();
        if (ordinals.SequenceEqual(Enumerable.Range(0, ordinals.Length)))
            return emit(values);

        var key = $"constructor:{source.SyntaxTree.FilePath}:{source.SpanStart}";
        var temporaries = values.Select((_, index) => _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
        var ordered = Enumerable.Repeat("undefined", ordinals.Max() + 1).ToArray();
        for (var index = 0; index < ordinals.Length; index++)
            ordered[ordinals[index]] = temporaries[index];
        return isAsync
            ? $"(await (async ({string.Join(", ", temporaries)}) => {emit(ordered)})({string.Join(", ", values)}))"
            : $"(({string.Join(", ", temporaries)}) => {emit(ordered)})({string.Join(", ", values)})";
    }

    private string StructuralInitializerProperty(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax name =>
            $"{JavaScriptObjectKey(StructuralPropertyName(assignment.Left, name.Identifier.ValueText))}: {StructuralValue(assignment)}",
        _ => throw Unsupported("WRK106", expression)
    };

    private string StructuralPropertyName(ExpressionSyntax source, string name) =>
        _model.GetSymbolInfo(source).Symbol?.ContainingType.ToDisplayString() is { } type
            && NativePropertyNames.Get(type, name) is { } nativeName
            ? nativeName
            : LowerFirst(name);

    private string StructuralValue(AssignmentExpressionSyntax assignment)
    {
        if (assignment.Right.IsKind(SyntaxKind.NullLiteralExpression)) return "undefined";
        var value = Expression(assignment.Right);
        var symbol = _model.GetSymbolInfo(assignment.Right).Symbol;
        var type = _model.GetTypeInfo(assignment.Right);
        return type.Nullability.FlowState == NullableFlowState.MaybeNull
               || type.Type?.NullableAnnotation == NullableAnnotation.Annotated
               || assignment.Right is ConditionalExpressionSyntax or SwitchExpressionSyntax
                  && _model.GetSymbolInfo(assignment.Left).Symbol is IPropertySymbol { NullableAnnotation: NullableAnnotation.Annotated }
               || symbol is ILocalSymbol { NullableAnnotation: NullableAnnotation.Annotated }
               || symbol is IParameterSymbol { NullableAnnotation: NullableAnnotation.Annotated }
            ? $"(({value}) ?? undefined)"
            : value;
    }

    private string OptionalValue(ExpressionSyntax expression)
    {
        if (expression.IsKind(SyntaxKind.NullLiteralExpression)) return "undefined";
        var value = Expression(expression);
        var type = _model.GetTypeInfo(expression);
        return type.Nullability.FlowState == NullableFlowState.MaybeNull
               || type.Type?.NullableAnnotation == NullableAnnotation.Annotated
            ? $"(({value}) ?? undefined)"
            : value;
    }

    private string DictionaryProperty(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax
        {
            Left: ImplicitElementAccessSyntax { ArgumentList.Arguments: [{ Expression: var key }] },
            Right: var value
        } => $"[{Expression(key)}]: {ValueExpression(value)}",
        _ => throw Unsupported("WRK106", expression)
    };

    private static bool IsException(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (current.ToDisplayString() == "System.Exception") return true;
        return false;
    }
}
