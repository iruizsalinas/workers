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
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SimpleAssignmentExpression) => $"{Expression(value.Left)} = {Expression(value.Right)}",
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.AddAssignmentExpression) => CompoundMutation(value, "+"),
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SubtractAssignmentExpression) => CompoundMutation(value, "-"),
        IsPatternExpressionSyntax value => IsPattern(value),
        MemberAccessExpressionSyntax value => Member(value),
        InvocationExpressionSyntax value => Invocation(value),
        ElementAccessExpressionSyntax value => ElementAccess(value),
        AnonymousObjectCreationExpressionSyntax value => "{ " + string.Join(", ", value.Initializers.Select(AnonymousMember)) + " }",
        InterpolatedStringExpressionSyntax value => "`" + string.Concat(value.Contents.Select(InterpolatedPart)) + "`",
        CollectionExpressionSyntax value => Collection(value),
        ObjectCreationExpressionSyntax value => ObjectCreation(value),
        ImplicitObjectCreationExpressionSyntax value => ObjectCreation(value),
        ParenthesizedLambdaExpressionSyntax value when value.ExpressionBody is not null =>
            $"{AsyncPrefix(value.AsyncKeyword)}({string.Join(", ", value.ParameterList.Parameters.Select(ParameterName))}) => {Expression(value.ExpressionBody)}",
        ParenthesizedLambdaExpressionSyntax value when value.Block is not null => Lambda(value),
        SimpleLambdaExpressionSyntax value when value.ExpressionBody is not null =>
            $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {Expression(value.ExpressionBody)}",
        SimpleLambdaExpressionSyntax value when value.Block is not null => Lambda(value),
        _ => throw Unsupported("WRK101", expression)
    };

    private string DefaultValue(ExpressionSyntax expression) =>
        _model.GetTypeInfo(expression).ConvertedType?.ToDisplayString() == "System.Threading.CancellationToken"
            ? "null"
            : throw Unsupported("WRK108", expression);

    private string UnaryNumeric(PrefixUnaryExpressionSyntax value, string operation)
    {
        var unary = _model.GetOperation(value) as IUnaryOperation;
        if (unary?.Type?.ToDisplayString() == "System.TimeSpan" && operation == "-")
            return TimeSpanNegate(Expression(value.Operand));
        if (unary?.OperatorMethod is not null)
            throw UnsupportedSymbol(unary.OperatorMethod, value);
        var type = unary?.Type?.SpecialType ?? SpecialType.None;
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
        if (operand is not IdentifierNameSyntax)
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
        if (value.Left is not IdentifierNameSyntax)
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
            return $"({target} = ({target} ?? \"\") + ({Expression(value.Right)} ?? \"\"))";
        return $"({target} = {NumericResult($"{target} {operation} {Expression(value.Right)}", type, value)})";
    }

    private static string NumericResult(string expression, SpecialType type, SyntaxNode source) => type switch
    {
        SpecialType.System_Int32 => $"({expression}) | 0",
        SpecialType.System_UInt32 => $"({expression}) >>> 0",
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
        foreach (var statement in block.Statements)
            EmitStatement(statement, 1);
        var statements = _output.ToString(start, _output.Length - start).TrimEnd();
        _output.Length = start;
        return statements.Length == 0 ? "{}" : $"{{\n{statements}\n}}";
    }

    private string Collection(CollectionExpressionSyntax value)
    {
        var items = "[" + string.Join(", ", value.Elements.Select(Element)) + "]";
        return _model.GetTypeInfo(value).ConvertedType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
            ? $"Uint8Array.from({items})"
            : items;
    }

}
