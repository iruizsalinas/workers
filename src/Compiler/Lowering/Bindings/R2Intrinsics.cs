using static BindingIntrinsicRegistry;

internal static class R2Intrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.R2Bucket", "HeadAsync")] = Direct("head"),
            [Key("Workers.R2Bucket", "GetAsync")] = new("get", BindingIntrinsicKind.R2Get),
            [Key("Workers.R2Bucket", "PutAsync")] = Direct("put"),
            [Key("Workers.R2Bucket", "DeleteAsync")] = Direct("delete"),
            [Key("Workers.R2Bucket", "ListAsync")] = Direct("list"),
            [Key("Workers.R2Bucket", "CreateMultipartUploadAsync")] = Direct("createMultipartUpload"),
            [Key("Workers.R2Bucket", "ResumeMultipartUpload")] = Direct("resumeMultipartUpload"),
            [Key("Workers.R2Object", "WriteHttpMetadata")] = Direct("writeHttpMetadata"),
            [Key("Workers.R2ObjectBody", "TextAsync")] = Direct("text"),
            [Key("Workers.R2ObjectBody", "BytesAsync")] = Direct("bytes"),
            [Key("Workers.R2ObjectBody", "JsonAsync")] = Direct("json"),
            [Key("Workers.R2ObjectBody", "BlobAsync")] = Direct("blob"),
            [Key("Workers.R2MultipartUpload", "UploadPartAsync")] = Direct("uploadPart"),
            [Key("Workers.R2MultipartUpload", "AbortAsync")] = Direct("abort"),
            [Key("Workers.R2MultipartUpload", "CompleteAsync")] = Direct("complete")
        };
}
