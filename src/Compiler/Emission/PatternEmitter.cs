using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// Lowers C# patterns to JavaScript conditions over a value that is safe to read repeatedly.
// Pattern variables are assigned inside the condition, so their names must already be declared:
// switch expressions declare them in the generated function, while "is" patterns and pattern case
// labels declare them before the enclosing statement.
internal sealed partial class JavaScriptEmitter
{
    private readonly HashSet<ISymbol> _declaredPatternVariables = new(SymbolEqualityComparer.Default);

    private string PatternTest(IPatternOperation operation, string value) => operation switch
    {
        IDiscardPatternOperation => "true",
        IConstantPatternOperation constant => ConstantPatternTest(constant, value),
        IRelationalPatternOperation relational =>
            $"({value} != null && {value} {BinaryOperator(relational.OperatorKind)} {Expression(relational.Value)})",
        IBinaryPatternOperation binary => $"({PatternTest(binary.LeftPattern, value)} " +
            $"{(binary.OperatorKind == BinaryOperatorKind.And ? "&&" : "||")} {PatternTest(binary.RightPattern, value)})",
        INegatedPatternOperation { Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } } } => $"{value} != null",
        INegatedPatternOperation negated => $"!({PatternTest(negated.Pattern, value)})",
        IDeclarationPatternOperation declaration => Bind(declaration.DeclaredSymbol, value,
            declaration.MatchesNull ? "true" : TypePatternTest(declaration.MatchedType!, value, declaration.InputType, declaration.Syntax)),
        ITypePatternOperation type => TypePatternTest(type.MatchedType, value, type.InputType, type.Syntax),
        IRecursivePatternOperation recursive => RecursivePatternTest(recursive, value),
        IListPatternOperation list => ListPatternTest(list, value),
        _ => throw Unsupported("WRK104", operation.Syntax)
    };

    private string ConstantPatternTest(IConstantPatternOperation constant, string value)
    {
        if (constant.Value.ConstantValue is { HasValue: true, Value: null }) return $"{value} == null";
        if (constant.Value.ConstantValue is { HasValue: true, Value: double or float } number
            && double.IsNaN(Convert.ToDouble(number.Value)))
            return $"Number.isNaN({value})";
        return $"{value} === {Expression(constant.Value)}";
    }

    private string Bind(ISymbol? symbol, string value, string condition)
    {
        if (symbol is null) return condition;
        if (!_declaredPatternVariables.Contains(symbol))
            throw new NotSupportedException(
                "WRK104: A pattern variable is only supported inside a statement or switch expression; use a block body.");
        return $"({condition} && (({UserIdentifier(symbol, symbol.Name)} = {value}), true))";
    }

    private string TypePatternTest(ITypeSymbol type, string value, ITypeSymbol? input, SyntaxNode source)
    {
        var unwrappedInput = input is null ? null : UnwrapNullable(input);
        if (unwrappedInput is not null && SymbolEqualityComparer.Default.Equals(unwrappedInput, type))
            return $"{value} != null";
        if (type.SpecialType == SpecialType.System_String) return $"typeof {value} === \"string\"";
        if (type.SpecialType == SpecialType.System_Boolean) return $"typeof {value} === \"boolean\"";
        if (type.ToDisplayString() == "Workers.HttpError")
            return $"{value} instanceof {_helpers.Require(JavaScriptHelper.HttpError)}";
        if (type is INamedTypeSymbol named && IsUserInstanceType(named) && (IsException(named) || RequiresUserClass(named)))
            return $"{value} instanceof {QueueUserType(named, source)}";
        throw new NotSupportedException(
            $"WRK104: Testing for type '{type.ToDisplayString()}' is not supported at runtime; only string, bool and Worker class types can be tested.");
    }

    private string RecursivePatternTest(IRecursivePatternOperation operation, string value)
    {
        var recursive = (RecursivePatternSyntax)operation.Syntax;
        var conditions = new List<string>();
        var type = operation.MatchedType;
        if (recursive.Type is not null)
        {
            conditions.Add(TypePatternTest(type, value, operation.InputType, recursive));
        }
        else
            conditions.Add($"{value} != null");
        if (operation.DeconstructionSubpatterns.Length != 0)
        {
            if (type is not INamedTypeSymbol { IsRecord: true } record || !IsUserInstanceType(record)
                || operation.DeconstructSymbol is not IMethodSymbol { IsImplicitlyDeclared: true } deconstruct)
                throw Unsupported("WRK104", recursive);
            for (var index = 0; index < operation.DeconstructionSubpatterns.Length; index++)
            {
                var property = record.GetMembers(deconstruct.Parameters[index].Name).OfType<IPropertySymbol>().Single();
                conditions.Add(SubpatternTest(operation.DeconstructionSubpatterns[index], UserMemberAccess(value, property)));
            }
        }
        foreach (var subpattern in operation.PropertySubpatterns)
        {
            var access = PatternMemberAccess(subpattern.Member, value);
            conditions.Add(SubpatternTest(subpattern.Pattern, access));
        }
        var condition = string.Join(" && ", conditions);
        return Bind(operation.DeclaredSymbol, value, $"({condition})");
    }

    // Nested patterns read their member once through a parameter so the value can be inspected repeatedly.
    private string SubpatternTest(IPatternOperation pattern, string access)
    {
        var source = pattern.Syntax;
        var parameter = _names.Get($"pattern:{source.SyntaxTree.FilePath}:{source.SpanStart}", "member");
        return $"(({parameter}) => {PatternTest(pattern, parameter)})({access})";
    }

    private string PatternMemberAccess(IOperation operation, string value)
    {
        var symbol = operation switch
        {
            IPropertyReferenceOperation property => (ISymbol)property.Property,
            IFieldReferenceOperation field => field.Field,
            _ => throw Unsupported("WRK104", operation.Syntax)
        };
        var instance = ((IMemberReferenceOperation)operation).Instance;
        if (instance is IMemberReferenceOperation) value = PatternMemberAccess(instance, value);
        else if (instance is not IInstanceReferenceOperation) throw Unsupported("WRK104", operation.Syntax);
        if (symbol.ContainingType is { } owner && IsUserInstanceType(owner))
            return UserMemberAccess(value, symbol);
        if (symbol.Name is "Length" or "Count"
            && (symbol.ContainingType.SpecialType == SpecialType.System_String
                || symbol.ContainingType.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.List<T>"
                    or "System.Array" or "System.Collections.Generic.IReadOnlyList<T>"
                    or "System.Collections.Generic.IReadOnlyCollection<T>" or "System.Collections.Generic.ICollection<T>"))
            return $"{value}.length";
        throw Unsupported("WRK104", operation.Syntax);
    }

    private string ListPatternTest(IListPatternOperation operation, string value)
    {
        var list = (ListPatternSyntax)operation.Syntax;
        var input = operation.InputType;
        if (input is not IArrayTypeSymbol && input.SpecialType != SpecialType.System_String
            && input.OriginalDefinition.ToDisplayString() is not ("System.Collections.Generic.List<T>"
                or "System.Collections.Generic.IReadOnlyList<T>"))
            throw Unsupported("WRK104", list);
        var sliceIndex = -1;
        for (var index = 0; index < operation.Patterns.Length; index++)
        {
            if (operation.Patterns[index] is not ISlicePatternOperation) continue;
            sliceIndex = index;
            break;
        }
        var fixedCount = operation.Patterns.Length - (sliceIndex >= 0 ? 1 : 0);
        var conditions = new List<string>
        {
            $"{value} != null",
            sliceIndex >= 0 ? $"{value}.length >= {fixedCount}" : $"{value}.length === {fixedCount}"
        };
        for (var index = 0; index < operation.Patterns.Length; index++)
        {
            var item = operation.Patterns[index];
            if (item is ISlicePatternOperation slice)
            {
                var after = operation.Patterns.Length - index - 1;
                if (slice.Pattern is not null)
                    conditions.Add(SubpatternTest(slice.Pattern, $"{value}.slice({index}, {value}.length - {after})"));
                continue;
            }
            var position = sliceIndex >= 0 && index > sliceIndex
                ? $"{value}[{value}.length - {operation.Patterns.Length - index}]"
                : $"{value}[{index}]";
            conditions.Add(SubpatternTest(item, position));
        }
        var condition = $"({string.Join(" && ", conditions)})";
        return Bind(operation.DeclaredSymbol, value, condition);
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

    private string IsPattern(IIsPatternOperation operation)
    {
        var subject = Expression(operation.Value);
        if (operation.Value is ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation)
            return PatternTest(operation.Pattern, subject);
        // Constant and null checks read the subject once, so no temporary is needed.
        if (operation.Pattern is IConstantPatternOperation or INegatedPatternOperation { Pattern: IConstantPatternOperation })
            return PatternTest(operation.Pattern, Operand(operation.Value));
        var source = operation.Syntax;
        var parameter = _names.Get($"is:{source.SyntaxTree.FilePath}:{source.SpanStart}", "subject");
        return $"(({parameter}) => {PatternTest(operation.Pattern, parameter)})({subject})";
    }

    private string SwitchExpression(ISwitchExpressionOperation operation)
    {
        var value = (SwitchExpressionSyntax)operation.Syntax;
        var parameter = _names.Get($"switch:{value.SyntaxTree.FilePath}:{value.SpanStart}", "subject");
        var variables = new List<string>();
        DeclarePatternVariables(
            value.Arms.SelectMany(arm => arm.Pattern.DescendantNodesAndSelf(node => node is not SwitchExpressionSyntax))
                .OfType<SingleVariableDesignationSyntax>(),
            variables.Add);
        var isAsync = operation.Arms.Any(ContainsAwait);
        var body = new List<string>();
        if (variables.Count != 0) body.Add($"let {string.Join(", ", variables)};");
        foreach (var arm in operation.Arms)
        {
            var condition = PatternTest(arm.Pattern, parameter);
            if (arm.Guard is not null) condition = $"{condition} && ({Expression(arm.Guard)})";
            var result = arm.Value is IThrowOperation thrown
                ? $"throw {Expression(thrown.Exception!)};"
                : $"return {Expression(arm.Value)};";
            body.Add(condition == "true" ? result : $"if ({condition}) {result}");
        }
        body.Add("throw new Error(\"The switch expression does not handle all possible values.\");");
        var function = $"({(isAsync ? "async " : "")}({parameter}) => {{ {string.Join(" ", body)} }})({Expression(operation.Value)})";
        return isAsync ? $"(await {function})" : function;
    }
}
