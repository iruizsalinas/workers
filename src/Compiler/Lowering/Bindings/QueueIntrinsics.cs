using static BindingIntrinsicRegistry;

internal static class
QueueIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.IQueueProducer", "SendJsonAsync")] = Direct("send"),
            [Key("Workers.IQueueProducer", "SendTextAsync")] = Direct("send"),
            [Key("Workers.IQueueProducer", "SendBytesAsync")] = Direct("send"),
            [Key("Workers.IQueueProducer", "SendJsonBatchAsync")] = Direct("sendBatch"),
            [Key("Workers.IQueueProducer", "SendTextBatchAsync")] = Direct("sendBatch"),
            [Key("Workers.IQueueProducer", "SendBytesBatchAsync")] = Direct("sendBatch"),
            [Key("Workers.IQueueProducer", "SendBatchAsync")] = Direct("sendBatch"),
            [Key("Workers.IQueueProducer", "MetricsAsync")] = Direct("metrics"),

        };
}
