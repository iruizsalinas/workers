using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Binary(BinaryExpressionSyntax expression)
    {
        var operation = _model.GetOperation(expression) as IBinaryOperation;
        var type = operation?.Type?.SpecialType ?? SpecialType.None;
        var left = Expression(expression.Left);
        var right = Expression(expression.Right);
        var integral32 = type is SpecialType.System_Int32 or SpecialType.System_UInt32;

        if (operation?.IsChecked == true && integral32
            && expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression)
            throw Unsupported("WRK108", expression);

        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64
            && expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression)
            throw Unsupported("WRK108", expression);

        if (expression.IsKind(SyntaxKind.DivideExpression) && integral32)
        {
            var helper = _helpers.Require(JavaScriptHelper.IntegerDivide);
            return $"{helper}({left}, {right}, {(type == SpecialType.System_UInt32 ? "true" : "false")})";
        }

        if (integral32)
        {
            if (expression.IsKind(SyntaxKind.MultiplyExpression))
                return type == SpecialType.System_UInt32 ? $"Math.imul({left}, {right}) >>> 0" : $"Math.imul({left}, {right})";
            if (expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression)
            {
                var native = $"({left} {BinaryOperator(expression.Kind())} {right})";
                return type == SpecialType.System_UInt32 ? $"{native} >>> 0" : $"{native} | 0";
            }
        }

        if (expression.IsKind(SyntaxKind.AddExpression) && type == SpecialType.System_String)
        {
            if (operation?.LeftOperand.Type?.SpecialType != SpecialType.System_String
                || operation.RightOperand.Type?.SpecialType != SpecialType.System_String)
                throw Unsupported("WRK108", expression);
            return $"({left} ?? \"\") + ({right} ?? \"\")";
        }

        if (type == SpecialType.System_Single
            && expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression)
            return $"Math.fround({left} {BinaryOperator(expression.Kind())} {right})";

        return $"{left} {BinaryOperator(expression.Kind())} {right}";
    }

    private static string BinaryOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => "===",
        SyntaxKind.NotEqualsExpression => "!==",
        SyntaxKind.CoalesceExpression => "??",
        SyntaxKind.AddExpression => "+",
        SyntaxKind.SubtractExpression => "-",
        SyntaxKind.MultiplyExpression => "*",
        SyntaxKind.DivideExpression => "/",
        SyntaxKind.LessThanExpression => "<",
        SyntaxKind.LessThanOrEqualExpression => "<=",
        SyntaxKind.GreaterThanExpression => ">",
        SyntaxKind.GreaterThanOrEqualExpression => ">=",
        _ => throw new NotSupportedException($"WRK103: Binary operator '{kind}' is not supported.")
    };
    private string ParameterName(ParameterSyntax parameter) =>
        UserIdentifier(_model.GetDeclaredSymbol(parameter)!, parameter.Identifier);
    private string UserIdentifier(ISymbol symbol, SyntaxToken token) => UserIdentifier(symbol, token.ValueText);
    private string UserIdentifier(ISymbol symbol, string value)
    {
        if (IsLegalJavascriptIdentifier(value) && !JavascriptReservedWords.Contains(value))
            return value;
        if (_userIdentifiers.TryGetValue(symbol, out var existing))
            return existing;
        var allocated = _names.Get("user:" + _userIdentifiers.Count, "user$" + value);
        _userIdentifiers.Add(symbol, allocated);
        return allocated;
    }
    private static bool IsLegalJavascriptIdentifier(string value) => value.Length != 0
        && (value[0] is '_' or '$' || value[0] is >= 'A' and <= 'Z' || value[0] is >= 'a' and <= 'z')
        && value.Skip(1).All(character => character is '_' or '$' || char.IsAsciiLetterOrDigit(character));
    private static readonly HashSet<string> JavascriptReservedWords = new(StringComparer.Ordinal)
    {
        "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do",
        "else", "enum", "export", "extends", "false", "finally", "for", "function", "if", "implements", "import",
        "in", "instanceof", "interface", "let", "new", "null", "package", "private", "protected", "public", "return",
        "static", "super", "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with", "yield"
    };
    private static string LowerFirst(string value) => value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
    private string IsPattern(IsPatternExpressionSyntax value) => value.Pattern switch
    {
        ConstantPatternSyntax constant when constant.Expression.IsKind(SyntaxKind.NullLiteralExpression) => $"{Expression(value.Expression)} === null",
        UnaryPatternSyntax unary
            when unary.IsKind(SyntaxKind.NotPattern)
                && unary.Pattern is ConstantPatternSyntax constant
                && constant.Expression.IsKind(SyntaxKind.NullLiteralExpression) =>
            $"{Expression(value.Expression)} !== null",
        _ => throw Unsupported("WRK104", value)
    };
    private static NotSupportedException Unsupported(string code, SyntaxNode node) => new($"{code}: '{node.Kind()}' is not supported yet: {node}");
    private static NotSupportedException UnsupportedSymbol(IMethodSymbol? symbol, SyntaxNode node) =>
        new($"WRK105: '{symbol?.ToDisplayString() ?? node.ToString()}' is outside the supported Workers C# profile.");
}

