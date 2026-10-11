using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Binary(IBinaryOperation operation)
    {
        var expression = operation.Syntax;
        if (operation.OperatorMethod is { } recordOperator && IsSynthesizedRecordMember(recordOperator)
            && operation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
        {
            var equals = RecordEquals(recordOperator.ContainingType, Expression(operation.LeftOperand), Expression(operation.RightOperand), expression, referenceComparison: true);
            return operation.OperatorKind == BinaryOperatorKind.Equals ? equals : $"!{equals}";
        }
        if (operation.OperatorMethod is not null)
            throw UnsupportedSymbol(operation.OperatorMethod, expression);
        if (operation is { IsLifted: true }
            && IsSupportedNullableNumeric(operation.LeftOperand.Type)
            && IsSupportedNullableNumeric(operation.RightOperand.Type))
            return LiftedNumericBinary(operation);
        var type = operation.Type?.SpecialType ?? SpecialType.None;
        var left = Operand(operation.LeftOperand);
        var right = Operand(operation.RightOperand);
        var integral32 = type is SpecialType.System_Int32 or SpecialType.System_UInt32;

        if (operation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
            && (operation.LeftOperand.ConstantValue is { HasValue: true, Value: null }
                || operation.RightOperand.ConstantValue is { HasValue: true, Value: null }))
            return $"{left} {(operation.OperatorKind == BinaryOperatorKind.Equals ? "==" : "!=")} {right}";

        if (IsNullableBoolean(operation.Type)
            && operation.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr)
            return NullableBooleanBinary(operation, left, right);

        if (type == SpecialType.System_Boolean
            && operation.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr)
            return $"Boolean({left} {BinaryOperator(operation.OperatorKind)} {right})";

        if (operation.IsChecked == true && integral32
            && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide)
            throw Unsupported("WRK108", expression);

        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64
            && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder)
            throw Unsupported("WRK108", expression);

        if (operation.OperatorKind == BinaryOperatorKind.Divide && integral32)
        {
            var helper = _helpers.Require(JavaScriptHelper.IntegerDivide);
            return $"{helper}({left}, {right}, {(type == SpecialType.System_UInt32 ? "true" : "false")})";
        }

        if (operation.OperatorKind == BinaryOperatorKind.Remainder && integral32)
        {
            var helper = _helpers.Require(JavaScriptHelper.IntegerRemainder);
            return $"{helper}({left}, {right}, {(type == SpecialType.System_UInt32 ? "true" : "false")})";
        }

        if (integral32)
        {
            if (operation.OperatorKind == BinaryOperatorKind.Multiply)
                return type == SpecialType.System_UInt32 ? $"(Math.imul({left}, {right}) >>> 0)" : $"Math.imul({left}, {right})";
            if (operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract)
            {
                var native = $"({left} {BinaryOperator(operation.OperatorKind)} {right})";
                return type == SpecialType.System_UInt32 ? $"({native} >>> 0)" : $"({native} | 0)";
            }
        }

        if (operation.OperatorKind == BinaryOperatorKind.Add && type == SpecialType.System_String)
            return $"{StringConcatOperand(operation.LeftOperand, left, expression)} + {StringConcatOperand(operation.RightOperand, right, expression)}";

        if (type == SpecialType.System_Single
            && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide)
            return $"Math.fround({left} {BinaryOperator(operation.OperatorKind)} {right})";

        if (operation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals
            or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
            or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual)
            return $"{left} {BinaryOperator(operation.OperatorKind)} " +
                   right;

        return $"{left} {BinaryOperator(operation.OperatorKind)} {right}";
    }

    // Converts an operand of string concatenation the way String.Concat calls ToString().
    private string StringConcatOperand(IOperation operation, string value, SyntaxNode source)
    {
        operation = Unbox(operation);
        var type = operation.Type;
        if (type is null) return "\"\"";
        var underlying = UnwrapNullable(type!);
        var nullable = type is null || type.IsReferenceType || !SymbolEqualityComparer.Default.Equals(type, underlying);
        if (operation.ConstantValue is { HasValue: true, Value: string or char })
            return value;
        if (underlying.SpecialType == SpecialType.System_String)
            return operation is IInterpolatedStringOperation or IBinaryOperation { OperatorKind: BinaryOperatorKind.Add }
                ? $"({value})"
                : $"({value} ?? \"\")";
        string Convert(string item) => underlying.SpecialType switch
        {
            SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
                or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 => $"String({item})",
            SpecialType.System_Boolean => $"({item} ? \"True\" : \"False\")",
            SpecialType.System_Single => $"String({item})",
            SpecialType.System_Double => $"String({item})",
            _ when IsTextEnum(underlying) => EnumText(underlying, item),
            _ when underlying is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record) =>
                RecordText(record, item, source),
            _ => throw Unsupported("WRK108", source)
        };
        return nullable
            ? $"(($workers$value) => $workers$value == null ? \"\" : {Convert("$workers$value")})({value})"
            : $"({Convert(value)})";
    }

    private string NullableBooleanBinary(IBinaryOperation operation, string left, string right)
    {
        var expression = operation.Syntax;
        var key = $"nullable-boolean:{expression.SyntaxTree.FilePath}:{expression.SpanStart}";
        var leftValue = _names.Get(key + ":left", "left");
        var rightValue = _names.Get(key + ":right", "right");
        var result = operation.OperatorKind switch
        {
            BinaryOperatorKind.And =>
                $"{leftValue} === false || {rightValue} === false ? false : {leftValue} == null || {rightValue} == null ? null : true",
            BinaryOperatorKind.Or =>
                $"{leftValue} === true || {rightValue} === true ? true : {leftValue} == null || {rightValue} == null ? null : false",
            BinaryOperatorKind.ExclusiveOr =>
                $"{leftValue} == null || {rightValue} == null ? null : {leftValue} !== {rightValue}",
            _ => throw Unsupported("WRK108", expression)
        };
        return $"(({leftValue}, {rightValue}) => {result})({left}, {right})";
    }

    private static bool IsNullableBoolean(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
        && nullable.TypeArguments[0].SpecialType == SpecialType.System_Boolean;

    private string LiftedNumericBinary(IBinaryOperation operation)
    {
        var expression = operation.Syntax;
        var kind = operation.OperatorKind;
        if (kind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
            return $"{Expression(operation.LeftOperand)} {BinaryOperator(kind)} {Expression(operation.RightOperand)}";

        var key = $"nullable-numeric:{expression.SyntaxTree.FilePath}:{expression.SpanStart}";
        var left = _names.Get(key + ":left", "left");
        var right = _names.Get(key + ":right", "right");
        var missing = kind is BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
            or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual
            ? "false"
            : "null";
        var underlying = NullableUnderlyingType(operation.LeftOperand.Type);
        if (operation.IsChecked && underlying is SpecialType.System_Int32 or SpecialType.System_UInt32
            && kind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract
                or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide)
            throw Unsupported("WRK108", expression);
        var result = kind switch
        {
            BinaryOperatorKind.Add or BinaryOperatorKind.Subtract =>
                NumericResult($"{left} {BinaryOperator(kind)} {right}", underlying, expression),
            BinaryOperatorKind.Multiply when underlying == SpecialType.System_Int32 => $"Math.imul({left}, {right})",
            BinaryOperatorKind.Multiply when underlying == SpecialType.System_UInt32 => $"(Math.imul({left}, {right}) >>> 0)",
            BinaryOperatorKind.Divide when underlying is SpecialType.System_Int32 or SpecialType.System_UInt32 =>
                $"{_helpers.Require(JavaScriptHelper.IntegerDivide)}({left}, {right}, {(underlying == SpecialType.System_UInt32 ? "true" : "false")})",
            BinaryOperatorKind.Remainder when underlying is SpecialType.System_Int32 or SpecialType.System_UInt32 =>
                $"{_helpers.Require(JavaScriptHelper.IntegerRemainder)}({left}, {right}, {(underlying == SpecialType.System_UInt32 ? "true" : "false")})",
            BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide when underlying == SpecialType.System_Single =>
                $"Math.fround({left} {BinaryOperator(kind)} {right})",
            BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder =>
                $"{left} {BinaryOperator(kind)} {right}",
            BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
                or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual =>
                $"{left} {BinaryOperator(kind)} {right}",
            _ => throw Unsupported("WRK108", expression)
        };
        return $"(({left}, {right}) => {left} == null || {right} == null ? {missing} : {result})" +
               $"({Expression(operation.LeftOperand)}, {Expression(operation.RightOperand)})";
    }

    private static bool IsSupportedNullableNumeric(ITypeSymbol? type) =>
        NullableUnderlyingType(type) is SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Single or SpecialType.System_Double;

    private static SpecialType NullableUnderlyingType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0].SpecialType
            : SpecialType.None;

    private static string BinaryOperator(BinaryOperatorKind kind) => kind switch
    {
        BinaryOperatorKind.Equals => "===",
        BinaryOperatorKind.NotEquals => "!==",
        BinaryOperatorKind.ConditionalAnd => "&&",
        BinaryOperatorKind.ConditionalOr => "||",
        BinaryOperatorKind.Add => "+",
        BinaryOperatorKind.Subtract => "-",
        BinaryOperatorKind.Multiply => "*",
        BinaryOperatorKind.Divide => "/",
        BinaryOperatorKind.Remainder => "%",
        BinaryOperatorKind.And => "&",
        BinaryOperatorKind.Or => "|",
        BinaryOperatorKind.ExclusiveOr => "^",
        BinaryOperatorKind.LessThan => "<",
        BinaryOperatorKind.LessThanOrEqual => "<=",
        BinaryOperatorKind.GreaterThan => ">",
        BinaryOperatorKind.GreaterThanOrEqual => ">=",
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
    private static NotSupportedException Locate(NotSupportedException exception, SyntaxNode node) =>
        WorkerDiagnostics.WithLocation(exception, node);
    private static NotSupportedException Unsupported(string code, SyntaxNode node) =>
        Locate(new($"{code}: '{node.Kind()}' is not supported yet: {node}"), node);
    private static NotSupportedException UnsupportedSymbol(ISymbol? symbol, SyntaxNode node) =>
        Locate(new($"WRK105: '{symbol?.ToDisplayString() ?? node.ToString()}' is outside the supported Workers C# profile."), node);
}
