using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Expression(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax value => Literal(value),
        IdentifierNameSyntax value => Identifier(value),
        ThisExpressionSyntax => "this",
        ParenthesizedExpressionSyntax value => $"({Expression(value.Expression)})",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.LogicalNotExpression) =>
            $"!({Expression(value.Operand)})",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.UnaryMinusExpression) =>
            $"-{Expression(value.Operand)}",
        PrefixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.UnaryPlusExpression) =>
            $"+{Expression(value.Operand)}",
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.PostIncrementExpression) =>
            $"{Expression(value.Operand)}++",
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.PostDecrementExpression) =>
            $"{Expression(value.Operand)}--",
        PostfixUnaryExpressionSyntax value when value.IsKind(SyntaxKind.SuppressNullableWarningExpression) =>
            Expression(value.Operand),
        AwaitExpressionSyntax value => $"await {Expression(value.Expression)}",
        BinaryExpressionSyntax value => Binary(value),
        ConditionalExpressionSyntax value => $"{Expression(value.Condition)} ? {Expression(value.WhenTrue)} : {Expression(value.WhenFalse)}",
        ConditionalAccessExpressionSyntax value => ConditionalAccess(value),
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SimpleAssignmentExpression) => $"{Expression(value.Left)} = {Expression(value.Right)}",
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.AddAssignmentExpression) => $"{Expression(value.Left)} += {Expression(value.Right)}",
        AssignmentExpressionSyntax value when value.IsKind(SyntaxKind.SubtractAssignmentExpression) => $"{Expression(value.Left)} -= {Expression(value.Right)}",
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

    private string Lambda(ParenthesizedLambdaExpressionSyntax value)
    {
        var parameters = string.Join(", ", value.ParameterList.Parameters.Select(ParameterName));
        var statements = string.Join(" ", value.Block!.Statements.Select(LambdaStatement));
        return $"{AsyncPrefix(value.AsyncKeyword)}({parameters}) => {{ {statements} }}";
    }

    private string Lambda(SimpleLambdaExpressionSyntax value)
    {
        var statements = string.Join(" ", value.Block!.Statements.Select(LambdaStatement));
        return $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {{ {statements} }}";
    }

    private static string AsyncPrefix(SyntaxToken token) =>
        token.IsKind(SyntaxKind.AsyncKeyword) ? "async " : "";

    private string LambdaStatement(StatementSyntax statement) => statement switch
    {
        ExpressionStatementSyntax expression => Expression(expression.Expression) + ";",
        ReturnStatementSyntax { Expression: { } expression } => "return " + Expression(expression) + ";",
        ReturnStatementSyntax => "return;",
        _ => throw Unsupported("WRK100", statement)
    };

    private string Collection(CollectionExpressionSyntax value)
    {
        var items = "[" + string.Join(", ", value.Elements.Select(Element)) + "]";
        return _model.GetTypeInfo(value).ConvertedType is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
            ? $"Uint8Array.from({items})"
            : items;
    }

}
