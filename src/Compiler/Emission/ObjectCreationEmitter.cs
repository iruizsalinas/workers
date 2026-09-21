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

    private string CreateTimeSpan(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor is null || arguments.Length is < 3 or > 5
            || constructor.Parameters.Any(parameter => parameter.Type.SpecialType != SpecialType.System_Int32))
            throw UnsupportedSymbol(constructor, source);
        return PositionalObjectCreation(source, constructor, arguments, values =>
        {
            var offset = values.Count == 3 ? 0 : 1;
            var days = values.Count == 3 ? "0" : values[0];
            var milliseconds = values.Count == 5 ? values[4] : "0";
            var total = $"((({days}) * 24 + ({values[offset]})) * 60 + ({values[offset + 1]})) * 60000"
                + $" + ({values[offset + 2]}) * 1000 + ({milliseconds})";
            return $"{_helpers.Require(JavaScriptHelper.TimeSpan)}({total})";
        });
    }

    private string CreateStringBuilder(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor is null
            || constructor.Parameters.Length > 1
            || constructor.Parameters.Any(parameter => parameter.Type.SpecialType != SpecialType.System_String))
            throw UnsupportedSymbol(constructor, source);
        return PositionalObjectCreation(source, constructor, arguments, values =>
            $"{_helpers.Require(JavaScriptHelper.StringBuilder)}({string.Join(", ", values)})");
    }

    private string CreateDateTimeOffset(
        SyntaxNode source,
        IMethodSymbol? constructor,
        ArgumentSyntax[] arguments)
    {
        if (constructor is null || constructor.Parameters.Length is not (7 or 8)
            || constructor.Parameters[^1].Name != "offset"
            || constructor.Parameters[^1].Type.ToDisplayString() != "System.TimeSpan")
            throw UnsupportedSymbol(constructor, source);

        var offset = arguments.Select((argument, index) => new
            {
                Argument = argument,
                Parameter = ArgumentParameter(constructor, argument, index)
            })
            .SingleOrDefault(item => item.Parameter.Name == "offset")?.Argument.Expression;
        var offsetSymbol = offset is null ? null : _model.GetSymbolInfo(offset).Symbol;
        if (offsetSymbol is null || !offsetSymbol.IsStatic || offsetSymbol.Name != "Zero"
            || offsetSymbol.ContainingType?.ToDisplayString() != "System.TimeSpan")
            throw Unsupported("WRK108", offset ?? source);

        var helper = _helpers.Require(JavaScriptHelper.DateTimeOffset);
        return PositionalObjectCreation(source, constructor, arguments, values =>
            $"{helper}({string.Join(", ", values.Take(values.Count - 1))})");
    }

    private string UserObject(
        BaseObjectCreationExpressionSyntax source,
        IMethodSymbol? constructor,
        INamedTypeSymbol type,
        ArgumentSyntax[] arguments)
    {
        var typeName = QueueUserType(type, source);
        var creation = PositionalObjectCreation(source, constructor, arguments,
            values => $"new {typeName}({string.Join(", ", values)})");
        if (source.Initializer is null || source.Initializer.Expressions.Count == 0)
            return creation;

        var temporary = _names.Get(
            $"object-initializer:{source.SyntaxTree.FilePath}:{source.SpanStart}",
            "value");
        var assignments = source.Initializer.Expressions.Select(expression => expression switch
        {
            AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax =>
                $"{UserInitializerMemberAccess(temporary, assignment.Left)} = {Expression(assignment.Right)};",
            _ => throw Unsupported("WRK106", expression)
        });
        return $"(({temporary}) => {{ {string.Join(" ", assignments)} return {temporary}; }})({creation})";
    }

    private string UserInitializerMemberAccess(string receiver, ExpressionSyntax expression) =>
        _model.GetSymbolInfo(expression).Symbol is { } symbol
            ? UserMemberAccess(receiver, symbol)
            : throw UnsupportedSymbol(null, expression);

    private string UserInitializerMemberName(ExpressionSyntax expression) =>
        _model.GetSymbolInfo(expression).Symbol is { } symbol
            ? UserMemberName(symbol)
            : throw UnsupportedSymbol(null, expression);

    private string CreateUrl(SyntaxNode source, IMethodSymbol? constructor, ArgumentSyntax[] arguments) =>
        PositionalObjectCreation(source, constructor, arguments, values => values.Count switch
        {
            1 => $"new URL({values[0]})",
            2 when constructor?.ContainingType.ToDisplayString() == "System.Uri"
                   && constructor.Parameters[0].Type.ToDisplayString() == "System.Uri"
                   && constructor.Parameters[1].Type.SpecialType == SpecialType.System_String =>
                $"new URL({values[1]}, {values[0]})",
            2 when constructor?.ContainingType.ToDisplayString() == "Workers.Url" => $"new URL({values[0]}, {values[1]})",
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
            return $"{JavaScriptObjectKey(LowerFirst(parameter.Name))}: {OptionalValue(argument.Expression)}";
        }).ToList();
        if (value.Initializer is not null)
            properties.AddRange(value.Initializer.Expressions.Select(StructuralInitializerProperty));
        return "{ " + string.Join(", ", properties) + " }";
    }

    private string RecordObject(
        BaseObjectCreationExpressionSyntax source,
        IMethodSymbol constructor,
        ArgumentSyntax[] arguments)
    {
        var supplied = arguments.Select((argument, index) =>
            (Parameter: ArgumentParameter(constructor, argument, index), Value: Expression(argument.Expression))).ToArray();
        var properties = supplied.Select(argument =>
            $"{JavaScriptObjectKey(LowerFirst(argument.Parameter.Name))}: {argument.Value}").ToList();
        properties.AddRange(constructor.Parameters
            .Where(parameter => parameter.HasExplicitDefaultValue
                                && supplied.All(argument => !SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter)))
            .Select(parameter =>
                $"{JavaScriptObjectKey(LowerFirst(parameter.Name))}: {LiteralConstant(parameter.ExplicitDefaultValue, source)}"));
        if (source.Initializer is not null)
            properties.AddRange(source.Initializer.Expressions.Select(expression => expression switch
            {
                AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax =>
                    $"{JavaScriptObjectKey(UserInitializerMemberName(assignment.Left))}: {Expression(assignment.Right)}",
                _ => throw Unsupported("WRK106", expression)
            }));
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
            $"{JavaScriptObjectKey(LowerFirst(name.Identifier.ValueText))}: {StructuralValue(assignment)}",
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
