using static BindingIntrinsicRegistry;

internal static class
R2Intrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.IR2Bucket", "HeadAsync")] = Direct("head"),
            [Key("Workers.IR2Bucket", "GetAsync")] = Direct("get"),
            [Key("Workers.IR2Bucket", "GetObjectAsync")] = Direct("get"),
            [Key("Workers.IR2Bucket", "PutAsync")] = Direct("put"),
            [Key("Workers.IR2Bucket", "PutObjectAsync")] = Direct("put"),
            [Key("Workers.R2ObjectBody", "WriteHttpMetadata")] = Direct("writeHttpMetadata"),
            [Key("Workers.IR2Bucket", "CreateMultipartUploadAsync")] = Direct("createMultipartUpload"),
            [Key("Workers.IR2Bucket", "ResumeMultipartUpload")] = Direct("resumeMultipartUpload"),
            [Key("Workers.IR2Bucket", "DeleteAsync")] = Direct("delete"),
            [Key("Workers.IR2Bucket", "ListAsync")] = Direct("list"),

        };
}
