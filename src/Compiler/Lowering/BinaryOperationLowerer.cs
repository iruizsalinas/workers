using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private string Binary(IBinaryOperation operation)
    {
        var expression = operation.Syntax;
        if (operation.OperatorMethod?.ContainingType.ToDisplayString() == "System.Type"
            && operation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
            return $"({Operand(operation.LeftOperand)} {(operation.OperatorKind == BinaryOperatorKind.Equals ? "===" : "!==")} {Operand(operation.RightOperand)})";
        if (operation.LeftOperand.Type?.TypeKind == TypeKind.Delegate || operation.RightOperand.Type?.TypeKind == TypeKind.Delegate)
        {
            var helper = operation.OperatorKind switch
            {
                BinaryOperatorKind.Add => "delegateCombine", BinaryOperatorKind.Subtract => "delegateRemove",
                BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals => "delegateEquals",
                _ => throw Unsupported("WRK108", expression)
            };
            var result = $"{DelegateHelper(helper)}({Expression(operation.LeftOperand)}, {Expression(operation.RightOperand)})";
            return operation.OperatorKind == BinaryOperatorKind.NotEquals ? $"!{result}" : result;
        }
        if (operation.OperatorMethod is { } recordOperator && IsSynthesizedRecordMember(recordOperator)
            && operation.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
        {
            var equals = RecordEquals(recordOperator.ContainingType, Expression(operation.LeftOperand), Expression(operation.RightOperand), expression, referenceComparison: true);
            return operation.OperatorKind == BinaryOperatorKind.Equals ? equals : $"!{equals}";
        }
        if (operation.OperatorMethod is { } sourceOperator && sourceOperator.DeclaringSyntaxReferences.Length != 0)
        {
            if (operation.IsLifted || operation.OperatorKind is BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr)
                throw Unsupported("WRK108", expression);
            return SourceOperator(sourceOperator, operation.ConstrainedToType, expression, operation.LeftOperand, operation.RightOperand);
        }
        if (operation.OperatorMethod is not null)
            throw UnsupportedSymbol(operation.OperatorMethod, expression);
        if (operation is { IsLifted: true }
            && IsSupportedNullableNumeric(operation.LeftOperand.Type)
            && IsSupportedNullableNumeric(operation.RightOperand.Type))
            return LiftedNumericBinary(operation);
        var numericType = operation.Type is INamedTypeSymbol { EnumUnderlyingType: { } enumUnderlying }
            ? enumUnderlying : operation.Type;
        var type = numericType?.SpecialType ?? SpecialType.None;
        var left = Operand(operation.LeftOperand);
        var right = Operand(operation.RightOperand);
        var integral32 = type is SpecialType.System_Int32 or SpecialType.System_UInt32;
        var narrowIntegral = type is SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16;

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

        if (operation.IsChecked == true && (integral32 || narrowIntegral)
            && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide)
            throw Unsupported("WRK108", expression);

        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64
            && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder)
            throw Unsupported("WRK108", expression);

        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64
            && operation.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr)
            return $"Number(BigInt({left}) {BinaryOperator(operation.OperatorKind)} BigInt({right}))";

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

        if (type == SpecialType.System_UInt32
            && operation.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr)
            return $"(({left} {BinaryOperator(operation.OperatorKind)} {right}) >>> 0)";

        if (narrowIntegral && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract)
            return ConvertValue(_compilation.GetSpecialType(SpecialType.System_Int32), numericType!,
                $"({left} {BinaryOperator(operation.OperatorKind)} {right})", expression);

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
    private string StringConcatOperand(IOperation operation, string value, SyntaxNode source, bool interpolation = false)
    {
        operation = Unbox(operation);
        var type = operation.Type is { } boundType ? ResolveType(boundType) : null;
        if (type is null) return "\"\"";
        var underlying = UnwrapNullable(type!);
        var nullable = type is null || type.IsReferenceType || !SymbolEqualityComparer.Default.Equals(type, underlying);
        if (interpolation && underlying.SpecialType is SpecialType.System_String or SpecialType.System_Char
            or >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return nullable ? $"({value}) ?? \"\"" : value;
        if (operation.ConstantValue is { HasValue: true, Value: string or char })
            return value;
        if (underlying.SpecialType == SpecialType.System_String)
            return operation is IInterpolatedStringOperation or IBinaryOperation { OperatorKind: BinaryOperatorKind.Add }
                ? $"({value})"
                : $"({value} ?? \"\")";
        string Convert(string item) => underlying.SpecialType switch
        {
            SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
                or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
                or SpecialType.System_Int64 or SpecialType.System_UInt64 => $"String({item})",
            SpecialType.System_Boolean => $"({item} ? \"True\" : \"False\")",
            SpecialType.System_Single => $"String({item})",
            SpecialType.System_Double => $"String({item})",
            _ when IsTextEnum(underlying) => EnumText(underlying, $"Number({item})"),
            _ when underlying is INamedTypeSymbol { IsRecord: true } record && IsUserInstanceType(record) =>
                RecordText(record, item, source),
            _ when underlying is INamedTypeSymbol user && IsUserInstanceType(user) =>
                UserObjectText(user, item, source),
            _ when underlying.SpecialType == SpecialType.System_Object => ObjectText(item),
            _ when underlying is INamedTypeSymbol { IsAnonymousType: true } anonymous => AnonymousText(anonymous, item, source),
            _ when underlying is INamedTypeSymbol { IsTupleType: true } tuple => TupleText(tuple, item, source),
            _ when underlying is INamedTypeSymbol pair && IsKeyValuePair(pair) => KeyValuePairText(pair, item, source),
            _ when underlying is IArrayTypeSymbol || HasCollectionTypeName(underlying) => System.Text.Json.JsonSerializer.Serialize(RuntimeTypeName(underlying)),
            _ => throw Unsupported("WRK108", source)
        };
        return nullable
            ? $"(($workers$value) => $workers$value == null ? \"\" : ({Convert("$workers$value")} ?? \"\"))({value})"
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
            return $"{Operand(operation.LeftOperand)} {BinaryOperator(kind)} {Operand(operation.RightOperand)}";

        var key = $"nullable-numeric:{expression.SyntaxTree.FilePath}:{expression.SpanStart}";
        var left = _names.Get(key + ":left", "left");
        var right = _names.Get(key + ":right", "right");
        var missing = kind is BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
            or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual
            ? "false"
            : "null";
        var underlying = NullableUnderlyingType(operation.LeftOperand.Type);
        if (operation.IsChecked && underlying is SpecialType.System_SByte or SpecialType.System_Byte
                or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            && kind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract
                or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide)
            throw Unsupported("WRK108", expression);
        if (underlying is SpecialType.System_Int64 or SpecialType.System_UInt64
            && kind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply
                or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder)
            throw Unsupported("WRK108", expression);
        var result = kind switch
        {
            BinaryOperatorKind.Add or BinaryOperatorKind.Subtract when underlying is
                SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 =>
                ConvertValue(_compilation.GetSpecialType(SpecialType.System_Int32), _compilation.GetSpecialType(underlying),
                    $"({left} {BinaryOperator(kind)} {right})", expression),
            BinaryOperatorKind.Add or BinaryOperatorKind.Subtract =>
                NumericResult($"{left} {BinaryOperator(kind)} {right}", underlying, expression),
            BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr
                when underlying is SpecialType.System_Int64 or SpecialType.System_UInt64 =>
                $"Number(BigInt({left}) {BinaryOperator(kind)} BigInt({right}))",
            BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr
                when underlying == SpecialType.System_UInt32 => $"(({left} {BinaryOperator(kind)} {right}) >>> 0)",
            BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr =>
                $"{left} {BinaryOperator(kind)} {right}",
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
        NullableUnderlyingType(type) is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Single or SpecialType.System_Double;

    private static SpecialType NullableUnderlyingType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0] is INamedTypeSymbol { EnumUnderlyingType: { } underlying }
                ? underlying.SpecialType : nullable.TypeArguments[0].SpecialType
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
        if (_userIdentifiers.TryGetValue(symbol, out var existing))
            return existing;
        if (IsLegalJavascriptIdentifier(value) && !JavascriptReservedWords.Contains(value)
            && !JavascriptRuntimeGlobals.Contains(value))
            return value;
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
    // Emitted expressions reference these native globals in the user's lexical scope.
    // Allocate C# bindings with the same names so they cannot capture those references.
    private static readonly HashSet<string> JavascriptRuntimeGlobals = new(StringComparer.Ordinal)
    {
        "globalThis", "undefined", "Infinity", "NaN", "eval", "arguments",
        "Object", "Function", "Boolean", "Symbol", "Number", "BigInt", "String", "Math", "JSON",
        "Array", "Map", "Set", "WeakMap", "WeakSet", "Promise", "Date", "RegExp",
        "Error", "TypeError", "RangeError", "SyntaxError", "ReferenceError", "AggregateError",
        "ArrayBuffer", "SharedArrayBuffer", "DataView", "Uint8Array", "Uint8ClampedArray",
        "Int8Array", "Uint16Array", "Int16Array", "Uint32Array", "Int32Array", "Float32Array", "Float64Array",
        "BigInt64Array", "BigUint64Array", "URL", "URLSearchParams", "Request", "Response", "Headers",
        "Blob", "File", "FormData", "TextEncoder", "TextDecoder", "AbortController", "AbortSignal",
        "ReadableStream", "WritableStream", "TransformStream", "IdentityTransformStream", "FixedLengthStream",
        "CompressionStream", "DecompressionStream", "HTMLRewriter", "WebSocket", "WebSocketPair",
        "WebSocketRequestResponsePair", "CryptoKey", "SubtleCrypto", "DOMException",
        "console", "fetch", "caches", "scheduler", "crypto", "performance", "encodeURIComponent", "decodeURIComponent",
        "encodeURI", "decodeURI", "parseInt", "parseFloat", "isNaN", "isFinite", "atob", "btoa",
        "setTimeout", "clearTimeout", "setInterval", "clearInterval", "queueMicrotask", "structuredClone"
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
