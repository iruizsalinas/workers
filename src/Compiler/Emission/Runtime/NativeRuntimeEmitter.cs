using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

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
            "Workers.JsonValue" when !method.IsStatic => method.Name switch
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
                "ParseInt" => $"Number.parseInt({arguments[0]}, {(arguments.Length > 1 ? arguments[1] : ParameterDefaultValue(method.Parameters[1], source))})",
                "ParseFloat" or "IsNaN" or "IsFinite" or "IsInteger" or "IsSafeInteger" =>
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
                "Replace" => $"((expression, text, replacement) => text.replace(expression, replacement))({receiver}, {arguments[0]}, {arguments[1]})",
                "Split" => $"((expression, text) => text.split(expression))({receiver}, {arguments[0]})",
                _ => $"{receiver}.{LowerFirst(method.Name)}({values})"
            },
            "Workers.Log" => $"console.{LowerFirst(method.Name)}({values})",
            "Workers.Url" => method.Name switch
            {
                "EncodeComponent" => $"encodeURIComponent({values})",
                "DecodeComponent" => $"decodeURIComponent({values})",
                "CanParse" or "Parse" => $"URL.{LowerFirst(method.Name)}({arguments[0]}{(arguments.Length > 1 ? $", ({arguments[1]}) ?? undefined" : "")})",
                _ => ""
            },
            "Workers.Scheduler" when method.Name == "WaitAsync" =>
                $"scheduler.wait({arguments[0]}{(arguments.Length > 1 ? $", {{ signal: ({arguments[1]}) ?? undefined }}" : "")})",
            "Workers.Crypto" when method.Name == "RandomUuid" => "globalThis.crypto.randomUUID()",
            "Workers.JsonValue" when method.Name == "From" => arguments[0],
            "Workers.Base64" => method.Name == "Encode"
                ? $"({arguments[0]}).toBase64({{ alphabet: {(arguments.Length > 1 ? arguments[1] : "\"base64\"")}, omitPadding: {(arguments.Length > 2 ? arguments[2] : "false")} }})"
                : $"Uint8Array.fromBase64({arguments[0]}, {{ alphabet: {(arguments.Length > 1 ? arguments[1] : "\"base64\"")} }})",
            "Workers.Hex" => method.Name == "Encode" ? $"({arguments[0]}).toHex()" : $"Uint8Array.fromHex({arguments[0]})",
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

    private string? NativeMember(IPropertySymbol property, string receiver)
    {
        return property.ContainingType.ToDisplayString() switch
        {
            "Workers.HtmlElement" when property.Name == "Attributes" => $"Object.fromEntries({receiver}.attributes)",
            "Workers.Crypto" when property.Name == "Subtle" => "globalThis.crypto.subtle",
            "Workers.DigestStream" when property.Name == "BytesWritten" => $"Number({receiver}.bytesWritten)",
            "Workers.CryptoKeyAlgorithm" when property.Name == "Hash" => $"({receiver}.hash?.name ?? null)",
            "Workers.CryptoKeyAlgorithm" when property.Name == "PublicExponent" => $"(value => value == null ? null : new Uint8Array(value))({receiver}.publicExponent)",
            "Workers.DigestStream" when property.Name == "Digest" => $"{receiver}.digest.then(value => new Uint8Array(value))",
            "Workers.ExecOutput" when property.Name is "Stdout" or "Stderr" => $"new Uint8Array({receiver}.{LowerFirst(property.Name)})",
            "Workers.EncapsulatedBits" => $"new Uint8Array({receiver}.{LowerFirst(property.Name)})",
            "Workers.R2Checksums" => $"(value => value == null ? null : new Uint8Array(value))({receiver}.{LowerFirst(property.Name)})",
            // new Date normalizes the log's timestamp whether the binding returns a Date or an ISO string.
            "Workers.AiGatewayLog" when property.Name == "CreatedAt" => $"new Date({receiver}.created_at)",
            "Workers.QueueMetrics" when property.Name == "OldestMessageTimestamp" =>
                $"(value => value == null ? null : new Date(value))({receiver}.oldestMessageTimestamp)",
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
            _ => null
        };
    }

    private static string? NativeEnumConstant(ITypeSymbol type, object? value)
    {
        type = UnwrapNullable(type);
        if (value is null || type.TypeKind != TypeKind.Enum) return null;
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue && Equals(field.ConstantValue, value)))
        {
            if (CryptoEnumValue(type.ToDisplayString(), field.Name) is { } crypto) return crypto;
            if (NativeEnumValue(type.ToDisplayString(), field.Name) is { } native) return JsonSerializer.Serialize(native);
        }
        return null;
    }

    // Values of runtime enums, which cross into JavaScript as the strings the platform expects.
    private static string? NativeEnumValue(string type, string name) => type switch
    {
        "Workers.RedirectMode" or "Workers.ResponseBodyEncoding" or "Workers.ResponseType" or "Workers.R2ListInclude"
            or "Workers.VectorizeMetadataRetrievalLevel" => LowerFirst(name),
        "Workers.SecureTransport" or "Workers.QueueContentType" or "Workers.Base64Alphabet" or "Workers.ContainerStdio"
            or "Workers.EmailAttachmentDisposition" or "Workers.ImageEncoding" or "Workers.RequestInitCfPropertiesVaryAction"
            or "Workers.TracingSpanStatusCode" or "Workers.ConversionOutputFormat" => name.ToLowerInvariant(),
        "Workers.RequestCache" => name switch { "NoStore" => "no-store", "NoCache" => "no-cache", _ => null },
        "Workers.CompressionFormat" => name switch { "Gzip" => "gzip", "Deflate" => "deflate", "DeflateRaw" => "deflate-raw", _ => null },
        "Workers.DurableObjectRoutingMode" => name == "PrimaryOnly" ? "primary-only" : null,
        "Workers.D1SessionConstraint" => name switch { "FirstPrimary" => "first-primary", "FirstUnconstrained" => "first-unconstrained", _ => null },
        _ => null
    };
}
