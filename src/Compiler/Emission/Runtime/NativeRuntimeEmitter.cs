using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private bool TryEmitNativeInvocation(
        InvocationExpressionSyntax source, IMethodSymbol method, string[] arguments,
        MemberAccessExpressionSyntax? member, string? receiverOverride, out string result)
    {
        var owner = method.ContainingType.ToDisplayString();
        if (method.IsStatic && owner is "Workers.Crypto" or "Workers.SubtleCrypto"
            && BindingIntrinsicRegistry.TryGet(method, out var cryptoIntrinsic))
        {
            result = EmitBindingIntrinsic(owner == "Workers.Crypto" ? "globalThis.crypto" : "globalThis.SubtleCrypto",
                source, method, cryptoIntrinsic);
            return true;
        }
        var receiver = receiverOverride ?? (member is null || method.IsStatic ? "" : Expression(member.Expression));
        var values = string.Join(", ", arguments);
        result = owner switch
        {
            "Workers.Json" when method.Name == "Stringify" => $"JSON.stringify({values})",
            "Workers.Json" when method.Name == "Parse" => NativeJsonRead(method, $"JSON.parse({values})", source),
            "Workers.JsonValue" => method.Name switch
            {
                "AsString" or "AsNumber" or "AsBoolean" =>
                    $"{_helpers.Require(JavaScriptHelper.JsonExpect)}({receiver}, \"{method.Name[2..].ToLowerInvariant()}\")",
                "As" => NativeJsonRead(method, receiver, source),
                "Has" => $"Object.hasOwn({receiver}, {values})",
                "Values" => $"Object.values({receiver})",
                _ => throw UnsupportedSymbol(method, source)
            },
            "Workers.Date" => $"{(method.IsStatic ? "Date" : receiver)}.{DateMethod(method.Name)}({values})",
            "Workers.Numbers" => method.Name switch
            {
                "From" => $"Number({values})",
                "ParseInt" or "ParseFloat" or "IsNaN" or "IsFinite" or "IsInteger" or "IsSafeInteger" =>
                    $"Number.{LowerFirst(method.Name)}({values})",
                "ToFixed" => $"({arguments[0]}).toFixed({string.Join(", ", arguments.Skip(1))})",
                "ToPrecision" => $"({arguments[0]}).toPrecision({arguments[1]})",
                "ToRadixString" => $"({arguments[0]}).toString({string.Join(", ", arguments.Skip(1))})",
                _ => $"Math.{LowerFirst(method.Name)}({values})"
            },
            "Workers.AbortSignal" => $"{(method.IsStatic ? "AbortSignal" : receiver)}.{LowerFirst(method.Name)}({values})",
            "Workers.AbortController" => $"{receiver}.abort({values})",
            "Workers.RegExp" => method.Name switch
            {
                "Replace" => $"{arguments[0]}.replace({receiver}, {arguments[1]})",
                "Split" => $"{arguments[0]}.split({receiver})",
                _ => $"{receiver}.{LowerFirst(method.Name)}({values})"
            },
            "Workers.Log" => $"console.{LowerFirst(method.Name)}({values})",
            "Workers.Url" when method.Name == "EncodeComponent" => $"encodeURIComponent({values})",
            "Workers.Url" when method.Name == "DecodeComponent" => $"decodeURIComponent({values})",
            "Workers.Timers" when method.Name == "WaitAsync" =>
                $"scheduler.wait({arguments[0]}{(arguments.Length > 1 ? $", {{ signal: {arguments[1]} ?? undefined }}" : "")})",
            "Workers.Crypto" when method.Name == "RandomUuid" => "globalThis.crypto.randomUUID()",
            "Workers.TextCodec" when method.Name == "EncodeUtf8" => $"new TextEncoder().encode({values})",
            "Workers.TextCodec" when method.Name == "EncodeBase64" => $"({arguments[0]}).toBase64()",
            "Workers.TextCodec" when method.Name == "DecodeBase64" => $"Uint8Array.fromBase64({arguments[0]})",
            "Workers.TextCodec" when method.Name == "EncodeHex" => $"({arguments[0]}).toHex()",
            "Workers.TextCodec" when method.Name == "DecodeHex" => $"Uint8Array.fromHex({arguments[0]})",
            _ => ""
        };
        return result.Length != 0;
    }

    private string NativeJsonRead(IMethodSymbol method, string value, SyntaxNode source) =>
        method.TypeArguments.FirstOrDefault() is { } type
            && JsonBoundaryConverter(type, source, JsonWebMode) is { } convert
            ? $"({convert})({value})" : value;

    private static string DateMethod(string name) => name switch
    {
        "Utc" => "UTC",
        "ToIsoString" => "toISOString",
        "ToUtcString" => "toUTCString",
        "ToJson" => "toJSON",
        _ => LowerFirst(name).Replace("Utc", "UTC", StringComparison.Ordinal)
    };

    private string? NativeMember(IOperation? instance, IPropertySymbol property)
    {
        var receiver = property.IsStatic ? "" : instance is null ? "this" : Operand(instance);
        return property.ContainingType.ToDisplayString() switch
        {
            "Workers.Crypto" when property.Name == "Subtle" => "globalThis.crypto.subtle",
            "Workers.DigestStream" when property.Name == "BytesWritten" => $"Number({receiver}.bytesWritten)",
            "Workers.JsonWebKey" when property.Name == "KeyOps" => $"{receiver}.key_ops",
            "Workers.CryptoKeyAlgorithm" when property.Name == "Hash" => $"({receiver}.hash?.name ?? null)",
            "Workers.CryptoKeyAlgorithm" when property.Name == "PublicExponent" => $"(value => value == null ? null : new Uint8Array(value))({receiver}.publicExponent)",
            "Workers.EncapsulatedBits" => $"new Uint8Array({receiver}.{LowerFirst(property.Name)})",
            "Workers.EncapsulatedKey" when property.Name == "Ciphertext" => $"new Uint8Array({receiver}.ciphertext)",
            "Workers.JsonValue" => property.Name switch
            {
                "IsNull" => $"({receiver} === null)",
                "IsUndefined" => $"({receiver} === undefined)",
                "Length" => $"{receiver}.length",
                _ => null
            },
            "Workers.AbortController" or "Workers.AbortSignal" or "Workers.RegExp" or "Workers.RegExpMatch" =>
                $"{receiver}.{LowerFirst(property.Name)}",
            "Workers.Request" when property.Name == "Signal" => $"{receiver}.signal",
            _ => null
        };
    }
}
