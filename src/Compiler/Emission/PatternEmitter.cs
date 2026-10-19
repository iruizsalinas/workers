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
    private HashSet<ISymbol> _declaredPatternVariables = new(SymbolEqualityComparer.Default);

    private string PatternTest(IPatternOperation operation, string value) => operation switch
    {
        IDiscardPatternOperation => "true",
        IConstantPatternOperation constant => ConstantPatternTest(constant, value),
        IRelationalPatternOperation relational => RelationalPatternTest(relational, value),
        IBinaryPatternOperation binary => $"({PatternTest(binary.LeftPattern, value)} " +
            $"{(binary.OperatorKind == BinaryOperatorKind.And ? "&&" : "||")} {PatternTest(binary.RightPattern, value)})",
        INegatedPatternOperation { Pattern: IConstantPatternOperation { Value.ConstantValue: { HasValue: true, Value: null } } } => $"{value} != null",
        INegatedPatternOperation negated => $"!({PatternTest(negated.Pattern, value)})",
        IDeclarationPatternOperation declaration => DeclarationPatternTest(declaration, value),
        ITypePatternOperation type => TypePatternTest(type.MatchedType, value, type.InputType, type.Syntax),
        IRecursivePatternOperation recursive => RecursivePatternTest(recursive, value),
        IListPatternOperation list => ListPatternTest(list, value),
        _ => throw Unsupported("WRK104", operation.Syntax)
    };

    private string DeclarationPatternTest(IDeclarationPatternOperation operation, string value)
    {
        var condition = operation.MatchesNull ? "true"
            : TypePatternTest(operation.MatchedType!, value, operation.InputType, operation.Syntax);
        var assigned = operation.DeclaredSymbol is ILocalSymbol local
            && (ResolveType(local.Type).SpecialType == SpecialType.System_Object
                || _compilation.ClassifyConversion(ResolveType(operation.InputType), ResolveType(local.Type)).IsBoxing)
            ? ObjectConversion(operation.InputType, value, operation.Syntax) : value;
        return Bind(operation.DeclaredSymbol, assigned, condition);
    }

    private string RelationalPatternTest(IRelationalPatternOperation operation, string value)
    {
        var constant = operation.Value.Type?.SpecialType;
        var numeric = constant is >= SpecialType.System_SByte and <= SpecialType.System_Decimal;
        // An object input also implies a type test. Integral and floating values share one
        // JavaScript number type, so only a fractional value can be told apart from an integral one.
        var guard = ResolveType(operation.InputType).SpecialType != SpecialType.System_Object || !numeric ? $"{value} != null"
            : constant is >= SpecialType.System_SByte and <= SpecialType.System_UInt64 ? $"Number.isInteger({value})"
            : $"typeof {value} === \"number\"";
        return $"({guard} && {value} {BinaryOperator(operation.OperatorKind)} {Expression(operation.Value)})";
    }

    private string ConstantPatternTest(IConstantPatternOperation constant, string value)
    {
        if (constant.Value.ConstantValue is { HasValue: true, Value: null }) return $"{value} == null";
        if (constant.InputType is { } input && ResolveType(input).SpecialType == SpecialType.System_Object && IsTextEnum(constant.Value.Type))
        {
            var type = ResolveType(constant.Value.Type!);
            var identity = System.Text.Json.JsonSerializer.Serialize(RuntimeTypeName(type));
            return $"({value} != null && {value}[{BoxedEnumTypeSlot}] === {identity} && Number({value}) === {Expression(constant.Value)})";
        }
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
        var assigned = symbol is ILocalSymbol local ? CopyValue(local.Type, value) : value;
        return $"({condition} && (({UserIdentifier(symbol, symbol.Name)} = {assigned}), true))";
    }

    private string TypePatternTest(ITypeSymbol type, string value, ITypeSymbol? input, SyntaxNode source)
    {
        type = ResolveType(type);
        var unwrappedInput = input is null ? null : UnwrapNullable(ResolveType(input));
        if (unwrappedInput is not null && SymbolEqualityComparer.Default.Equals(unwrappedInput, type))
            return $"{value} != null";
        if (type.SpecialType == SpecialType.System_String) return $"typeof {value} === \"string\"";
        if (type.SpecialType == SpecialType.System_Boolean) return $"typeof {value} === \"boolean\"";
        if (type is INamedTypeSymbol contract && IsSourceInterface(contract))
            return $"({value} != null && {value}[{InterfaceMarker(contract)}] === true)";
        if (type.SpecialType == SpecialType.System_Object) return $"{value} != null";
        if (type.ToDisplayString() == "System.Exception") return $"{value} instanceof Error";
        if (type is INamedTypeSymbol builtIn && IsException(builtIn) && builtIn.DeclaringSyntaxReferences.Length == 0
            && builtIn.ToDisplayString() != "Workers.HttpError")
            return $"{value} instanceof ({BuiltInExceptionType(builtIn)})";
        if (type.ToDisplayString() == "Workers.HttpError")
            return $"{value} instanceof {_helpers.Require(JavaScriptHelper.HttpError)}";
        if (type is INamedTypeSymbol named && IsUserInstanceType(named) && (IsException(named) || RequiresUserClass(named)))
            return $"{value} instanceof {QueueUserType(named, source)}";
        if (type is INamedTypeSymbol { IsRecord: true } plain && IsUserInstanceType(plain))
            return $"({value} != null && {value}[{PlainRecordIdentitySlot}] === {PlainRecordIdentity(plain)})";
        throw new NotSupportedException(
            $"WRK104: Testing for type '{type.ToDisplayString()}' is not supported at runtime; only string, bool and Worker class types can be tested.");
    }

    private string IsType(IIsTypeOperation operation)
    {
        var subject = Expression(operation.ValueOperand);
        if (operation.ValueOperand is ILocalReferenceOperation or IParameterReferenceOperation or IInstanceReferenceOperation)
            return TypePatternTest(operation.TypeOperand, subject, operation.ValueOperand.Type, operation.Syntax);
        var temporary = _names.Get($"type-test:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "subject");
        return $"(({temporary}) => {TypePatternTest(operation.TypeOperand, temporary, operation.ValueOperand.Type, operation.Syntax)})({subject})";
    }

    private string RecursivePatternTest(IRecursivePatternOperation operation, string value)
    {
        var recursive = operation.Syntax;
        var conditions = new List<string>();
        var type = operation.MatchedType;
        if (recursive is RecursivePatternSyntax { Type: not null })
        {
            conditions.Add(TypePatternTest(type, value, operation.InputType, recursive));
        }
        else
            conditions.Add($"{value} != null");
        if (operation.DeconstructionSubpatterns.Length != 0)
        {
            var components = type is INamedTypeSymbol { IsTupleType: true } ? value
                : operation.DeconstructSymbol is IMethodSymbol method ? DeconstructComponents(method, type, value, recursive)
                : throw Unsupported("WRK104", recursive);
            var extracted = _names.Get($"pattern-deconstruction:{recursive.SyntaxTree.FilePath}:{recursive.SpanStart}", "components");
            var tests = operation.DeconstructionSubpatterns.Select((pattern, index) => SubpatternTest(pattern, $"{extracted}[{index}]"));
            conditions.Add($"(({extracted}) => {string.Join(" && ", tests)})({components})");
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
        if (IsExtensionBlockProperty(operation, out var extensionProperty))
            return ExtensionAccessorCall(extensionProperty.Property, true, operation.Syntax, CopyValue(instance.Type, value));
        if (symbol.ContainingType is { } owner && IsUserInstanceType(owner))
            return UserMemberAccess(value, symbol);
        if (symbol.Name == "Count" && symbol.ContainingType.OriginalDefinition.ToDisplayString() is
            "System.Collections.Generic.IReadOnlyCollection<T>" or "System.Collections.Generic.ICollection<T>")
            return CollectionCount(symbol.ContainingType, instance.Type, value);
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
        var input = ResolveType(operation.InputType);
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
        // A nested getter can mutate a list; trailing indices still use its initial length.
        var length = _names.Get($"list-pattern-length:{list.SyntaxTree.FilePath}:{list.SpanStart}", "length");
        var conditions = new List<string>
        {
            sliceIndex >= 0 ? $"{length} >= {fixedCount}" : $"{length} === {fixedCount}"
        };
        for (var index = 0; index < operation.Patterns.Length; index++)
        {
            var item = operation.Patterns[index];
            if (item is ISlicePatternOperation slice)
            {
                var after = operation.Patterns.Length - index - 1;
                if (slice.Pattern is not null)
                {
                    var sliced = $"{value}.slice({index}, {length} - {after})";
                    if (input is IArrayTypeSymbol array) sliced = CopySequenceValues(array.ElementType, sliced);
                    conditions.Add(SubpatternTest(slice.Pattern, sliced));
                }
                continue;
            }
            var position = sliceIndex >= 0 && index > sliceIndex
                ? $"{value}[{length} - {operation.Patterns.Length - index}]"
                : $"{value}[{index}]";
            conditions.Add(SubpatternTest(item, position));
        }
        var condition = $"({value} != null && (({length}) => {string.Join(" && ", conditions)})({value}.length))";
        return Bind(operation.DeclaredSymbol, value, condition);
    }

    // Pattern variables declared by a statement's "is" patterns and pattern case labels, excluding
    // those owned by nested statements, catch clauses, lambdas and switch expression arms.
    // Catch clauses and switch expression arms also use this traversal for their local variables.
    private IEnumerable<SingleVariableDesignationSyntax> StatementPatternDesignations(SyntaxNode statement) =>
        statement
            .DescendantNodes(node => node == statement
                || node is not (StatementSyntax or CatchClauseSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
                    or SwitchExpressionArmSyntax))
            .Where(node => node is PatternSyntax)
            .SelectMany(pattern => pattern.DescendantNodesAndSelf(node => node is not SwitchExpressionArmSyntax))
            .OfType<SingleVariableDesignationSyntax>()
            .Distinct();

    // Distinct pattern symbols need distinct bindings, even when arms or sections
    // reuse a name: a failed guard can retain a closure over its pattern variable.
    private void DeclarePatternVariables(IEnumerable<SingleVariableDesignationSyntax> designations, Action<string> declare, HashSet<string> names)
    {
        foreach (var designation in designations)
        {
            var symbol = _model.GetDeclaredSymbol(designation);
            if (symbol is null || !_declaredPatternVariables.Add(symbol)) continue;
            declare(LocalDeclarationName(symbol, designation, names));
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
        var isAsync = operation.Arms.Any(ContainsAwait);
        string? state = null;
        if (isAsync)
        {
            if (!_expressionTemporaryScopes.TryPeek(out var temporaries)) throw Unsupported("WRK108", value);
            state = _names.Get($"switch-state:{value.SyntaxTree.FilePath}:{value.SpanStart}", "switchState");
            temporaries.Add(state);
        }
        var body = new List<string>();
        foreach (var arm in operation.Arms)
        {
            var variables = new List<string>();
            DeclareScopeVariables(arm.Syntax, variables.Add);
            var declarations = variables.Count == 0 ? "" : $"let {string.Join(", ", variables)}; ";
            var condition = PatternTest(arm.Pattern, parameter);
            if (arm.Guard is not null) condition = $"{condition} && ({Expression(arm.Guard)})";
            var result = arm.Value is IThrowOperation thrown
                ? $"throw {Expression(thrown.Exception!)};"
                : isAsync ? $"{state}.value = ({Expression(arm.Value)}); {state}.done = true; return;"
                : $"return {Expression(arm.Value)};";
            var selected = condition == "true" ? result : isAsync ? $"if ({condition}) {{ {result} }}" : $"if ({condition}) {result}";
            body.Add($"{{ {declarations}{selected} }}");
        }
        body.Add("throw new Error(\"The switch expression does not handle all possible values.\");");
        if (!isAsync)
            return $"(({parameter}) => {{ {string.Join(" ", body)} }})({ValueExpression(operation.Value)})";
        // The wrapper stores its result rather than returning it, so a Task-valued arm keeps
        // its identity. Inspect completion before awaiting to preserve synchronous branches.
        var resultHelper = _helpers.Require(JavaScriptHelper.AsyncExpressionResult);
        var function = $"(async ({parameter}, {state}) => {{ try {{ {string.Join(" ", body)} }} " +
            $"catch ($workers$error) {{ {state}.error = $workers$error; {state}.failed = true; {state}.done = true; }} }})" +
            $"({ValueExpression(operation.Value)}, {state})";
        return $"(({state} = {{}}), {state}.pending = {function}, {state}.done ? {resultHelper}({state}) : " +
            $"(await {state}.pending, {resultHelper}({state})))";
    }
}
