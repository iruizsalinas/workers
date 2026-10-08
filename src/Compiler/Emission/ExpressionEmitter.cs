using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Expression(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax value => Literal(value),
        DefaultExpressionSyntax value => DefaultValue(value),
        IdentifierNameSyntax value => Identifier(value),
        ThisExpressionSyntax => "this",
        ParenthesizedExpressionSyntax value => $"({Expression(value.Expression)})",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.LogicalNotExpression) =>
            $"!({Expression(value.Operand)})",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.UnaryMinusExpression) =>
            UnaryNumeric(value, "-"),
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.UnaryPlusExpression) =>
            UnaryNumeric(value, "+"),
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.PreIncrementExpression) => NumericMutation(value, 1, postfix: false),
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.PreDecrementExpression) => NumericMutation(value, -1, postfix: false),
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.PostIncrementExpression) => NumericMutation(value, 1, postfix: true),
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.PostDecrementExpression) => NumericMutation(value, -1, postfix: true),
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.SuppressNullableWarningExpression) =>
            Expression(value.Operand),
        AwaitExpressionSyntax value => $"await {Expression(value.Expression)}",
        BinaryExpressionSyntax value => Binary(value),
        ConditionalExpressionSyntax value => $"{Expression(value.Condition)} ? {Expression(value.WhenTrue)} : {Expression(value.WhenFalse)}",
        ConditionalAccessExpressionSyntax value => ConditionalAccess(value),
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SimpleAssignmentExpression) => SimpleAssignment(value),
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.AddAssignmentExpression) => CompoundMutation(value, "+"),
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SubtractAssignmentExpression) => CompoundMutation(value, "-"),
        AssignmentExpressionSyntax value when value.Kind() is SyntaxKind.MultiplyAssignmentExpression
            or SyntaxKind.DivideAssignmentExpression or SyntaxKind.ModuloAssignmentExpression => CompoundArithmetic(value),
        WithExpressionSyntax value => WithExpression(value),
        SwitchExpressionSyntax value => SwitchExpression(value),
        CastExpressionSyntax value => Cast(value),
        ArrayCreationExpressionSyntax value => ArrayCreation(value, value.Initializer),
        ImplicitArrayCreationExpressionSyntax value => ArrayCreation(value, value.Initializer),
        ThrowExpressionSyntax value => $"(() => {{ throw {Expression(value.Expression)}; }})()",
        IsPatternExpressionSyntax value => IsPattern(value),
        MemberAccessExpressionSyntax value => Member(value),
        InvocationExpressionSyntax value when _model.GetConstantValue(value) is { HasValue: true, Value: string text } =>
            LiteralConstant(text, value),
        InvocationExpressionSyntax value => Invocation(value),
        ElementAccessExpressionSyntax value => ElementAccess(value),
        AnonymousObjectCreationExpressionSyntax value => "{ " + string.Join(", ", value.Initializers.Select(AnonymousMember)) + " }",
        InterpolatedStringExpressionSyntax value => "`" + string.Concat(value.Contents.Select(InterpolatedPart)) + "`",
        CollectionExpressionSyntax value => Collection(value),
        ObjectCreationExpressionSyntax value => ObjectCreation(value),
        ImplicitObjectCreationExpressionSyntax value => ObjectCreation(value),
        ParenthesizedLambdaExpressionSyntax value when value.ExpressionBody is not null =>
            $"{AsyncPrefix(value.AsyncKeyword)}({string.Join(", ", value.ParameterList.Parameters.Select(ParameterName))}) => {LambdaExpressionBody(value.ExpressionBody)}",
        ParenthesizedLambdaExpressionSyntax value when value.Block is not null => Lambda(value),
        SimpleLambdaExpressionSyntax value when value.ExpressionBody is not null =>
            $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {LambdaExpressionBody(value.ExpressionBody)}",
        SimpleLambdaExpressionSyntax value when value.Block is not null => Lambda(value),
        _ => throw Unsupported("WRK101", expression)
    };

    private string LambdaExpressionBody(ExpressionSyntax expression)
    {
        var body = Expression(expression);
        // Object literals are parsed as statement blocks in arrow-function bodies.
        return body.StartsWith('{') ? $"({body})" : body;
    }

    private string DefaultValue(ExpressionSyntax expression) =>
        _model.GetTypeInfo(expression).ConvertedType?.ToDisplayString() == "System.Threading.CancellationToken"
            ? "null"
            : throw Unsupported("WRK108", expression);

    private string SimpleAssignment(AssignmentExpressionSyntax value)
    {
        if (value.Left is MemberAccessExpressionSyntax member
            && _model.GetSymbolInfo(member).Symbol is IPropertySymbol property and
            {
                Name: "Length",
                ContainingType: { } containingType
            }
            && containingType.ToDisplayString() == "System.Text.StringBuilder")
        {
            _helpers.Require(JavaScriptHelper.StringBuilder);
            return $"{_helpers.Name("stringBuilderLength")}({Expression(member.Expression)}, {Expression(value.Right)})";
        }
        if (value.Left is ElementAccessExpressionSyntax element && IsSequenceType(_model.GetTypeInfo(element.Expression).Type))
        {
            var index = element.ArgumentList.Arguments.Single();
            _helpers.Require(JavaScriptHelper.SequenceIndex);
            return $"{_helpers.Name("sequenceSet")}({Expression(element.Expression)}, {Expression(index.Expression)}, {Expression(value.Right)})";
        }
        if (value.Left is ElementAccessExpressionSyntax dictionary && IsDictionary(_model.GetTypeInfo(dictionary.Expression).Type))
        {
            var key = dictionary.ArgumentList.Arguments.Single();
            _helpers.Require(JavaScriptHelper.DictionaryIndex);
            return $"{_helpers.Name("dictionarySet")}({Expression(dictionary.Expression)}, {Expression(key.Expression)}, {Expression(value.Right)})";
        }
        return $"{Expression(value.Left)} = {Expression(value.Right)}";
    }

    private string UnaryNumeric(PrefixUnaryExpressionSyntax value, string operation)
    {
        var unary = _model.GetOperation(value) as IUnaryOperation;
        if (unary?.Type?.ToDisplayString() == "System.TimeSpan" && operation == "-")
            return TimeSpanNegate(Expression(value.Operand));
        if (unary?.OperatorMethod is not null)
            throw UnsupportedSymbol(unary.OperatorMethod, value);
        var type = unary?.Type?.SpecialType ?? SpecialType.None;
        if (_model.GetConstantValue(value) is { HasValue: true, Value: int or uint or double } constant)
            return LiteralConstant(constant.Value, value);
        return NumericResult($"{operation}{Expression(value.Operand)}", type, value);
    }

    private string NumericMutation(ExpressionSyntax value, int delta, bool postfix)
    {
        var operand = value switch
        {
            PrefixUnaryExpressionSyntax prefix => prefix.Operand,
            PostfixUnaryExpressionSyntax postfixValue => postfixValue.Operand,
            _ => throw new InvalidOperationException()
        };
        if (!IsSimpleMutationTarget(operand))
            throw Unsupported("WRK108", value);
        var type = _model.GetTypeInfo(operand).Type?.SpecialType ?? SpecialType.None;
        var target = Expression(operand);
        var updated = NumericResult($"$workers$value {(delta > 0 ? "+" : "-")} 1", type, value);
        return postfix
            ? $"(($workers$value) => {{ {target} = {updated}; return $workers$value; }})({target})"
            : $"({target} = {NumericResult($"{target} {(delta > 0 ? "+" : "-")} 1", type, value)})";
    }

    private string CompoundMutation(AssignmentExpressionSyntax value, string operation)
    {
        if (!IsSimpleMutationTarget(value.Left))
            throw Unsupported("WRK108", value);
        var type = _model.GetTypeInfo(value).Type?.SpecialType ?? SpecialType.None;
        var target = Expression(value.Left);
        if (IsDateValue(_model.GetTypeInfo(value.Left).Type)
            && IsTimeSpan(_model.GetTypeInfo(value.Right).Type))
            return $"({target} = {DateTimeAddMilliseconds(target,
                operation == "+" ? Expression(value.Right) : $"-({Expression(value.Right)})")})";
        if (_model.GetTypeInfo(value).Type?.ToDisplayString() == "System.TimeSpan")
            return $"({target} = {TimeSpanArithmetic(target, Expression(value.Right), operation)})";
        if (type == SpecialType.System_String && operation == "+")
            return $"({target} = ({target} ?? \"\") + {StringConcatOperand(value.Right, Expression(value.Right), value)})";
        return $"({target} = {NumericResult($"{target} {operation} {Expression(value.Right)}", type, value)})";
    }

    private string CompoundArithmetic(AssignmentExpressionSyntax value)
    {
        if (!IsSimpleMutationTarget(value.Left) || _model.GetOperation(value) is not ICompoundAssignmentOperation operation
            || operation.OperatorMethod is not null || operation.InConversion is { IsIdentity: false } || operation.IsChecked)
            throw Unsupported("WRK108", value);
        var type = operation.Type?.SpecialType ?? SpecialType.None;
        var target = Expression(value.Left);
        var right = Expression(value.Right);
        var integral = type is SpecialType.System_Int32 or SpecialType.System_UInt32;
        var unsigned = type == SpecialType.System_UInt32 ? "true" : "false";
        var result = value.Kind() switch
        {
            SyntaxKind.MultiplyAssignmentExpression when type == SpecialType.System_Int32 => $"Math.imul({target}, {right})",
            SyntaxKind.MultiplyAssignmentExpression when type == SpecialType.System_UInt32 => $"(Math.imul({target}, {right}) >>> 0)",
            SyntaxKind.DivideAssignmentExpression when integral =>
                $"{_helpers.Require(JavaScriptHelper.IntegerDivide)}({target}, {right}, {unsigned})",
            SyntaxKind.ModuloAssignmentExpression when integral =>
                $"{_helpers.Require(JavaScriptHelper.IntegerRemainder)}({target}, {right}, {unsigned})",
            SyntaxKind.MultiplyAssignmentExpression => NumericResult($"{target} * {right}", type, value),
            SyntaxKind.DivideAssignmentExpression => NumericResult($"{target} / {right}", type, value),
            _ => NumericResult($"{target} % {right}", type, value)
        };
        return $"({target} = {result})";
    }

    // A record "with" expression is a memberwise clone that keeps the prototype, so class-backed
    // records keep their methods, followed by the initializer assignments.
    private string WithExpression(WithExpressionSyntax value)
    {
        if (_model.GetTypeInfo(value.Expression).Type is not INamedTypeSymbol { IsRecord: true } type || !IsUserInstanceType(type))
            throw Unsupported("WRK101", value);
        var assignments = value.Initializer.Expressions.Select(expression => expression switch
        {
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax } assignment =>
                $"{JavaScriptObjectKey(UserInitializerMemberName(assignment.Left))}: {Expression(assignment.Right)}",
            _ => throw Unsupported("WRK106", expression)
        });
        return $"((source) => Object.assign(Object.create(Object.getPrototypeOf(source)), source, {{ {string.Join(", ", assignments)} }}))" +
               $"({Expression(value.Expression)})";
    }

    // Mutations read and write their target, so the target must be safe to evaluate twice.
    private bool IsSimpleMutationTarget(ExpressionSyntax target) => target switch
    {
        IdentifierNameSyntax => true,
        MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax or ThisExpressionSyntax } access =>
            _model.GetSymbolInfo(access).Symbol is IFieldSymbol { HasConstantValue: false }
                or IPropertySymbol { IsIndexer: false, SetMethod: not null }
            && _model.GetSymbolInfo(access).Symbol!.DeclaringSyntaxReferences.Length != 0,
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
    private string ArrayCreation(ExpressionSyntax value, InitializerExpressionSyntax? initializer)
    {
        if (_model.GetTypeInfo(value).Type is not IArrayTypeSymbol { Rank: 1 } array)
            throw Unsupported("WRK101", value);
        var bytes = array.ElementType.SpecialType == SpecialType.System_Byte;
        if (initializer is not null)
        {
            var items = "[" + string.Join(", ", initializer.Expressions.Select(Expression)) + "]";
            return bytes ? $"Uint8Array.from({items})" : items;
        }
        if (value is not ArrayCreationExpressionSyntax { Type.RankSpecifiers: [{ Sizes: [var size] }] })
            throw Unsupported("WRK101", value);
        var length = Expression(size);
        if (bytes) return $"new Uint8Array({length})";
        var fallback = DefaultValueText(array.ElementType, value);
        return $"((length) => {{ if (!Number.isInteger(length) || length < 0) throw new RangeError(\"Arithmetic operation resulted in an overflow.\"); " +
               $"return Array.from({{ length }}, () => {fallback}); }})({length})";
    }

    private string Collection(CollectionExpressionSyntax value)
    {
        var items = "[" + string.Join(", ", value.Elements.Select(Element)) + "]";
        return _model.GetTypeInfo(value).ConvertedType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
            ? $"Uint8Array.from({items})"
            : items;
    }

}
