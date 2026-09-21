using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Binary(BinaryExpressionSyntax expression)
    {
        var operation = _model.GetOperation(expression) as IBinaryOperation;
        if (operation is { } timeOperation
            && (IsTimeSpan(timeOperation.LeftOperand.Type) || IsTimeSpan(timeOperation.RightOperand.Type)))
            return TimeSpanBinary(expression, timeOperation);
        if (operation is { } dateDifference
            && IsDateValue(dateDifference.LeftOperand.Type) && IsDateValue(dateDifference.RightOperand.Type)
            && expression.IsKind(SyntaxKind.SubtractExpression))
            return $"new Date({Expression(expression.Left)}).getTime() - new Date({Expression(expression.Right)}).getTime()";
        if (operation is { } dateOperation
            && IsDateValue(dateOperation.LeftOperand.Type) && IsDateValue(dateOperation.RightOperand.Type)
            && SymbolEqualityComparer.Default.Equals(dateOperation.LeftOperand.Type, dateOperation.RightOperand.Type)
            && expression.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
                or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression)
            return DateTimeOffsetComparison(expression);
        if (operation is { } guidOperation
            && guidOperation.LeftOperand.Type?.ToDisplayString() == "System.Guid"
            && guidOperation.RightOperand.Type?.ToDisplayString() == "System.Guid"
            && expression.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            return $"{Expression(expression.Left)} {BinaryOperator(expression.Kind())} {Expression(expression.Right)}";
        if (operation is { } cancellationOperation
            && cancellationOperation.LeftOperand.Type?.ToDisplayString() == "System.Threading.CancellationToken"
            && cancellationOperation.RightOperand.Type?.ToDisplayString() == "System.Threading.CancellationToken"
            && expression.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            return $"{Expression(expression.Left)} {BinaryOperator(expression.Kind())} {Expression(expression.Right)}";
        if (operation?.OperatorMethod is not null)
            throw UnsupportedSymbol(operation.OperatorMethod, expression);
        if (operation is { IsLifted: true }
            && IsSupportedNullableNumeric(operation.LeftOperand.Type)
            && IsSupportedNullableNumeric(operation.RightOperand.Type))
            return LiftedNumericBinary(expression, operation);
        var type = operation?.Type?.SpecialType ?? SpecialType.None;
        var numeric = type != SpecialType.System_String
            && expression.Kind() is (SyntaxKind.AddExpression or SyntaxKind.SubtractExpression
                or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression);
        var left = BinaryOperand(expression.Left, numeric);
        var right = BinaryOperand(expression.Right, numeric);
        var integral32 = type is SpecialType.System_Int32 or SpecialType.System_UInt32;

        if (expression.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            && (expression.Left.IsKind(SyntaxKind.NullLiteralExpression)
                || expression.Right.IsKind(SyntaxKind.NullLiteralExpression)))
            return $"{left} {(expression.IsKind(SyntaxKind.EqualsExpression) ? "==" : "!=")} {right}";

        if (expression.IsKind(SyntaxKind.CoalesceExpression))
            return $"(({left}) ?? ({right}))";

        if (IsNullableBoolean(operation?.Type)
            && expression.Kind() is SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression)
            return NullableBooleanBinary(expression, left, right);

        if (type == SpecialType.System_Boolean
            && expression.Kind() is SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression)
            return $"Boolean({left} {BinaryOperator(expression.Kind())} {right})";

        if (operation?.IsChecked == true && integral32
            && expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression)
            throw Unsupported("WRK108", expression);

        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64
            && expression.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression)
            throw Unsupported("WRK108", expression);

        if (expression.IsKind(SyntaxKind.DivideExpression) && integral32)
        {
            var helper = _helpers.Require(JavaScriptHelper.IntegerDivide);
            return $"{helper}({left}, {right}, {(type == SpecialType.System_UInt32 ? "true" : "false")})";
        }

        if (expression.IsKind(SyntaxKind.ModuloExpression) && integral32)
        {
            var helper = _helpers.Require(JavaScriptHelper.IntegerRemainder);
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

        if (expression.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression)
            return $"{ComparisonOperand(expression.Left, left)} {BinaryOperator(expression.Kind())} " +
                   ComparisonOperand(expression.Right, right);

        return $"{left} {BinaryOperator(expression.Kind())} {right}";
    }

    private string NullableBooleanBinary(BinaryExpressionSyntax expression, string left, string right)
    {
        var key = $"nullable-boolean:{expression.SyntaxTree.FilePath}:{expression.SpanStart}";
        var leftValue = _names.Get(key + ":left", "left");
        var rightValue = _names.Get(key + ":right", "right");
        var result = expression.Kind() switch
        {
            SyntaxKind.BitwiseAndExpression =>
                $"{leftValue} === false || {rightValue} === false ? false : {leftValue} == null || {rightValue} == null ? null : true",
            SyntaxKind.BitwiseOrExpression =>
                $"{leftValue} === true || {rightValue} === true ? true : {leftValue} == null || {rightValue} == null ? null : false",
            SyntaxKind.ExclusiveOrExpression =>
                $"{leftValue} == null || {rightValue} == null ? null : {leftValue} !== {rightValue}",
            _ => throw Unsupported("WRK108", expression)
        };
        return $"(({leftValue}, {rightValue}) => {result})({left}, {right})";
    }

    private static bool IsNullableBoolean(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
        && nullable.TypeArguments[0].SpecialType == SpecialType.System_Boolean;

    private string LiftedNumericBinary(BinaryExpressionSyntax expression, IBinaryOperation operation)
    {
        var kind = expression.Kind();
        if (kind is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
            return $"{Expression(expression.Left)} {BinaryOperator(kind)} {Expression(expression.Right)}";

        var key = $"nullable-numeric:{expression.SyntaxTree.FilePath}:{expression.SpanStart}";
        var left = _names.Get(key + ":left", "left");
        var right = _names.Get(key + ":right", "right");
        var missing = kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
            ? "false"
            : "null";
        var underlying = NullableUnderlyingType(operation.LeftOperand.Type);
        if (operation.IsChecked && underlying is SpecialType.System_Int32 or SpecialType.System_UInt32
            && kind is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression
                or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression)
            throw Unsupported("WRK108", expression);
        var result = kind switch
        {
            SyntaxKind.AddExpression or SyntaxKind.SubtractExpression =>
                NumericResult($"{left} {BinaryOperator(kind)} {right}", underlying, expression),
            SyntaxKind.MultiplyExpression when underlying == SpecialType.System_Int32 => $"Math.imul({left}, {right})",
            SyntaxKind.MultiplyExpression when underlying == SpecialType.System_UInt32 => $"Math.imul({left}, {right}) >>> 0",
            SyntaxKind.DivideExpression when underlying is SpecialType.System_Int32 or SpecialType.System_UInt32 =>
                $"{_helpers.Require(JavaScriptHelper.IntegerDivide)}({left}, {right}, {(underlying == SpecialType.System_UInt32 ? "true" : "false")})",
            SyntaxKind.ModuloExpression when underlying is SpecialType.System_Int32 or SpecialType.System_UInt32 =>
                $"{_helpers.Require(JavaScriptHelper.IntegerRemainder)}({left}, {right}, {(underlying == SpecialType.System_UInt32 ? "true" : "false")})",
            SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression when underlying == SpecialType.System_Single =>
                $"Math.fround({left} {BinaryOperator(kind)} {right})",
            SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression =>
                $"{left} {BinaryOperator(kind)} {right}",
            SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression =>
                $"{left} {BinaryOperator(kind)} {right}",
            _ => throw Unsupported("WRK108", expression)
        };
        return $"(({left}, {right}) => {left} == null || {right} == null ? {missing} : {result})" +
               $"({Expression(expression.Left)}, {Expression(expression.Right)})";
    }

    private static bool IsSupportedNullableNumeric(ITypeSymbol? type) =>
        NullableUnderlyingType(type) is SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Single or SpecialType.System_Double;

    private static SpecialType NullableUnderlyingType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0].SpecialType
            : SpecialType.None;

    private string DateTimeOffsetComparison(BinaryExpressionSyntax expression)
    {
        var key = $"date-time-comparison:{expression.SyntaxTree.FilePath}:{expression.SpanStart}";
        var left = _names.Get($"{key}:left", "left");
        var right = _names.Get($"{key}:right", "right");
        return $"(({left}, {right}) => new Date({left}).getTime() {BinaryOperator(expression.Kind())} " +
               $"new Date({right}).getTime())({Expression(expression.Left)}, {Expression(expression.Right)})";
    }

    private static bool IsDateValue(ITypeSymbol? type) =>
        type?.ToDisplayString() is "System.DateTimeOffset" or "System.DateTime";

    private static bool IsTimeSpan(ITypeSymbol? type) => type?.ToDisplayString() == "System.TimeSpan";

    private string TimeSpanBinary(BinaryExpressionSyntax expression, IBinaryOperation operation)
    {
        var left = Expression(expression.Left);
        var right = Expression(expression.Right);
        if (IsDateValue(operation.LeftOperand.Type))
            return expression.Kind() switch
            {
                SyntaxKind.AddExpression => DateTimeAddMilliseconds(left, right),
                SyntaxKind.SubtractExpression => DateTimeAddMilliseconds(left, $"-({right})"),
                _ => throw UnsupportedSymbol(operation.OperatorMethod, expression)
            };
        if (!IsTimeSpan(operation.LeftOperand.Type) || !IsTimeSpan(operation.RightOperand.Type))
            throw UnsupportedSymbol(operation.OperatorMethod, expression);
        return expression.Kind() switch
        {
            SyntaxKind.AddExpression => TimeSpanArithmetic(left, right, "+"),
            SyntaxKind.SubtractExpression => TimeSpanArithmetic(left, right, "-"),
            SyntaxKind.EqualsExpression => $"{left} === {right}",
            SyntaxKind.NotEqualsExpression => $"{left} !== {right}",
            SyntaxKind.LessThanExpression => $"{left} < {right}",
            SyntaxKind.LessThanOrEqualExpression => $"{left} <= {right}",
            SyntaxKind.GreaterThanExpression => $"{left} > {right}",
            SyntaxKind.GreaterThanOrEqualExpression => $"{left} >= {right}",
            _ => throw UnsupportedSymbol(operation.OperatorMethod, expression)
        };
    }

    private string BinaryOperand(ExpressionSyntax operand, bool numeric)
    {
        var value = Expression(operand);
        return numeric && _model.GetTypeInfo(operand).Type?.SpecialType == SpecialType.System_Char
            ? $"({value}).charCodeAt(0)"
            : value;
    }

    private static string ComparisonOperand(ExpressionSyntax syntax, string value) => syntax switch
    {
        BinaryExpressionSyntax or AssignmentExpressionSyntax => $"({value})",
        PrefixUnaryExpressionSyntax prefix when !prefix.IsKind(SyntaxKind.LogicalNotExpression) => $"({value})",
        _ => value
    };

    private static string BinaryOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => "===",
        SyntaxKind.NotEqualsExpression => "!==",
        SyntaxKind.LogicalAndExpression => "&&",
        SyntaxKind.LogicalOrExpression => "||",
        SyntaxKind.AddExpression => "+",
        SyntaxKind.SubtractExpression => "-",
        SyntaxKind.MultiplyExpression => "*",
        SyntaxKind.DivideExpression => "/",
        SyntaxKind.ModuloExpression => "%",
        SyntaxKind.BitwiseAndExpression => "&",
        SyntaxKind.BitwiseOrExpression => "|",
        SyntaxKind.ExclusiveOrExpression => "^",
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
    private static string LowerNativeMethodName(string value) => LowerFirst(
        value.EndsWith("Async", StringComparison.Ordinal) ? value[..^"Async".Length] : value);
    private string IsPattern(IsPatternExpressionSyntax value) => value.Pattern switch
    {
        ConstantPatternSyntax constant when constant.Expression.IsKind(SyntaxKind.NullLiteralExpression) => $"{Expression(value.Expression)} == null",
        UnaryPatternSyntax unary
            when unary.IsKind(SyntaxKind.NotPattern)
                && unary.Pattern is ConstantPatternSyntax constant
                && constant.Expression.IsKind(SyntaxKind.NullLiteralExpression) =>
            $"{Expression(value.Expression)} != null",
        _ => throw Unsupported("WRK104", value)
    };
    private static NotSupportedException Unsupported(string code, SyntaxNode node) => new($"{code}: '{node.Kind()}' is not supported yet: {node}");
    private static NotSupportedException UnsupportedSymbol(ISymbol? symbol, SyntaxNode node) =>
        new($"WRK105: '{symbol?.ToDisplayString() ?? node.ToString()}' is outside the supported Workers C# profile.");
}
