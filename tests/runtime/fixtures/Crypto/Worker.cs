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

    private static AesAlgorithm Aes(string algorithm) => algorithm switch
    {
        "AES-CBC" => AesAlgorithm.AesCbc,
        "AES-CTR" => AesAlgorithm.AesCtr,
        "AES-KW" => AesAlgorithm.AesKw,
        _ => AesAlgorithm.AesGcm
    };

    private static RsaAlgorithm Rsa(string algorithm) => algorithm switch
    {
        "RSA-PSS" => RsaAlgorithm.RsaPss,
        "RSA-OAEP" => RsaAlgorithm.RsaOaep,
        _ => RsaAlgorithm.RsassaPkcs1V15
    };

    private static MlDsaAlgorithm MlDsa(string algorithm) => algorithm switch
    {
        "ML-DSA-65" => MlDsaAlgorithm.MlDsa65,
        "ML-DSA-87" => MlDsaAlgorithm.MlDsa87,
        _ => MlDsaAlgorithm.MlDsa44
    };

    private static IKeyPairGenerationAlgorithm PairParameters(string algorithm, DigestAlgorithm hash, EcNamedCurve curve) => algorithm switch
    {
        "RSASSA-PKCS1-v1_5" or "RSA-PSS" or "RSA-OAEP" => new RsaKeyGenParams { Name = Rsa(algorithm), Hash = hash, ModulusLength = 2048, PublicExponent = [1, 0, 1] },
        "ECDSA" => new EcKeyParams { Name = EcAlgorithm.Ecdsa, NamedCurve = curve },
        "ECDH" => new EcKeyParams { Name = EcAlgorithm.Ecdh, NamedCurve = curve },
        "Ed25519" => new Ed25519Params(),
        "NODE-ED25519" => new NodeEd25519Params(),
        "X25519" => new X25519Params(),
        _ => new MlDsaParams { Name = MlDsa(algorithm) }
    };

    private static IKeyImportAlgorithm ImportParameters(string algorithm, DigestAlgorithm hash, EcNamedCurve curve) => algorithm switch
    {
        "HMAC" => new HmacKeyParams { Hash = hash },
        "AES-GCM" or "AES-CBC" or "AES-CTR" or "AES-KW" => new AesKeyParams { Name = Aes(algorithm) },
        "RSASSA-PKCS1-v1_5" or "RSA-PSS" or "RSA-OAEP" => new RsaKeyImportParams { Name = Rsa(algorithm), Hash = hash },
        "ECDSA" => new EcKeyParams { Name = EcAlgorithm.Ecdsa, NamedCurve = curve },
        "ECDH" => new EcKeyParams { Name = EcAlgorithm.Ecdh, NamedCurve = curve },
        "Ed25519" => new Ed25519Params(),
        "NODE-ED25519" => new NodeEd25519Params(),
        "X25519" => new X25519Params(),
        _ => new MlDsaParams { Name = MlDsa(algorithm) }
    };

    private static ISignatureAlgorithm SignParameters(string algorithm, byte[]? context) => algorithm switch
    {
        "HMAC" => new HmacParams(),
        "RSASSA-PKCS1-v1_5" => new RsaPkcs1Params(),
        "RSA-PSS" => new RsaPssParams { SaltLength = 32 },
        "ECDSA" => new EcdsaParams { Hash = DigestAlgorithm.Sha256 },
        "Ed25519" => new Ed25519Params(),
        "NODE-ED25519" => new NodeEd25519Params(),
        _ => new MlDsaParams { Name = MlDsa(algorithm), Context = context }
    };

    private static IKeyWrapAlgorithm WrapParameters(string algorithm) => algorithm switch
    {
        "AES-GCM" => new AesGcmParams { Iv = new byte[16] },
        "AES-CBC" => new AesCbcParams { Iv = new byte[16] },
        "AES-CTR" => new AesCtrParams { Counter = new byte[16], Length = 64 },
        "AES-KW" => new AesKwParams(),
        _ => new RsaOaepParams()
    };

    private static async Task<Response> EncryptAsync(string algorithm, byte[] data)
    {
        CryptoKey encryptKey;
        CryptoKey decryptKey;
        if (algorithm == "RSA-OAEP")
        {
            var pair = await Crypto.Subtle.GenerateKeyAsync(PairParameters(algorithm, DigestAlgorithm.Sha256, EcNamedCurve.P256), true,
                [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
            encryptKey = pair.PublicKey;
            decryptKey = pair.PrivateKey;
        }
        else
        {
            encryptKey = await Crypto.Subtle.GenerateKeyAsync(new AesKeyParams { Name = Aes(algorithm), Length = 128 }, true,
                [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
            decryptKey = encryptKey;
        }
        IEncryptionAlgorithm options = algorithm switch
        {
            "AES-GCM" => new AesGcmParams { Iv = Crypto.RandomBytes(16), AdditionalData = [1, 2], TagLength = 128 },
            "AES-CBC" => new AesCbcParams { Iv = Crypto.RandomBytes(16) },
            "AES-CTR" => new AesCtrParams { Counter = new byte[16], Length = 64 },
            _ => new RsaOaepParams { Label = [3] }
        };
        var encrypted = await Crypto.Subtle.EncryptAsync(options, encryptKey, data);
        var decrypted = await Crypto.Subtle.DecryptAsync(options, decryptKey, encrypted);
        var jwk = await Crypto.Subtle.ExportJwkAsync(decryptKey);
        var imported = await Crypto.Subtle.ImportKeyAsync(jwk, ImportParameters(algorithm, DigestAlgorithm.Sha256, EcNamedCurve.P256), true, [CryptoKeyUsage.Decrypt]);
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
                new HmacKeyParams { Hash = DigestAlgorithm.Sha256 }, true, [CryptoKeyUsage.Sign, CryptoKeyUsage.Verify]);
            publicKey = privateKey;
        }
        else
        {
            var pair = await Crypto.Subtle.GenerateKeyAsync(PairParameters(algorithm, DigestAlgorithm.Sha256, EcNamedCurve.P256), true,
                [CryptoKeyUsage.Sign, CryptoKeyUsage.Verify]);
            privateKey = pair.PrivateKey;
            publicKey = pair.PublicKey;
        }
        var options = SignParameters(algorithm, context ? new byte[] { 1, 2, 3 } : null);
        var signature = await Crypto.Subtle.SignAsync(options, privateKey, data);
        var valid = await Crypto.Subtle.VerifyAsync(options, publicKey, signature, data);
        var invalid = await Crypto.Subtle.VerifyAsync(options, publicKey, signature, TextCodec.EncodeUtf8("tampered"));
        var jwk = await Crypto.Subtle.ExportJwkAsync(publicKey);
        var importParameters = ImportParameters(algorithm, DigestAlgorithm.Sha256, EcNamedCurve.P256);
        var imported = await Crypto.Subtle.ImportKeyAsync(jwk, importParameters, true, [CryptoKeyUsage.Verify]);
        var importedValid = await Crypto.Subtle.VerifyAsync(options, imported, signature, data);
        var format = algorithm == "HMAC" ? CryptoBinaryKeyFormat.Raw : CryptoBinaryKeyFormat.Spki;
        var binary = await Crypto.Subtle.ExportKeyAsync(format, publicKey);
        var binaryKey = await Crypto.Subtle.ImportKeyAsync(format, binary, importParameters, true, [CryptoKeyUsage.Verify]);
        var binaryValid = await Crypto.Subtle.VerifyAsync(options, binaryKey, signature, data);
        var hashed = algorithm == "HMAC" || algorithm.StartsWith("RSA");
        return Response.Json(new
        {
            valid, invalid, importedValid, binaryValid, signature = TextCodec.EncodeHex(signature), jwk, algorithm = privateKey.Algorithm,
            publicExponent = privateKey.Algorithm.PublicExponent is { } exponent ? TextCodec.EncodeHex(exponent) : null,
            privateKey.Type, privateKey.Extractable,
            hashMatches = hashed ? privateKey.Algorithm.Hash == DigestAlgorithm.Sha256 : privateKey.Algorithm.Hash is null
        });
    }

    private static async Task<Response> DeriveAsync(string algorithm)
    {
        CryptoKey key;
        IKeyDerivationAlgorithm options;
        byte[]? peerSecret = null;
        if (algorithm == "ECDH" || algorithm == "X25519")
        {
            var name = algorithm == "ECDH" ? KeyAgreementAlgorithm.Ecdh : KeyAgreementAlgorithm.X25519;
            var parameters = PairParameters(algorithm, DigestAlgorithm.Sha256, EcNamedCurve.P256);
            var first = await Crypto.Subtle.GenerateKeyAsync(parameters, true, [CryptoKeyUsage.DeriveBits, CryptoKeyUsage.DeriveKey]);
            var second = await Crypto.Subtle.GenerateKeyAsync(parameters, true, [CryptoKeyUsage.DeriveBits, CryptoKeyUsage.DeriveKey]);
            key = first.PrivateKey;
            options = new EcdhKeyDeriveParams { Name = name, Public = second.PublicKey };
            peerSecret = await Crypto.Subtle.DeriveBitsAsync(new EcdhKeyDeriveParams { Name = name, Public = first.PublicKey }, second.PrivateKey);
        }
        else
        {
            var salt = TextCodec.EncodeUtf8("salt");
            IKeyImportAlgorithm baseKey = algorithm == "HKDF" ? new HkdfKeyParams() : new Pbkdf2KeyParams();
            options = algorithm == "HKDF"
                ? new HkdfParams { Hash = DigestAlgorithm.Sha256, Salt = salt, Info = TextCodec.EncodeUtf8("info") }
                : new Pbkdf2Params { Hash = DigestAlgorithm.Sha256, Salt = salt, Iterations = 2 };
            key = await Crypto.Subtle.ImportKeyAsync(CryptoBinaryKeyFormat.Raw, TextCodec.EncodeUtf8("password"), baseKey, false, [CryptoKeyUsage.DeriveBits, CryptoKeyUsage.DeriveKey]);
        }
        var bits = await Crypto.Subtle.DeriveBitsAsync(options, key, 256);
        var derived = await Crypto.Subtle.DeriveKeyAsync(options, key, new AesKeyParams { Name = AesAlgorithm.AesGcm, Length = 256 }, true, [CryptoKeyUsage.Encrypt]);
        var exported = await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, derived);
        return Response.Json(new { bits = TextCodec.EncodeHex(bits), same = Crypto.TimingSafeEqual(bits, exported), peers = peerSecret is null || Crypto.TimingSafeEqual(bits, peerSecret), length = derived.Algorithm.Length });
    }

    private static async Task<Response> WrapAsync(string algorithm, CryptoKeyFormat format)
    {
        var key = await Crypto.Subtle.GenerateKeyAsync(new AesKeyParams { Name = AesAlgorithm.AesGcm, Length = 128 }, true, [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
        CryptoKey wrapping;
        CryptoKey unwrapping;
        if (algorithm == "RSA-OAEP")
        {
            var pair = await Crypto.Subtle.GenerateKeyAsync(PairParameters(algorithm, DigestAlgorithm.Sha256, EcNamedCurve.P256), true, [CryptoKeyUsage.WrapKey, CryptoKeyUsage.UnwrapKey]);
            wrapping = pair.PublicKey;
            unwrapping = pair.PrivateKey;
        }
        else
        {
            wrapping = await Crypto.Subtle.GenerateKeyAsync(new AesKeyParams { Name = Aes(algorithm), Length = 128 }, true, [CryptoKeyUsage.WrapKey, CryptoKeyUsage.UnwrapKey]);
            unwrapping = wrapping;
        }
        var options = WrapParameters(algorithm);
        var wrapped = await Crypto.Subtle.WrapKeyAsync(format, key, wrapping, options);
        var unwrapped = await Crypto.Subtle.UnwrapKeyAsync(format, wrapped, unwrapping, options, new AesKeyParams { Name = AesAlgorithm.AesGcm }, true, [CryptoKeyUsage.Encrypt, CryptoKeyUsage.Decrypt]);
        return Response.Json(new { same = Crypto.TimingSafeEqual(await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, key), await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, unwrapped)), unwrapped.Extractable, usages = unwrapped.Usages });
    }

    private static async Task<Response> KemAsync(string algorithm)
    {
        var parameters = new MlKemParams { Name = algorithm == "ML-KEM-1024" ? MlKemAlgorithm.MlKem1024 : MlKemAlgorithm.MlKem768 };
        var pair = await Crypto.Subtle.GenerateKeyAsync(parameters, true, [CryptoKeyUsage.EncapsulateBits, CryptoKeyUsage.DecapsulateBits, CryptoKeyUsage.EncapsulateKey, CryptoKeyUsage.DecapsulateKey]);
        var encapsulated = await Crypto.Subtle.EncapsulateBitsAsync(parameters, pair.PublicKey);
        var decapsulated = await Crypto.Subtle.DecapsulateBitsAsync(parameters, pair.PrivateKey, encapsulated.Ciphertext);
        var secretAlgorithm = new AesKeyParams { Name = AesAlgorithm.AesGcm, Length = 256 };
        var keys = await Crypto.Subtle.EncapsulateKeyAsync(parameters, pair.PublicKey, secretAlgorithm, true, [CryptoKeyUsage.Encrypt]);
        var shared = await Crypto.Subtle.DecapsulateKeyAsync(parameters, pair.PrivateKey, keys.Ciphertext, secretAlgorithm, true, [CryptoKeyUsage.Encrypt]);
        var publicKey = await Crypto.Subtle.GetPublicKeyAsync(pair.PrivateKey, [CryptoKeyUsage.EncapsulateBits]);
        var jwk = await Crypto.Subtle.ExportJwkAsync(pair.PublicKey);
        return Response.Json(new
        {
            sameBits = Crypto.TimingSafeEqual(encapsulated.SharedKey, decapsulated),
            sameKey = Crypto.TimingSafeEqual(await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, keys.SharedKey), await Crypto.Subtle.ExportKeyAsync(CryptoBinaryKeyFormat.Raw, shared)),
            publicType = publicKey.Type, isPublic = publicKey.Type == CryptoKeyType.Public, jwk,
            supported = SubtleCrypto.Supports(CryptoOperation.EncapsulateBits, parameters),
            supportedKey = SubtleCrypto.Supports(CryptoOperation.EncapsulateKey, parameters, secretAlgorithm),
            unsupported = SubtleCrypto.Supports(CryptoOperation.Encrypt, parameters),
            supportedDigest = SubtleCrypto.Supports(CryptoOperation.Digest, DigestAlgorithm.Sha256)
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
        var digest = await stream.DigestAsync();
        return Response.Json(new { hex = TextCodec.EncodeHex(digest), bytesWritten, same = Crypto.TimingSafeEqual(digest, await stream.DigestAsync()) });
    }

    private static Response Random(Request request)
    {
        var count = int.Parse(request.QueryParameters.Get("count") ?? "16");
        var bytes = new byte[count];
        var filled = Crypto.GetRandomValues(bytes);
        return Response.Json(new { count = filled.Length, same = Crypto.TimingSafeEqual(bytes, filled), sameReference = bytes == filled });
    }

    private static async Task<Response> ErrorsAsync(string scenario)
    {
        if (scenario == "timing") return Response.Json(Crypto.TimingSafeEqual(new byte[1], new byte[2]));
        var key = await Crypto.Subtle.GenerateKeyAsync(new HmacKeyParams { Hash = DigestAlgorithm.Sha256 }, false, [CryptoKeyUsage.Verify]);
        if (scenario == "export") return Response.Json(await Crypto.Subtle.ExportJwkAsync(key));
        if (scenario == "iv")
        {
            var aes = await Crypto.Subtle.GenerateKeyAsync(new AesKeyParams { Name = AesAlgorithm.AesGcm, Length = 128 }, false, [CryptoKeyUsage.Encrypt]);
            return Response.Json(await Crypto.Subtle.EncryptAsync(new AesGcmParams { Iv = [] }, aes, new byte[1]));
        }
        return Response.Json(await Crypto.Subtle.SignAsync(new HmacParams(), key, new byte[1]));
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
        var parameters = PairParameters(algorithm, DigestAlgorithm.Sha384, EcNamedCurve.P384);
        var pair = await Crypto.Subtle.GenerateKeyAsync(parameters, true, usages);
        var isPublic = format == "spki" || format == "raw-public";
        var key = isPublic ? pair.PublicKey : pair.PrivateKey;
        var keyUsages = derive ? (isPublic ? new CryptoKeyUsage[0] : new[] { CryptoKeyUsage.DeriveBits })
            : (isPublic ? new[] { CryptoKeyUsage.Verify } : new[] { CryptoKeyUsage.Sign });
        var bytes = await Crypto.Subtle.ExportKeyAsync(binaryFormat, key);
        var imported = await Crypto.Subtle.ImportKeyAsync(binaryFormat, bytes, ImportParameters(algorithm, DigestAlgorithm.Sha384, EcNamedCurve.P384), true, keyUsages);
        return Response.Json(new { same = Crypto.TimingSafeEqual(bytes, await Crypto.Subtle.ExportKeyAsync(binaryFormat, imported)), imported.Type, usages = imported.Usages, supported = SubtleCrypto.Supports(CryptoOperation.GenerateKey, parameters) });
    }

    private static async Task<Response> StreamTextAsync()
    {
        using var stream = Crypto.CreateDigestStream(DigestStreamAlgorithm.Sha256, new DigestStreamOptions { ToWellFormed = true });
        var writer = stream.GetWriter();
        await writer.WriteAsync("\ud83d");
        await writer.WriteAsync("\ude00");
        await writer.WriteAsync("\ud800");
        await writer.CloseAsync();
        return Response.Json(new { hex = TextCodec.EncodeHex(await stream.DigestAsync()), count = stream.BytesWritten });
    }

    private static async Task<Response> StreamErrorAsync(string scenario)
    {
        var stream = Crypto.CreateDigestStream(DigestStreamAlgorithm.Sha256);
        var rejected = RejectedAsync(stream.DigestAsync());
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
