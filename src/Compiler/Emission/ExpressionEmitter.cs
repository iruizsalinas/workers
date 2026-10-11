using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    // Roslyn's operation tree is the typed IR. Syntax remains available for diagnostics and
    // platform adapters, but never supplies a fallback for an unrecognized operation.
    private string Expression(ExpressionSyntax syntax)
    {
        // Parentheses and null-forgiving operators have no independent operation in C#.
        if (syntax is ParenthesizedExpressionSyntax parenthesized)
            return $"({Expression(parenthesized.Expression)})";
        if (syntax is CheckedExpressionSyntax checkedExpression)
            return Expression(checkedExpression.Expression);
        if (syntax is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            return Expression(suppression.Operand);
        var operation = _model.GetOperation(syntax) ?? throw Unsupported("WRK101", syntax);
        while (operation.Parent is IConversionOperation { IsImplicit: true } conversion
            && conversion.Syntax.Span == syntax.Span)
            operation = conversion;
        return Expression(operation);
    }

    private string Expression(IOperation operation)
    {
        try
        {
            return operation switch
            {
                IConversionOperation value => Conversion(value),
                IParenthesizedOperation value => $"({Expression(value.Operand)})",
                ILiteralOperation value => LiteralConstant(value.ConstantValue.Value, value.Syntax),
                INameOfOperation value => LiteralConstant(value.ConstantValue.Value, value.Syntax),
                IDefaultValueOperation value => DefaultFieldValue(value.Type!, value.Syntax),
                ILocalReferenceOperation value => UserIdentifier(value.Local, value.Local.Name),
                IParameterReferenceOperation value => UserIdentifier(value.Parameter, value.Parameter.Name),
                IInstanceReferenceOperation => "this",
                IUnaryOperation value => Unary(value),
                IIncrementOrDecrementOperation value => NumericMutation(value),
                IAwaitOperation value => $"await {Operand(value.Operation)}",
                IBinaryOperation value => Binary(value),
                ICoalesceOperation value => $"(({Expression(value.Value)}) ?? ({Expression(value.WhenNull)}))",
                IConditionalOperation value => $"{Expression(value.Condition)} ? {Expression(value.WhenTrue)} : {Expression(value.WhenFalse!)}",
                IConditionalAccessOperation value => ConditionalAccess(value),
                IConditionalAccessInstanceOperation value => ConditionalReceiver(value),
                ISimpleAssignmentOperation value => SimpleAssignment(value),
                ICompoundAssignmentOperation value => CompoundAssignment(value),
                IWithOperation value => WithExpression(value),
                ISwitchExpressionOperation value => SwitchExpression(value),
                IArrayCreationOperation value => ArrayCreation(value),
                IThrowOperation value => $"(() => {{ throw {Expression(value.Exception!)}; }})()",
                IIsPatternOperation value => IsPattern(value),
                IFieldReferenceOperation value => Member(value),
                IPropertyReferenceOperation value => Member(value),
                IInvocationOperation value => Invocation(value),
                IArrayElementReferenceOperation value => HelperInvocation(JavaScriptHelper.SequenceIndex,
                    [Expression(value.ArrayReference), Expression(value.Indices.Single())]),
                IAnonymousObjectCreationOperation value => AnonymousObject(value),
                IInterpolatedStringOperation value => "`" + string.Concat(value.Parts.Select(InterpolatedPart)) + "`",
                ICollectionExpressionOperation value => Collection(value),
                IObjectCreationOperation value => ObjectCreation(value),
                IDelegateCreationOperation value => Expression(value.Target),
                IMethodReferenceOperation value => MethodGroup((ExpressionSyntax)value.Syntax, value.Method,
                    value.Syntax is MemberAccessExpressionSyntax member ? member.Expression : null),
                IAnonymousFunctionOperation value => AnonymousFunction(value),
                _ => throw Unsupported("WRK101", operation.Syntax)
            };
        }
        catch (NotSupportedException exception)
        {
            throw WorkerDiagnostics.WithLocation(exception, operation.Syntax);
        }
    }

    private string Operand(IOperation operation)
    {
        var value = Expression(operation);
        return operation is IBinaryOperation or IConditionalOperation or IAwaitOperation
            or ISimpleAssignmentOperation or ICompoundAssignmentOperation ? $"({value})" : value;
    }

    private string AnonymousObject(IAnonymousObjectCreationOperation operation) =>
        "{ " + string.Join(", ", operation.Initializers.Select(initializer =>
        {
            if (initializer is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment)
                throw Unsupported("WRK101", initializer.Syntax);
            var name = property.Property.Name;
            if (initializer.Syntax is AnonymousObjectMemberDeclaratorSyntax { NameEquals: null, Expression: MemberAccessExpressionSyntax })
                name = LowerFirst(name);
            return $"{JavaScriptObjectKey(name)}: {Expression(assignment.Value)}";
        })) + " }";

    private string AnonymousFunction(IAnonymousFunctionOperation operation) => operation.Syntax switch
    {
        ParenthesizedLambdaExpressionSyntax { ExpressionBody: not null } value =>
            $"{AsyncPrefix(value.AsyncKeyword)}({string.Join(", ", value.ParameterList.Parameters.Select(ParameterName))}) => {LambdaExpressionBody(value.ExpressionBody)}",
        ParenthesizedLambdaExpressionSyntax value => Lambda(value),
        SimpleLambdaExpressionSyntax { ExpressionBody: not null } value =>
            $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {LambdaExpressionBody(value.ExpressionBody)}",
        SimpleLambdaExpressionSyntax value => Lambda(value),
        _ => throw Unsupported("WRK101", operation.Syntax)
    };

    private string LambdaExpressionBody(ExpressionSyntax expression)
    {
        var body = Expression(expression);
        // Object literals are parsed as statement blocks in arrow-function bodies.
        return body.StartsWith('{') ? $"({body})" : body;
    }

    private string SimpleAssignment(ISimpleAssignmentOperation operation)
    {
        var right = Expression(operation.Value);
        if (operation.Target is IDiscardOperation) return $"({right})";
        if (operation.Target is IArrayElementReferenceOperation array)
        {
            _helpers.Require(JavaScriptHelper.SequenceIndex);
            return $"{_helpers.Name("sequenceSet")}({Expression(array.ArrayReference)}, {Expression(array.Indices.Single())}, {right})";
        }
        if (operation.Target is IPropertyReferenceOperation { Property.IsIndexer: true } property)
        {
            if (IsSequenceType(property.Instance?.Type))
            {
                _helpers.Require(JavaScriptHelper.SequenceIndex);
                return $"{_helpers.Name("sequenceSet")}({Expression(property.Instance!)}, {Expression(property.Arguments.Single().Value)}, {right})";
            }
            if (IsDictionary(property.Instance?.Type))
            {
                _helpers.Require(JavaScriptHelper.DictionaryIndex);
                return $"{_helpers.Name("dictionarySet")}({Expression(property.Instance!)}, {Expression(property.Arguments.Single().Value)}, {right})";
            }
        }
        return $"{Expression(operation.Target)} = {right}";
    }

    private string Unary(IUnaryOperation operation)
    {
        if (operation.OperatorMethod is not null) throw UnsupportedSymbol(operation.OperatorMethod, operation.Syntax);
        if (operation.Type?.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
            throw Unsupported("WRK108", operation.Syntax);
        var operand = Operand(operation.Operand);
        if (operation.OperatorKind == UnaryOperatorKind.Not && operation.Type?.SpecialType == SpecialType.System_Boolean)
            return $"!({operand})";
        if (operation.IsChecked || operation.IsLifted) throw Unsupported("WRK108", operation.Syntax);
        if (operation.ConstantValue is { HasValue: true, Value: int or uint or double } constant)
            return LiteralConstant(constant.Value, operation.Syntax);
        var token = operation.OperatorKind switch
        {
            UnaryOperatorKind.Minus => "-",
            UnaryOperatorKind.Plus => "+",
            UnaryOperatorKind.BitwiseNegation => "~",
            _ => throw Unsupported("WRK108", operation.Syntax)
        };
        return NumericResult($"{token}{operand}", operation.Type?.SpecialType ?? SpecialType.None, operation.Syntax);
    }

    private string NumericMutation(IIncrementOrDecrementOperation operation)
    {
        if (operation.OperatorMethod is not null || operation.IsChecked || operation.IsLifted
            || !IsSimpleMutationTarget(operation.Target))
            throw Unsupported("WRK108", operation.Syntax);
        var token = operation.Kind == OperationKind.Increment ? "+" : "-";
        var type = operation.Type?.SpecialType ?? SpecialType.None;
        var updated = NumericResult($"$workers$value {token} 1", type, operation.Syntax);
        return Mutate(operation.Target, target => operation.IsPostfix
            ? $"(($workers$value) => {{ {target} = {updated}; return $workers$value; }})({target})"
            : $"({target} = {NumericResult($"{target} {token} 1", type, operation.Syntax)})");
    }

    private string CompoundAssignment(ICompoundAssignmentOperation operation)
    {
        if (!IsSimpleMutationTarget(operation.Target)
            || operation.OperatorMethod is not null || !operation.InConversion.IsIdentity
            || !operation.OutConversion.IsIdentity || operation.IsChecked || operation.IsLifted)
            throw Unsupported("WRK108", operation.Syntax);
        return Mutate(operation.Target, target => CompoundAssignment(operation, target), ContainsAwait(operation.Value));
    }

    private string CompoundAssignment(ICompoundAssignmentOperation operation, string target)
    {
        var type = operation.Type?.SpecialType ?? SpecialType.None;
        var right = Operand(operation.Value);
        if (type == SpecialType.System_String && operation.OperatorKind == BinaryOperatorKind.Add)
            return $"({target} = ({target} ?? \"\") + {StringConcatOperand(operation.Value, right, operation.Syntax)})";
        var integral = type is SpecialType.System_Int32 or SpecialType.System_UInt32;
        var unsigned = type == SpecialType.System_UInt32 ? "true" : "false";
        var result = operation.OperatorKind switch
        {
            BinaryOperatorKind.Multiply when type == SpecialType.System_Int32 => $"Math.imul({target}, {right})",
            BinaryOperatorKind.Multiply when type == SpecialType.System_UInt32 => $"(Math.imul({target}, {right}) >>> 0)",
            BinaryOperatorKind.Divide when integral =>
                $"{_helpers.Require(JavaScriptHelper.IntegerDivide)}({target}, {right}, {unsigned})",
            BinaryOperatorKind.Remainder when integral =>
                $"{_helpers.Require(JavaScriptHelper.IntegerRemainder)}({target}, {right}, {unsigned})",
            BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder =>
                NumericResult($"{target} {BinaryOperator(operation.OperatorKind)} {right}", type, operation.Syntax),
            _ => throw Unsupported("WRK108", operation.Syntax)
        };
        return $"({target} = {result})";
    }

    // Capture a member receiver before a getter or RHS can change the original variable.
    private string Mutate(IOperation operation, Func<string, string> emit, bool isAsync = false)
    {
        var instance = operation switch
        {
            IFieldReferenceOperation field => field.Instance,
            IPropertyReferenceOperation property => property.Instance,
            _ => null
        };
        if (instance is null || instance is IInstanceReferenceOperation) return emit(Expression(operation));
        var symbol = operation is IFieldReferenceOperation fieldTarget ? (ISymbol)fieldTarget.Field
            : ((IPropertyReferenceOperation)operation).Property;
        var source = operation.Syntax;
        var receiver = _names.Get($"mutation:{source.SyntaxTree.FilePath}:{source.SpanStart}", "receiver");
        var body = emit(UserMemberAccess(receiver, symbol));
        return isAsync
            ? $"(await (async ({receiver}) => {body})({Expression(instance)}))"
            : $"(({receiver}) => {body})({Expression(instance)})";
    }

    private static bool ContainsAwait(IOperation operation) => operation is IAwaitOperation
        || operation is not IAnonymousFunctionOperation && operation.ChildOperations.Any(ContainsAwait);

    // A record "with" expression is a memberwise clone that keeps the prototype, so class-backed
    // records keep their methods, followed by the initializer assignments.
    private string WithExpression(IWithOperation operation)
    {
        var value = operation.Syntax;
        if (operation.Operand.Type is not INamedTypeSymbol { IsRecord: true } type || !IsUserInstanceType(type))
            throw Unsupported("WRK101", value);
        var assignments = operation.Initializer.Initializers.Select(expression => expression switch
        {
            ISimpleAssignmentOperation { Target: IMemberReferenceOperation member } assignment =>
                $"{JavaScriptObjectKey(UserMemberName(member.Member))}: {Expression(assignment.Value)}",
            _ => throw Unsupported("WRK106", expression.Syntax)
        });
        if (type.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared && IsUserRecordCopyConstructor(constructor)))
        {
            var record = QueueUserType(type, value);
            return $"((source) => Object.assign({record}.$copy(source), {{ {string.Join(", ", assignments)} }}))"
                + $"({Expression(operation.Operand)})";
        }
        return $"((source) => Object.assign(Object.create(Object.getPrototypeOf(source)), source, {{ {string.Join(", ", assignments)} }}))"
            + $"({Expression(operation.Operand)})";
    }

    private static bool IsSimpleMutationTarget(IOperation target) => target switch
    {
        ILocalReferenceOperation or IParameterReferenceOperation => true,
        IFieldReferenceOperation { Field.HasConstantValue: false } field => field.Field.DeclaringSyntaxReferences.Length != 0,
        IPropertyReferenceOperation { Property: { IsIndexer: false, SetMethod: not null } } property =>
            property.Property.DeclaringSyntaxReferences.Length != 0,
        _ => false
    };

    private static string NumericResult(string expression, SpecialType type, SyntaxNode source) => type switch
    {
        SpecialType.System_Int32 => $"(({expression}) | 0)",
        SpecialType.System_UInt32 => $"(({expression}) >>> 0)",
        SpecialType.System_Single => $"Math.fround({expression})",
        SpecialType.System_Double => $"({expression})",
        _ => throw Unsupported("WRK108", source)
    };

    private string Lambda(ParenthesizedLambdaExpressionSyntax value)
    {
        var parameters = string.Join(", ", value.ParameterList.Parameters.Select(ParameterName));
        return $"{AsyncPrefix(value.AsyncKeyword)}({parameters}) => {LambdaBlock(value.Block!)}";
    }

    private string Lambda(SimpleLambdaExpressionSyntax value)
    {
        return $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {LambdaBlock(value.Block!)}";
    }

    private static string AsyncPrefix(SyntaxToken token) =>
        token.IsKind(SyntaxKind.AsyncKeyword) ? "async " : "";

    private string LambdaBlock(BlockSyntax block)
    {
        var start = _output.Length;
        EmitStatements(block.Statements, 1);
        var statements = _output.ToString(start, _output.Length - start).TrimEnd();
        _output.Length = start;
        return statements.Length == 0 ? "{}" : $"{{\n{statements}\n}}";
    }

    // Single-dimensional arrays: byte arrays are Uint8Array, other arrays are JavaScript arrays whose
    // elements start at the element type's default value.
    private string ArrayCreation(IArrayCreationOperation operation)
    {
        if (operation.Type is not IArrayTypeSymbol { Rank: 1 } array)
            throw Unsupported("WRK101", operation.Syntax);
        var bytes = array.ElementType.SpecialType == SpecialType.System_Byte;
        if (operation.Initializer is { } initializer)
        {
            var items = "[" + string.Join(", ", initializer.ElementValues.Select(Expression)) + "]";
            return bytes ? $"Uint8Array.from({items})" : items;
        }
        var length = Expression(operation.DimensionSizes.Single());
        if (bytes) return $"new Uint8Array({length})";
        var fallback = DefaultValueText(array.ElementType, operation.Syntax);
        return $"((length) => {{ if (!Number.isInteger(length) || length < 0) throw new RangeError(\"Arithmetic operation resulted in an overflow.\"); " +
               $"return Array.from({{ length }}, () => {fallback}); }})({length})";
    }

    private string Collection(ICollectionExpressionOperation operation)
    {
        var elementType = operation.Type is IArrayTypeSymbol array ? array.ElementType
            : (operation.Type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();
        var items = "[" + string.Join(", ", operation.Elements.Select(element => element is ISpreadOperation spread
            ? "..." + Spread(spread, elementType) : Expression(element))) + "]";
        return operation.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
            ? $"Uint8Array.from({items})"
            : items;
    }

    private string Spread(ISpreadOperation operation, ITypeSymbol? elementType)
    {
        var values = Expression(operation.Operand);
        if (operation.Operand.Type?.SpecialType == SpecialType.System_String)
            values = HelperInvocation(JavaScriptHelper.LinqValues, [values]);
        if (operation.ElementConversion.IsIdentity || operation.ElementConversion.IsReference
            || elementType?.SpecialType == SpecialType.System_Object) return values;
        if (elementType is null || operation.ElementType is null || operation.ElementConversion.IsUserDefined)
            throw Unsupported("WRK108", operation.Syntax);
        var converted = ConvertValue(operation.ElementType, elementType, "value", operation.Syntax);
        return $"Array.from({values}, value => {converted})";
    }

}
