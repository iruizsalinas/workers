using static BindingIntrinsicRegistry;

internal static class PlatformIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.RateLimit", "LimitAsync")] = Direct("limit"),
            [Key("Workers.AnalyticsEngineDataset", "WriteDataPoint")] = Direct("writeDataPoint"),
            [Key("Workers.SendEmail", "SendAsync")] = Direct("send"),
            [Key("Workers.ForwardableEmailMessage", "SetReject")] = Direct("setReject"),
            [Key("Workers.ForwardableEmailMessage", "ForwardAsync")] = Direct("forward"),
            [Key("Workers.ForwardableEmailMessage", "ReplyAsync")] = Direct("reply"),
            [Key("Workers.Ai", "RunAsync")] = Direct("run"),
            [Key("Workers.Ai", "RunStreamAsync")] = Direct("run"),
            [Key("Workers.Workflow", "GetAsync")] = Direct("get"),
            [Key("Workers.Workflow", "CreateAsync")] = Direct("create"),
            [Key("Workers.Workflow", "CreateBatchAsync")] = Direct("createBatch"),
            [Key("Workers.WorkflowInstance", "StatusAsync")] = Direct("status"),
            [Key("Workers.WorkflowInstance", "PauseAsync")] = Direct("pause"),
            [Key("Workers.WorkflowInstance", "ResumeAsync")] = Direct("resume"),
            [Key("Workers.WorkflowInstance", "TerminateAsync")] = Direct("terminate"),
            [Key("Workers.WorkflowInstance", "RestartAsync")] = Direct("restart"),
            [Key("Workers.WorkflowInstance", "SendEventAsync")] = Direct("sendEvent"),
            [Key("Workers.ImagesBinding", "InfoAsync")] = Direct("info"),
            [Key("Workers.ImagesBinding", "Input")] = Direct("input"),
            [Key("Workers.ImageTransformer", "Transform")] = Direct("transform"),
            [Key("Workers.ImageTransformer", "Draw")] = Direct("draw"),
            [Key("Workers.ImageTransformer", "OutputAsync")] = Direct("output"),
            [Key("Workers.ImageTransformationResult", "Response")] = Direct("response"),
            [Key("Workers.ImageTransformationResult", "ContentType")] = Direct("contentType"),
            [Key("Workers.ImageTransformationResult", "Image")] = Direct("image"),
            [Key("Workers.MediaBinding", "Input")] = Direct("input"),
            [Key("Workers.MediaTransformer", "Transform")] = Direct("transform"),
            [Key("Workers.MediaTransformer", "Output")] = Direct("output"),
            [Key("Workers.MediaTransformationGenerator", "Output")] = Direct("output"),
            [Key("Workers.MediaTransformationResult", "MediaAsync")] = Direct("media"),
            [Key("Workers.MediaTransformationResult", "ResponseAsync")] = Direct("response"),
            [Key("Workers.MediaTransformationResult", "ContentTypeAsync")] = Direct("contentType"),
            [Key("Workers.Vectorize", "DescribeAsync")] = Direct("describe"),
            [Key("Workers.Vectorize", "QueryAsync")] = Direct("query"),
            [Key("Workers.Vectorize", "QueryByIdAsync")] = Direct("queryById"),
            [Key("Workers.Vectorize", "InsertAsync")] = Direct("insert"),
            [Key("Workers.Vectorize", "UpsertAsync")] = Direct("upsert"),
            [Key("Workers.Vectorize", "DeleteByIdsAsync")] = Direct("deleteByIds"),
            [Key("Workers.Vectorize", "GetByIdsAsync")] = Direct("getByIds"),
            [Key("Workers.SecretsStoreSecret", "GetAsync")] = Direct("get"),
            [Key("Workers.Hyperdrive", "Connect")] = Direct("connect")
        };
}
