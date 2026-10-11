using Workers;

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, Context context)
    {
        var algorithm = request.QueryParameters.Get("algorithm") ?? "SHA-256";
        var data = await request.BytesAsync();
        try
        {
            return request.Path switch
            {
                "/digest" => await DigestAsync(algorithm, data),
                "/encrypt" => await EncryptAsync(algorithm, data),
                "/sign" => await SignAsync(algorithm, data, false),
                "/sign-context" => await SignAsync(algorithm, data, true),
                "/derive" => await DeriveAsync(algorithm),
                "/wrap" => await WrapAsync(algorithm, CryptoKeyFormat.Raw),
                "/wrap-jwk" => await WrapAsync(algorithm, CryptoKeyFormat.Jwk),
                "/kem" => await KemAsync(algorithm),
                "/stream" => await StreamAsync(algorithm, data),
                "/random" => Random(request),
                "/errors" => await ErrorsAsync(request.QueryParameters.Get("case") ?? "usage"),
                "/format" => await FormatAsync(algorithm, request.QueryParameters.Get("format") ?? "pkcs8"),
                "/stream-text" => await StreamTextAsync(),
                "/stream-error" => await StreamErrorAsync(request.QueryParameters.Get("case") ?? "abort"),
                _ => Response.Empty(404)
            };
        }
        catch (Exception error) { return Response.Text(error.Message, 400); }
    }

    private static async Task<Response> DigestAsync(string algorithm, byte[] data)
    {
        var hash = algorithm switch
        {
            "SHA-1" => DigestAlgorithm.Sha1,
            "SHA-384" => DigestAlgorithm.Sha384,
            "SHA-512" => DigestAlgorithm.Sha512,
            "MD5" => DigestAlgorithm.Md5,
            _ => DigestAlgorithm.Sha256
        };
        return Response.Json(new
        {
            bytes = TextCodec.EncodeHex(await Crypto.DigestAsync(hash, data)),
            subtle = TextCodec.EncodeHex(await Crypto.Subtle.DigestAsync(hash, data)),
            text = TextCodec.EncodeHex(await Crypto.DigestAsync(hash, "héllo"))
        });
    }

    private static async Task<Response> EncryptAsync(string algorithm, byte[] data)
    {
        CryptoKey encryptKey;
        CryptoKey decryptKey;
        if (algorithm == "RSA-OAEP")
        {
            var pair = await Crypto.Subtle.GenerateKeyAsync(new CryptoKeyPairAlgorithm
            { Name = algorithm, ModulusLength = 2048, PublicExponent = [1, 0, 1], Hash = DigestAlgorithm.Sha256 }, true,
                [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
            encryptKey = pair.PublicKey;
            decryptKey = pair.PrivateKey;
        }
        else
        {
            encryptKey = await Crypto.Subtle.GenerateKeyAsync(new CryptoSecretKeyAlgorithm { Name = algorithm, Length = 128 }, true,
                [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
            decryptKey = encryptKey;
        }
        var options = new CryptoEncryptAlgorithm
        { Name = algorithm, Iv = Crypto.RandomBytes(16), Counter = new byte[16], Length = 64, AdditionalData = [1, 2], TagLength = 128, Label = [3] };
        var encrypted = await Crypto.Subtle.EncryptAsync(options, encryptKey, data);
        var decrypted = await Crypto.Subtle.DecryptAsync(options, decryptKey, encrypted);
        var jwk = await Crypto.Subtle.ExportJwkAsync(decryptKey);
        var imported = await Crypto.Subtle.ImportKeyAsync(jwk, new CryptoImportAlgorithm { Name = algorithm, Hash = DigestAlgorithm.Sha256 }, true, [CryptoKeyUsage.Decrypt]);
        var second = await Crypto.Subtle.DecryptAsync(options, imported, encrypted);
        return Response.Json(new { encrypted = TextCodec.EncodeHex(encrypted), decrypted = TextCodec.EncodeHex(decrypted), imported = Crypto.TimingSafeEqual(second, data), jwk.Kty, usages = imported.Usages, name = imported.Algorithm.Name });
    }

    private static async Task<Response> SignAsync(string algorithm, byte[] data, bool context)
    {
        CryptoKey privateKey;
        CryptoKey publicKey;
        if (algorithm == "HMAC")
        {
            privateKey = await Crypto.Subtle.ImportKeyAsync(CryptoBinaryKeyFormat.Raw, TextCodec.EncodeUtf8("secret"),
                new CryptoImportAlgorithm { Name = algorithm, Hash = DigestAlgorithm.Sha256 }, true, [CryptoKeyUsage.Sign, CryptoKeyUsage.Verify]);
            publicKey = privateKey;
        }
        else
        {
            var pair = await Crypto.Subtle.GenerateKeyAsync(new CryptoKeyPairAlgorithm
            {
                Name = algorithm, ModulusLength = 2048, PublicExponent = [1, 0, 1], Hash = DigestAlgorithm.Sha256,
                NamedCurve = algorithm == "NODE-ED25519" ? "NODE-ED25519" : "P-256"
            }, true, [CryptoKeyUsage.Sign, CryptoKeyUsage.Verify]);
            privateKey = pair.PrivateKey;
            publicKey = pair.PublicKey;
        }
        var options = new CryptoSignAlgorithm { Name = algorithm, Hash = DigestAlgorithm.Sha256, SaltLength = 32, Context = context ? new byte[] { 1, 2, 3 } : null };
        var signature = await Crypto.Subtle.SignAsync(options, privateKey, data);
        var valid = await Crypto.Subtle.VerifyAsync(options, publicKey, signature, data);
        var invalid = await Crypto.Subtle.VerifyAsync(options, publicKey, signature, TextCodec.EncodeUtf8("tampered"));
        var jwk = await Crypto.Subtle.ExportJwkAsync(publicKey);
        var imported = await Crypto.Subtle.ImportKeyAsync(jwk, new CryptoImportAlgorithm { Name = algorithm, Hash = DigestAlgorithm.Sha256, NamedCurve = algorithm == "NODE-ED25519" ? "NODE-ED25519" : "P-256" }, true, [CryptoKeyUsage.Verify]);
        var importedValid = await Crypto.Subtle.VerifyAsync(options, imported, signature, data);
        var format = algorithm == "HMAC" ? CryptoBinaryKeyFormat.Raw : CryptoBinaryKeyFormat.Spki;
        var binary = await Crypto.Subtle.ExportKeyAsync(format, publicKey);
        var binaryKey = await Crypto.Subtle.ImportKeyAsync(format, binary, new CryptoImportAlgorithm
        { Name = algorithm, Hash = DigestAlgorithm.Sha256, NamedCurve = algorithm == "NODE-ED25519" ? "NODE-ED25519" : "P-256" }, true, [CryptoKeyUsage.Verify]);
        var binaryValid = await Crypto.Subtle.VerifyAsync(options, binaryKey, signature, data);
        return Response.Json(new { valid, invalid, importedValid, binaryValid, signature = TextCodec.EncodeHex(signature), jwk, algorithm = privateKey.Algorithm, publicExponent = privateKey.Algorithm.PublicExponent is { } exponent ? TextCodec.EncodeHex(exponent) : null, privateKey.Type, privateKey.Extractable });
    }

    private static async Task<Response> DeriveAsync(string algorithm)
    {
        CryptoKey key;
        var options = new CryptoDeriveAlgorithm { Name = algorithm, Hash = DigestAlgorithm.Sha256, Salt = TextCodec.EncodeUtf8("salt"), Info = TextCodec.EncodeUtf8("info"), Iterations = 2 };
        byte[]? peerSecret = null;
        if (algorithm == "ECDH" || algorithm == "X25519")
        {
            var parameters = new CryptoKeyPairAlgorithm { Name = algorithm, NamedCurve = "P-256" };
            var first = await Crypto.Subtle.GenerateKeyAsync(parameters, true, [CryptoKeyUsage.DeriveBits, CryptoKeyUsage.DeriveKey]);
            var second = await Crypto.Subtle.GenerateKeyAsync(parameters, true, [CryptoKeyUsage.DeriveBits, CryptoKeyUsage.DeriveKey]);
            key = first.PrivateKey;
            options = new CryptoDeriveAlgorithm { Name = algorithm, Public = second.PublicKey };
            peerSecret = await Crypto.Subtle.DeriveBitsAsync(new CryptoDeriveAlgorithm { Name = algorithm, Public = first.PublicKey }, second.PrivateKey);
        }
        else key = await Crypto.Subtle.ImportKeyAsync(CryptoBinaryKeyFormat.Raw, TextCodec.EncodeUtf8("password"), new CryptoAlgorithm { Name = algorithm }, false, [CryptoKeyUsage.DeriveBits, CryptoKeyUsage.DeriveKey]);
        var bits = await Crypto.Subtle.DeriveBitsAsync(options, key, 256);
        var derived = await Crypto.Subtle.DeriveKeyAsync(options, key, new CryptoImportAlgorithm { Name = "AES-GCM", Length = 256 }, true, [CryptoKeyUsage.Encrypt]);
        var exported = await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, derived);
        return Response.Json(new { bits = TextCodec.EncodeHex(bits), same = Crypto.TimingSafeEqual(bits, exported), peers = peerSecret is null || Crypto.TimingSafeEqual(bits, peerSecret), length = derived.Algorithm.Length });
    }

    private static async Task<Response> WrapAsync(string algorithm, CryptoKeyFormat format)
    {
        var key = await Crypto.Subtle.GenerateKeyAsync(new CryptoSecretKeyAlgorithm { Name = "AES-GCM", Length = 128 }, true, [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
        CryptoKey wrapping;
        CryptoKey unwrapping;
        if (algorithm == "RSA-OAEP")
        {
            var pair = await Crypto.Subtle.GenerateKeyAsync(new CryptoKeyPairAlgorithm { Name = algorithm, ModulusLength = 2048, PublicExponent = [1, 0, 1], Hash = DigestAlgorithm.Sha256 }, true, [CryptoKeyUsage.WrapKey, CryptoKeyUsage.UnwrapKey]);
            wrapping = pair.PublicKey;
            unwrapping = pair.PrivateKey;
        }
        else
        {
            wrapping = await Crypto.Subtle.GenerateKeyAsync(new CryptoSecretKeyAlgorithm { Name = algorithm, Length = 128 }, true, [CryptoKeyUsage.WrapKey, CryptoKeyUsage.UnwrapKey]);
            unwrapping = wrapping;
        }
        var options = new CryptoEncryptAlgorithm { Name = algorithm, Iv = new byte[16], Counter = new byte[16], Length = 64 };
        var wrapped = await Crypto.Subtle.WrapKeyAsync(format, key, wrapping, options);
        var unwrapped = await Crypto.Subtle.UnwrapKeyAsync(format, wrapped, unwrapping, options, new CryptoAlgorithm { Name = "AES-GCM" }, true, [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
        return Response.Json(new { same = Crypto.TimingSafeEqual(await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, key), await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, unwrapped)), unwrapped.Extractable, usages = unwrapped.Usages });
    }

    private static async Task<Response> KemAsync(string algorithm)
    {
        var pair = await Crypto.Subtle.GenerateKeyAsync(new CryptoKeyPairAlgorithm { Name = algorithm }, true, [CryptoKeyUsage.EncapsulateBits, CryptoKeyUsage.DecapsulateBits, CryptoKeyUsage.EncapsulateKey, CryptoKeyUsage.DecapsulateKey]);
        var parameters = new CryptoAlgorithm { Name = algorithm };
        var encapsulated = await Crypto.Subtle.EncapsulateBitsAsync(parameters, pair.PublicKey);
        var decapsulated = await Crypto.Subtle.DecapsulateBitsAsync(parameters, pair.PrivateKey, encapsulated.Ciphertext);
        var secretAlgorithm = new CryptoImportAlgorithm { Name = "AES-GCM", Length = 256 };
        var keys = await Crypto.Subtle.EncapsulateKeyAsync(parameters, pair.PublicKey, secretAlgorithm, true, [CryptoKeyUsage.Encrypt]);
        var shared = await Crypto.Subtle.DecapsulateKeyAsync(parameters, pair.PrivateKey, keys.Ciphertext, secretAlgorithm, true, [CryptoKeyUsage.Encrypt]);
        var publicKey = await Crypto.Subtle.GetPublicKeyAsync(pair.PrivateKey, [CryptoKeyUsage.EncapsulateBits]);
        var jwk = await Crypto.Subtle.ExportJwkAsync(pair.PublicKey);
        return Response.Json(new
        {
            sameBits = Crypto.TimingSafeEqual(encapsulated.SharedKey, decapsulated),
            sameKey = Crypto.TimingSafeEqual(await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, keys.SharedKey), await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, shared)),
            publicType = publicKey.Type, jwk,
            supported = SubtleCrypto.Supports("encapsulateBits", parameters),
            supportedKey = SubtleCrypto.Supports("encapsulateKey", parameters, secretAlgorithm),
            unsupported = SubtleCrypto.Supports("generateKey", new CryptoAlgorithm { Name = "ML-KEM-512" }),
            supportedDigest = SubtleCrypto.Supports("digest", DigestAlgorithm.Sha256)
        });
    }

    private static async Task<Response> StreamAsync(string algorithm, byte[] data)
    {
        var hash = algorithm switch
        {
            "MD5" => DigestStreamAlgorithm.Md5, "CRC32" => DigestStreamAlgorithm.Crc32,
            "CRC32C" => DigestStreamAlgorithm.Crc32C, "CRC64NVME" => DigestStreamAlgorithm.Crc64Nvme,
            _ => DigestStreamAlgorithm.Sha256
        };
        using var stream = Crypto.CreateDigestStream(hash);
        var writer = stream.GetWriter();
        await writer.WriteAsync(data);
        await writer.CloseAsync();
        var bytesWritten = stream.BytesWritten;
        var digest = await stream.Digest;
        return Response.Json(new { hex = TextCodec.EncodeHex(digest), bytesWritten, same = Crypto.TimingSafeEqual(digest, await stream.DigestAsync()) });
    }

    private static Response Random(Request request)
    {
        var count = int.Parse(request.QueryParameters.Get("count") ?? "16");
        var bytes = new byte[count];
        var filled = Crypto.FillRandomBytes(bytes);
        return Response.Json(new { count = filled.Length, same = Crypto.TimingSafeEqual(bytes, filled), sameReference = bytes == filled });
    }

    private static async Task<Response> ErrorsAsync(string scenario)
    {
        if (scenario == "timing") return Response.Json(Crypto.TimingSafeEqual(new byte[1], new byte[2]));
        var key = await Crypto.Subtle.GenerateKeyAsync(new CryptoSecretKeyAlgorithm { Name = "HMAC", Hash = DigestAlgorithm.Sha256 }, false, [CryptoKeyUsage.Verify]);
        if (scenario == "export") return Response.Json(await Crypto.Subtle.ExportJwkAsync(key));
        if (scenario == "family") return Response.Json(await Crypto.Subtle.GenerateKeyAsync(new CryptoSecretKeyAlgorithm { Name = "Ed25519" }, false, [CryptoKeyUsage.Sign]));
        return Response.Json(await Crypto.Subtle.SignAsync(new CryptoAlgorithm { Name = "HMAC" }, key, new byte[1]));
    }

    private static async Task<Response> FormatAsync(string algorithm, string format)
    {
        var binaryFormat = format switch
        {
            "spki" => CryptoBinaryKeyFormat.Spki,
            "raw-public" => CryptoBinaryKeyFormat.RawPublic,
            "raw-private" => CryptoBinaryKeyFormat.RawPrivate,
            "raw-seed" => CryptoBinaryKeyFormat.RawSeed,
            _ => CryptoBinaryKeyFormat.Pkcs8
        };
        var derive = algorithm == "X25519" || algorithm == "ECDH";
        var usages = derive ? new[] { CryptoKeyUsage.DeriveBits } : new[] { CryptoKeyUsage.Sign, CryptoKeyUsage.Verify };
        var pair = await Crypto.Subtle.GenerateKeyAsync(new CryptoKeyPairAlgorithm
        { Name = algorithm, NamedCurve = "P-384", Hash = DigestAlgorithm.Sha384, ModulusLength = 2048, PublicExponent = [1, 0, 1] }, true, usages);
        var isPublic = format == "spki" || format == "raw-public";
        var key = isPublic ? pair.PublicKey : pair.PrivateKey;
        var keyUsages = derive ? (isPublic ? new CryptoKeyUsage[0] : new[] { CryptoKeyUsage.DeriveBits })
            : (isPublic ? new[] { CryptoKeyUsage.Verify } : new[] { CryptoKeyUsage.Sign });
        var bytes = await Crypto.Subtle.ExportKeyAsync(binaryFormat, key);
        var imported = await Crypto.Subtle.ImportKeyAsync(binaryFormat, bytes, new CryptoImportAlgorithm
        { Name = algorithm, NamedCurve = "P-384", Hash = DigestAlgorithm.Sha384 }, true, keyUsages);
        return Response.Json(new { same = Crypto.TimingSafeEqual(bytes, await Crypto.Subtle.ExportKeyAsync(binaryFormat, imported)), imported.Type, usages = imported.Usages, supported = SubtleCrypto.Supports("generateKey", new CryptoKeyPairAlgorithm { Name = algorithm, NamedCurve = "P-384", Hash = DigestAlgorithm.Sha384, ModulusLength = 2048, PublicExponent = [1, 0, 1] }) });
    }

    private static async Task<Response> StreamTextAsync()
    {
        using var stream = Crypto.CreateDigestStream(DigestAlgorithm.Sha256, new DigestStreamOptions { ToWellFormed = true });
        var writer = stream.GetWriter();
        await writer.WriteAsync("\ud83d");
        await writer.WriteAsync("\ude00");
        await writer.WriteAsync("\ud800");
        await writer.CloseAsync();
        return Response.Json(new { hex = TextCodec.EncodeHex(await stream.Digest), count = stream.BytesWritten });
    }

    private static async Task<Response> StreamErrorAsync(string scenario)
    {
        var stream = Crypto.CreateDigestStream(DigestAlgorithm.Sha256);
        var rejected = RejectedAsync(stream.Digest);
        if (scenario == "dispose") stream.Dispose();
        else if (scenario == "writer") await stream.GetWriter().AbortAsync("cancelled");
        else await stream.AbortAsync("cancelled");
        return Response.Json(new { rejected = await rejected });
    }

    private static async Task<bool> RejectedAsync(Task<byte[]> pending)
    {
        try { await pending; return false; }
        catch { return true; }
    }
}
