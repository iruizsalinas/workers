using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string LinqCast(SyntaxNode node, IMethodSymbol method, string source)
    {
        var type = ResolveType(method.TypeArguments.Single());
        var underlying = UnwrapNullable(type).WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var value = "value";
        var nativeStringEnum = underlying is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType
            && enumType.GetMembers().OfType<IFieldSymbol>().Any(field => field.HasConstantValue
                && (NativeEnumValue(enumType.ToDisplayString(), field.Name) is not null
                    || CryptoEnumValue(enumType.ToDisplayString(), field.Name) is not null));
        var test = underlying.SpecialType switch
        {
            SpecialType.System_String => "typeof value === \"string\"",
            SpecialType.System_Boolean => "typeof value === \"boolean\"",
            SpecialType.System_SByte => "Number.isInteger(value) && value >= -128 && value <= 127",
            SpecialType.System_Byte => "Number.isInteger(value) && value >= 0 && value <= 255",
            SpecialType.System_Int16 => "Number.isInteger(value) && value >= -32768 && value <= 32767",
            SpecialType.System_UInt16 => "Number.isInteger(value) && value >= 0 && value <= 65535",
            SpecialType.System_Int32 => "Number.isInteger(value) && value >= -2147483648 && value <= 2147483647",
            SpecialType.System_UInt32 => "Number.isInteger(value) && value >= 0 && value <= 4294967295",
            SpecialType.System_Int64 or SpecialType.System_UInt64 => "Number.isSafeInteger(value)" + (underlying.SpecialType == SpecialType.System_UInt64 ? " && value >= 0" : ""),
            SpecialType.System_Single or SpecialType.System_Double => "typeof value === \"number\"",
            SpecialType.System_Char => "typeof value === \"string\" && value.length === 1",
            _ when nativeStringEnum => "typeof value === \"string\"",
            _ when underlying.TypeKind == TypeKind.Enum => "typeof value === \"number\" || value instanceof Number",
            _ when underlying is INamedTypeSymbol { IsTupleType: true } tuple => $"Array.isArray(value) && value.length === {tuple.TupleElements.Length}",
            _ when underlying is INamedTypeSymbol pair && IsKeyValuePair(pair) => "Array.isArray(value) && value.length === 2",
            _ when underlying is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte } => "value instanceof Uint8Array",
            _ when underlying is IArrayTypeSymbol => "Array.isArray(value)",
            _ when IsSequenceType(underlying) => "Array.isArray(value) || value instanceof Uint8Array",
            _ when underlying is INamedTypeSymbol collection && collection.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.Queue<T>" or "System.Collections.Generic.Stack<T>" => "Array.isArray(value)",
            _ when IsDictionary(underlying) => "value != null && typeof value === \"object\" && !Array.isArray(value)",
            _ when IsSet(underlying) => "value != null && typeof value.has === \"function\" && typeof value.add === \"function\"",
            _ when underlying.OriginalDefinition.ToDisplayString() == "System.Linq.IGrouping<TKey, TElement>" =>
                "Array.isArray(value) && Object.hasOwn(value, \"key\")",
            _ when underlying.OriginalDefinition.ToDisplayString() == "System.Linq.IOrderedEnumerable<TElement>" =>
                $"value != null && value[{RequireHelperName(JavaScriptHelper.LinqOrder, "linqOrderState")}] != null",
            _ when underlying.OriginalDefinition.ToDisplayString() is "System.Collections.Generic.ICollection<T>"
                or "System.Collections.Generic.IReadOnlyCollection<T>" =>
                QueryCollectionTypeTest((INamedTypeSymbol)underlying),
            _ when underlying.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                || underlying.ToDisplayString() == "System.Collections.IEnumerable" => "value != null && (typeof value[Symbol.iterator] === \"function\" || typeof value === \"object\")",
            _ when QueryNativeConstructor(underlying) is { } constructor => $"typeof globalThis.{constructor} === \"function\" && value instanceof globalThis.{constructor}",
            _ when underlying is INamedTypeSymbol structural && BindingIntrinsicRegistry.IsStructuralType(structural) => "value != null && typeof value === \"object\"",
            _ => TypePatternTest(underlying, value, null, node)
        };
        var nullable = type.IsReferenceType || IsNullable(type);
        if (nullable) test = $"value == null || ({test})";
        var result = underlying.TypeKind == TypeKind.Enum && !nativeStringEnum
            ? nullable ? "value == null ? value : Number(value)" : "Number(value)" : CopyValue(type, value);
        var convert = $"value => {{ if ({test}) return {result}; throw {NewBuiltInException("System.InvalidCastException", "Specified cast is not valid.")}; }}";
        return Linq(JavaScriptHelper.LinqCast, source, [convert]);
    }

    private static string QueryCollectionTypeTest(INamedTypeSymbol type)
    {
        var plainDictionary = type.TypeArguments[0] is INamedTypeSymbol pair && IsKeyValuePair(pair)
            && pair.TypeArguments[0].SpecialType == SpecialType.System_String
                ? " || Object.getPrototypeOf(value) === null || Object.getPrototypeOf(value) === Object.prototype"
                : "";
        return "value != null && typeof value === \"object\" && (Array.isArray(value) || value instanceof Uint8Array"
            + " || value instanceof Set || value instanceof Map || typeof value[Symbol.iterator] === \"function\" && Number.isInteger(value.length)"
            + plainDictionary + ")";
    }

    private static string? QueryNativeConstructor(ITypeSymbol type) => type.ToDisplayString() switch
    {
        "Workers.Url" => "URL",
        "Workers.UrlSearchParams" => "URLSearchParams",
        "Workers.HtmlRewriter" => "HTMLRewriter",
        "Workers.Request" or "Workers.Response" or "Workers.Headers" or "Workers.Date" or "Workers.RegExp"
            or "Workers.AbortController" or "Workers.AbortSignal" or "Workers.TextEncoder" or "Workers.TextDecoder"
            or "Workers.Blob" or "Workers.File" or "Workers.FormData" or "Workers.ReadableStream"
            or "Workers.WritableStream" or "Workers.TransformStream" or "Workers.IdentityTransformStream"
            or "Workers.FixedLengthStream" or "Workers.CompressionStream" or "Workers.DecompressionStream"
            or "Workers.WebSocket" or "Workers.CryptoKey" => type.Name,
        _ => null
    };

    private string LinqSelectMany(
        SyntaxNode sourceNode,
        IMethodSymbol method,
        IParameterSymbol[] parameters,
        string source,
        string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || !IsDelegate(parameters[0].Type)
            || arguments.Length == 2 && !IsDelegate(parameters[1].Type))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqSelectMany, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null", arguments.Length == 2 ? "true" : "false"]);
    }

    private string LinqOrdering(
        SyntaxNode sourceNode,
        IMethodSymbol method,
        IParameterSymbol[] parameters,
        string source,
        string[] arguments,
        bool descending,
        bool append)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || parameters[0].Type is not INamedTypeSymbol { DelegateInvokeMethod: { } selector }
            || selector.Parameters.Length != 1)
            throw UnsupportedSymbol(method, sourceNode);
        var keyKind = arguments.Length == 2
            ? StringComparerOrderKind(sourceNode, method, selector.ReturnType)
            : selector.ReturnType.SpecialType == SpecialType.System_String
                ? LinqOrderKeyKind.Ordinal : GetLinqOrderKeyKind(selector.ReturnType);
        if (keyKind is null) throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqOrder, source,
            [arguments[0], descending ? "true" : "false", ((int)keyKind).ToString(), append ? "true" : "false"]);
    }

    private static LinqOrderKeyKind? GetLinqOrderKeyKind(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        if (type.SpecialType == SpecialType.System_Char) return LinqOrderKeyKind.Character;
        if (type.TypeKind == TypeKind.Enum || type.SpecialType == SpecialType.System_Boolean
            || type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double)
            return LinqOrderKeyKind.Numeric;
        return null;
    }

    private enum LinqOrderKeyKind { Numeric, Character, Ordinal, OrdinalIgnoreCase }

    private LinqOrderKeyKind? StringComparerOrderKind(SyntaxNode source, IMethodSymbol method, ITypeSymbol keyType)
    {
        if (keyType.SpecialType != SpecialType.System_String
            || source is not InvocationExpressionSyntax invocation
            || InvocationArgument(invocation, method, "comparer") is not { } comparer
            || _model.GetSymbolInfo(comparer).Symbol is not IPropertySymbol
            {
                IsStatic: true, ContainingType: { } comparerType, Name: var name
            }
            || comparerType.ToDisplayString() != "System.StringComparer")
            return null;
        return name switch
        {
            "Ordinal" => LinqOrderKeyKind.Ordinal,
            "OrdinalIgnoreCase" => LinqOrderKeyKind.OrdinalIgnoreCase,
            _ => null
        };
    }

    private string LinqGroupBy(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || !HasDelegateArity(parameters[0].Type, 1)
            || arguments.Length == 2 && !HasDelegateArity(parameters[1].Type, 1)
            || method.TypeArguments.Length < 2)
            throw UnsupportedSymbol(method, sourceNode);
        var lookup = QueryKeyLookup(method.TypeArguments[1], sourceNode);
        return Linq(JavaScriptHelper.LinqGroupBy, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null", arguments.Length == 2 ? "true" : "false", .. lookup is null ? [] : new[] { lookup }]);
    }

    private string LinqDictionary(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (method.TypeArguments.Length >= 2 && IsCompositeKey(method.TypeArguments[1])
            && arguments.Length is 1 or 2 && parameters.All(parameter => HasDelegateArity(parameter.Type, 1)))
        {
            var resultType = (INamedTypeSymbol)ResolveType(method.ReturnType);
            return $"{RequireHelperName(JavaScriptHelper.StructuralKeys, "keyToDictionary")}({source}, {arguments[0]}, {(arguments.Length == 2 ? arguments[1] : "null")}, () => {KeyCollection(resultType, sourceNode)}, {(arguments.Length == 2 ? "true" : "false")})";
        }
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || parameters.Any(parameter => !HasDelegateArity(parameter.Type, 1))
            || method.TypeArguments.Length < 2
            || method.TypeArguments[1].SpecialType != SpecialType.System_String)
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqToDictionary, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null", arguments.Length == 2 ? "true" : "false"]);
    }

    private string LinqLookup(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is not (1 or 2) || parameters.Length != arguments.Length
            || parameters.Any(parameter => !HasDelegateArity(parameter.Type, 1))
            || method.TypeArguments.Length < 2)
            throw UnsupportedSymbol(method, sourceNode);
        var lookup = QueryKeyLookup(method.TypeArguments[1], sourceNode);
        return Linq(JavaScriptHelper.LinqToLookup, source,
            [arguments[0], arguments.Length == 2 ? arguments[1] : "null", arguments.Length == 2 ? "true" : "false", .. lookup is null ? [] : new[] { lookup }]);
    }

    private string LinqNumericAggregate(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length > 1 || parameters.Length != arguments.Length
            || arguments.Length == 1 && !HasDelegateArity(parameters[0].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        var numericType = UnwrapNullable(method.ReturnType);
        var kind = numericType.SpecialType switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_Single => 1,
            SpecialType.System_Double => 2,
            _ => -1
        };
        if (kind < 0) throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqNumericAggregate, source,
            [method.Name == "Sum" ? "0" : "1", arguments.Length == 0 ? "null" : arguments[0],
                kind.ToString(), IsNullable(method.ReturnType) ? "true" : "false", arguments.Length == 1 ? "true" : "false"]);
    }

    private string LinqExtremum(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        var by = method.Name.EndsWith("By", StringComparison.Ordinal);
        if (arguments.Length > 1 || parameters.Length != arguments.Length
            || by && arguments.Length != 1
            || arguments.Length == 1 && !HasDelegateArity(parameters[0].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        var hasSelector = arguments.Length == 1;
        var keyType = hasSelector
            ? ((INamedTypeSymbol)parameters[0].Type).DelegateInvokeMethod!.ReturnType
            : method.ReturnType;
        var kind = GetLinqOrderKeyKind(keyType);
        if (kind is null) throw UnsupportedSymbol(method, sourceNode);
        var canBeNull = IsNullable(method.ReturnType) || method.ReturnType.IsReferenceType;
        var floating = (UnwrapNullable(keyType).SpecialType is SpecialType.System_Single or SpecialType.System_Double)
            && method.OriginalDefinition.ReturnType.TypeKind != TypeKind.TypeParameter;
        return Linq(JavaScriptHelper.LinqExtremum, source,
            [hasSelector ? arguments[0] : "null", method.Name.StartsWith("Max", StringComparison.Ordinal) ? "true" : "false",
                ((int)kind).ToString(), canBeNull ? "true" : "false", by ? "true" : "false",
                hasSelector && !by ? "true" : "false", floating ? "true" : "false"]);
    }

    private string LinqSet(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        var by = method.Name.EndsWith("By", StringComparison.Ordinal);
        if (arguments.Length != (by ? 2 : 1) || parameters.Length != arguments.Length
            || by && !HasDelegateArity(parameters[1].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        var keyType = by
            ? ((INamedTypeSymbol)parameters[1].Type).DelegateInvokeMethod!.ReturnType
            : method.TypeArguments[0];
        if (!SupportsLinqEquality(keyType)) throw UnsupportedSymbol(method, sourceNode);
        var mode = method.Name.StartsWith("Union", StringComparison.Ordinal) ? 0
            : method.Name.StartsWith("Intersect", StringComparison.Ordinal) ? 1 : 2;
        return Linq(JavaScriptHelper.LinqSet, source,
            [arguments[0], by ? arguments[1] : "null", mode.ToString(), by && mode != 0 ? "true" : "false", by ? "true" : "false"]);
    }

    private string LinqDefaultIfEmpty(
        SyntaxNode sourceNode, IMethodSymbol method, string source, string[] arguments)
    {
        if (arguments.Length > 1) throw UnsupportedSymbol(method, sourceNode);
        var defaultValue = arguments.Length == 1 ? arguments[0] : DefaultFieldValue(method.TypeArguments[0], sourceNode);
        return Linq(JavaScriptHelper.LinqDefaultIfEmpty, source, [defaultValue]);
    }

    private string LinqZip(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length != 2 || parameters.Length != 2 || !HasDelegateArity(parameters[1].Type, 2))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqZip, source, arguments);
    }

    private string LinqAggregate(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length is < 1 or > 3 || parameters.Length != arguments.Length)
            throw UnsupportedSymbol(method, sourceNode);
        var seedless = arguments.Length == 1;
        var accumulatorIndex = seedless ? 0 : 1;
        if (!HasDelegateArity(parameters[accumulatorIndex].Type, 2)
            || arguments.Length == 3 && !HasDelegateArity(parameters[2].Type, 1))
            throw UnsupportedSymbol(method, sourceNode);
        return Linq(JavaScriptHelper.LinqAggregate, source,
            [arguments[accumulatorIndex], seedless ? "null" : arguments[0], seedless ? "false" : "true",
                arguments.Length == 3 ? arguments[2] : "null", arguments.Length == 3 ? "true" : "false"]);
    }

    private string LinqJoin(
        SyntaxNode sourceNode, IMethodSymbol method, IParameterSymbol[] parameters,
        string source, string[] arguments)
    {
        if (arguments.Length != 4 || parameters.Length != 4
            || !HasDelegateArity(parameters[1].Type, 1)
            || !HasDelegateArity(parameters[2].Type, 1)
            || !HasDelegateArity(parameters[3].Type, 2)
            || method.TypeArguments.Length < 3)
            throw UnsupportedSymbol(method, sourceNode);
        var lookup = QueryKeyLookup(method.TypeArguments[2], sourceNode);
        return Linq(JavaScriptHelper.LinqJoin, source,
            [arguments[0], arguments[1], arguments[2], arguments[3], method.Name == "GroupJoin" ? "true" : "false", .. lookup is null ? [] : new[] { lookup }]);
    }

    private string? QueryKeyLookup(ITypeSymbol type, SyntaxNode source)
    {
        type = ResolveType(type);
        if (SupportsLinqEquality(type) || type.SpecialType == SpecialType.System_Object) return null;
        return $"() => {_helpers.Require(JavaScriptHelper.LinqLookup)}({EqualityFunction(type, source)}, value => {KeyHash(type, "value", source)})";
    }

}
