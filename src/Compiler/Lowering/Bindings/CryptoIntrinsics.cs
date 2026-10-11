using static BindingIntrinsicRegistry;

internal static class CryptoIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.Crypto", "DigestAsync")] = new("", BindingIntrinsicKind.CryptoDigestBody),
            [Key("Workers.Crypto", "SignHmacAsync")] = new("", BindingIntrinsicKind.CryptoSignHmac),
            [Key("Workers.Crypto", "VerifyHmacAsync")] = new("", BindingIntrinsicKind.CryptoVerifyHmac),
            [Key("Workers.Crypto", "GetRandomValues")] = Direct("getRandomValues"),
            [Key("Workers.SubtleCrypto", "TimingSafeEqual")] = Direct("timingSafeEqual"),
            [Key("Workers.SubtleCrypto", "DigestAsync")] = Bytes("digest"),
            [Key("Workers.SubtleCrypto", "EncryptAsync")] = Bytes("encrypt"),
            [Key("Workers.SubtleCrypto", "DecryptAsync")] = Bytes("decrypt"),
            [Key("Workers.SubtleCrypto", "SignAsync")] = Bytes("sign"),
            [Key("Workers.SubtleCrypto", "VerifyAsync")] = Direct("verify"),
            [Key("Workers.SubtleCrypto", "GenerateKeyAsync")] = Direct("generateKey"),
            [Key("Workers.SubtleCrypto", "ImportKeyAsync")] = new("importKey", BindingIntrinsicKind.CryptoImportKey),
            [Key("Workers.SubtleCrypto", "ExportKeyAsync")] = Bytes("exportKey"),
            [Key("Workers.SubtleCrypto", "ExportJwkAsync")] = new("exportKey", BindingIntrinsicKind.CryptoExportJwk),
            [Key("Workers.SubtleCrypto", "DeriveBitsAsync")] = Bytes("deriveBits"),
            [Key("Workers.SubtleCrypto", "DeriveKeyAsync")] = Direct("deriveKey"),
            [Key("Workers.SubtleCrypto", "WrapKeyAsync")] = Bytes("wrapKey"),
            [Key("Workers.SubtleCrypto", "UnwrapKeyAsync")] = Direct("unwrapKey"),
            [Key("Workers.SubtleCrypto", "Supports")] = Direct("supports"),
            [Key("Workers.SubtleCrypto", "GetPublicKeyAsync")] = Direct("getPublicKey"),
            [Key("Workers.SubtleCrypto", "EncapsulateBitsAsync")] = Direct("encapsulateBits"),
            [Key("Workers.SubtleCrypto", "EncapsulateKeyAsync")] = Direct("encapsulateKey"),
            [Key("Workers.SubtleCrypto", "DecapsulateBitsAsync")] = Bytes("decapsulateBits"),
            [Key("Workers.SubtleCrypto", "DecapsulateKeyAsync")] = Direct("decapsulateKey"),
            [Key("Workers.DigestStream", "Dispose")] = new("", BindingIntrinsicKind.Dispose)
        };

    private static BindingIntrinsic Bytes(string name) => new(name, BindingIntrinsicKind.CryptoBytesResult);
}
