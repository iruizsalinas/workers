using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Lowers C# patterns to JavaScript conditions over a value that is safe to read repeatedly.
// Pattern variables are assigned inside the condition, so their names must already be declared:
// switch expressions declare them in the generated function, while "is" patterns and pattern case
// labels declare them before the enclosing statement.
internal sealed partial class JavaScriptEmitter
{
    private readonly HashSet<ISymbol> _declaredPatternVariables = new(SymbolEqualityComparer.Default);

    private string PatternTest(PatternSyntax pattern, string value, ITypeSymbol? input) => pattern switch
    {
        DiscardPatternSyntax => "true",
        ParenthesizedPatternSyntax parenthesized => PatternTest(parenthesized.Pattern, value, input),
        ConstantPatternSyntax constant => ConstantPatternTest(constant, value),
        RelationalPatternSyntax relational => RelationalPatternTest(relational, value),
        BinaryPatternSyntax binary => $"({PatternTest(binary.Left, value, input)} " +
            $"{(binary.IsKind(SyntaxKind.AndPattern) ? "&&" : "||")} {PatternTest(binary.Right, value, input)})",
        UnaryPatternSyntax { Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } } } unary
            when unary.IsKind(SyntaxKind.NotPattern) => $"{value} != null",
        UnaryPatternSyntax unary when unary.IsKind(SyntaxKind.NotPattern) => $"!({PatternTest(unary.Pattern, value, input)})",
        VarPatternSyntax var => Bind(var.Designation, value, "true"),
        DeclarationPatternSyntax declaration =>
            Bind(declaration.Designation, value, TypePatternTest(declaration.Type, value, input, pattern)),
        TypePatternSyntax type => TypePatternTest(type.Type, value, input, pattern),
        RecursivePatternSyntax recursive => RecursivePatternTest(recursive, value, input),
        ListPatternSyntax list => ListPatternTest(list, value, input),
        _ => throw Unsupported("WRK104", pattern)
    };

    private string ConstantPatternTest(ConstantPatternSyntax constant, string value)
    {
        if (constant.Expression.IsKind(SyntaxKind.NullLiteralExpression)) return $"{value} == null";
        if (_model.GetConstantValue(constant.Expression) is { HasValue: true, Value: double or float } number
            && double.IsNaN(Convert.ToDouble(number.Value)))
            return $"Number.isNaN({value})";
        return $"{value} === {Expression(constant.Expression)}";
    }

    private string RelationalPatternTest(RelationalPatternSyntax relational, string value)
    {
        var operation = relational.OperatorToken.Kind() switch
        {
            SyntaxKind.LessThanToken => "<",
            SyntaxKind.LessThanEqualsToken => "<=",
            SyntaxKind.GreaterThanToken => ">",
            SyntaxKind.GreaterThanEqualsToken => ">=",
            _ => throw Unsupported("WRK104", relational)
        };
        return $"({value} != null && {value} {operation} {Expression(relational.Expression)})";
    }

    private string Bind(VariableDesignationSyntax designation, string value, string condition)
    {
        if (designation is DiscardDesignationSyntax) return condition;
        if (designation is not SingleVariableDesignationSyntax single
            || _model.GetDeclaredSymbol(single) is not { } symbol)
            throw Unsupported("WRK104", designation);
        if (!_declaredPatternVariables.Contains(symbol))
            throw new NotSupportedException(
                "WRK104: A pattern variable is only supported inside a statement or switch expression; use a block body.");
        return $"({condition} && (({UserIdentifier(symbol, single.Identifier)} = {value}), true))";
    }

    private string TypePatternTest(TypeSyntax typeSyntax, string value, ITypeSymbol? input, SyntaxNode source)
    {
        var type = _model.GetTypeInfo(typeSyntax).Type;
        if (type is null) throw Unsupported("WRK104", source);
        var unwrappedInput = input is null ? null : UnwrapNullable(input);
        if (unwrappedInput is not null && SymbolEqualityComparer.Default.Equals(unwrappedInput, type))
            return $"{value} != null";
        if (JsonNodeKind(type) is var nodeKind and >= 0)
            return nodeKind == JsonNodeAny ? $"{value} != null" : JsonNodeHelper("jsonNodeIs", value, nodeKind.ToString());
        if (type.SpecialType == SpecialType.System_String) return $"typeof {value} === \"string\"";
        if (type.SpecialType == SpecialType.System_Boolean) return $"typeof {value} === \"boolean\"";
        if (type is INamedTypeSymbol named && IsUserInstanceType(named) && (IsException(named) || RequiresUserClass(named)))
            return $"{value} instanceof {QueueUserType(named, source)}";
        throw new NotSupportedException(
            $"WRK104: Testing for type '{type.ToDisplayString()}' is not supported at runtime; only string, bool and Worker class types can be tested.");
    }

    private string RecursivePatternTest(RecursivePatternSyntax recursive, string value, ITypeSymbol? input)
    {
        var conditions = new List<string>();
        var type = input;
        if (recursive.Type is not null)
        {
            conditions.Add(TypePatternTest(recursive.Type, value, input, recursive));
            type = _model.GetTypeInfo(recursive.Type).Type;
        }
        else
            conditions.Add($"{value} != null");
        if (recursive.PositionalPatternClause is { } positional)
        {
            if (type is not INamedTypeSymbol { IsRecord: true } record || !IsUserInstanceType(record)
                || record.DeclaringSyntaxReferences[0].GetSyntax() is not RecordDeclarationSyntax { ParameterList: { } parameters }
                || parameters.Parameters.Count != positional.Subpatterns.Count)
                throw Unsupported("WRK104", positional);
            for (var index = 0; index < positional.Subpatterns.Count; index++)
            {
                var property = record.GetMembers(parameters.Parameters[index].Identifier.ValueText).OfType<IPropertySymbol>().Single();
                conditions.Add(SubpatternTest(positional.Subpatterns[index].Pattern, UserMemberAccess(value, property), property.Type));
            }
        }
        foreach (var subpattern in recursive.PropertyPatternClause?.Subpatterns ?? default)
        {
            var member = subpattern.ExpressionColon?.Expression ?? subpattern.NameColon?.Name
                ?? throw Unsupported("WRK104", subpattern);
            var (access, memberType) = PatternMemberAccess(member, value);
            conditions.Add(SubpatternTest(subpattern.Pattern, access, memberType));
        }
        var condition = string.Join(" && ", conditions);
        return recursive.Designation is null ? $"({condition})" : Bind(recursive.Designation, value, $"({condition})");
    }

    // Nested patterns read their member once through a parameter so the value can be inspected repeatedly.
    private string SubpatternTest(PatternSyntax pattern, string access, ITypeSymbol? type)
    {
        var parameter = _names.Get($"pattern:{pattern.SyntaxTree.FilePath}:{pattern.SpanStart}", "member");
        return $"(({parameter}) => {PatternTest(pattern, parameter, type)})({access})";
    }

    private (string Access, ITypeSymbol? Type) PatternMemberAccess(ExpressionSyntax member, string value)
    {
        if (member is MemberAccessExpressionSyntax chain)
        {
            var (receiver, _) = PatternMemberAccess(chain.Expression, value);
            return PatternMemberAccess(chain.Name, receiver);
        }
        var symbol = _model.GetSymbolInfo(member).Symbol;
        var type = symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => throw Unsupported("WRK104", member)
        };
        if (symbol.ContainingType is { } owner && IsUserInstanceType(owner))
            return (UserMemberAccess(value, symbol), type);
        if (RegexCaptureField(symbol) is { } regexField)
            return ($"{value}.{regexField}", type);
        if (symbol.Name == "Count" && IsRegexType(symbol.ContainingType))
            return ($"{value}.length", type);
        if (symbol.Name is "Length" or "Count"
            && (symbol.ContainingType.SpecialType == SpecialType.System_String
                || symbol.ContainingType.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.List<T>"
                    or "System.Array" or "System.Collections.Generic.IReadOnlyList<T>"
                    or "System.Collections.Generic.IReadOnlyCollection<T>" or "System.Collections.Generic.ICollection<T>"))
            return ($"{value}.length", type);
        throw Unsupported("WRK104", member);
    }

    private string ListPatternTest(ListPatternSyntax list, string value, ITypeSymbol? input)
    {
        var element = input switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { SpecialType: SpecialType.System_String } => _compilation.GetSpecialType(SpecialType.System_Char),
            INamedTypeSymbol named when named.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.List<T>"
                or "System.Collections.Generic.IReadOnlyList<T>" => named.TypeArguments[0],
            _ => throw Unsupported("WRK104", list)
        };
        var sliceIndex = list.Patterns.IndexOf(pattern => pattern is SlicePatternSyntax);
        var fixedCount = list.Patterns.Count - (sliceIndex >= 0 ? 1 : 0);
        var conditions = new List<string>
        {
            $"{value} != null",
            sliceIndex >= 0 ? $"{value}.length >= {fixedCount}" : $"{value}.length === {fixedCount}"
        };
        for (var index = 0; index < list.Patterns.Count; index++)
        {
            var item = list.Patterns[index];
            if (item is SlicePatternSyntax slice)
            {
                var after = list.Patterns.Count - index - 1;
                if (slice.Pattern is not null)
                    conditions.Add(SubpatternTest(slice.Pattern, $"{value}.slice({index}, {value}.length - {after})", input));
                continue;
            }
            var position = sliceIndex >= 0 && index > sliceIndex
                ? $"{value}[{value}.length - {list.Patterns.Count - index}]"
                : $"{value}[{index}]";
            conditions.Add(SubpatternTest(item, position, element));
        }
        var condition = $"({string.Join(" && ", conditions)})";
        return list.Designation is null ? condition : Bind(list.Designation, value, condition);
    }

    // Pattern variables declared by a statement's "is" patterns and pattern case labels, excluding
    // those owned by nested statements, lambdas and switch expressions.
    private IEnumerable<SingleVariableDesignationSyntax> StatementPatternDesignations(StatementSyntax statement) =>
        statement
            .DescendantNodes(node => node == statement
                || node is not (StatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
                    or SwitchExpressionSyntax))
            .Where(node => node is PatternSyntax && node.Ancestors().TakeWhile(ancestor => ancestor != statement)
                .All(ancestor => ancestor is not SwitchExpressionSyntax))
            .SelectMany(pattern => pattern.DescendantNodesAndSelf(node => node is not SwitchExpressionSyntax))
            .OfType<SingleVariableDesignationSyntax>()
            .Distinct();

    // Variables of different arms or sections may share a name; they share one JavaScript variable,
    // which is safe because each pattern assigns it before its arm reads it.
    private void DeclarePatternVariables(IEnumerable<SingleVariableDesignationSyntax> designations, Action<string> declare)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var designation in designations)
        {
            var symbol = _model.GetDeclaredSymbol(designation);
            if (symbol is null || !_declaredPatternVariables.Add(symbol)) continue;
            var name = UserIdentifier(symbol, designation.Identifier);
            if (names.Add(name)) declare(name);
        }
    }

    private string IsPattern(IsPatternExpressionSyntax value)
    {
        var subject = Expression(value.Expression);
        var input = _model.GetTypeInfo(value.Expression).Type;
        if (value.Expression is IdentifierNameSyntax or ThisExpressionSyntax)
            return PatternTest(value.Pattern, subject, input);
        // Constant and null checks read the subject once, so no temporary is needed.
        if (value.Pattern is ConstantPatternSyntax or UnaryPatternSyntax { Pattern: ConstantPatternSyntax })
            return PatternTest(value.Pattern, value.Expression is MemberAccessExpressionSyntax or InvocationExpressionSyntax
                or ElementAccessExpressionSyntax ? subject : $"({subject})", input);
        var parameter = _names.Get($"is:{value.SyntaxTree.FilePath}:{value.SpanStart}", "subject");
        return $"(({parameter}) => {PatternTest(value.Pattern, parameter, input)})({subject})";
    }

    private string SwitchExpression(SwitchExpressionSyntax value)
    {
        var parameter = _names.Get($"switch:{value.SyntaxTree.FilePath}:{value.SpanStart}", "subject");
        var input = _model.GetTypeInfo(value.GoverningExpression).Type;
        var variables = new List<string>();
        DeclarePatternVariables(
            value.Arms.SelectMany(arm => arm.Pattern.DescendantNodesAndSelf(node => node is not SwitchExpressionSyntax))
                .OfType<SingleVariableDesignationSyntax>(),
            variables.Add);
        var isAsync = value.Arms.Any(arm => arm.DescendantNodes(node => node is not AnonymousFunctionExpressionSyntax)
            .OfType<AwaitExpressionSyntax>().Any());
        var body = new List<string>();
        if (variables.Count != 0) body.Add($"let {string.Join(", ", variables)};");
        foreach (var arm in value.Arms)
        {
            var condition = PatternTest(arm.Pattern, parameter, input);
            if (arm.WhenClause is not null) condition = $"{condition} && ({Expression(arm.WhenClause.Condition)})";
            var result = arm.Expression is ThrowExpressionSyntax thrown
                ? $"throw {Expression(thrown.Expression)};"
                : $"return {Expression(arm.Expression)};";
            body.Add(condition == "true" ? result : $"if ({condition}) {result}");
        }
        body.Add("throw new Error(\"The switch expression does not handle all possible values.\");");
        var function = $"({(isAsync ? "async " : "")}({parameter}) => {{ {string.Join(" ", body)} }})({Expression(value.GoverningExpression)})";
        return isAsync ? $"(await {function})" : function;
    }
}
