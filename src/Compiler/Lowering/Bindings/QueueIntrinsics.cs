using static BindingIntrinsicRegistry;

internal static class
QueueIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.IQueueProducer", "SendJsonAsync")] = new("json", BindingIntrinsicKind.QueueSend),
            [Key("Workers.IQueueProducer", "SendTextAsync")] = new("text", BindingIntrinsicKind.QueueSend),
            [Key("Workers.IQueueProducer", "SendBytesAsync")] = new("bytes", BindingIntrinsicKind.QueueSend),
            [Key("Workers.IQueueProducer", "SendJsonBatchAsync")] = new("json", BindingIntrinsicKind.QueueSendBatch),
            [Key("Workers.IQueueProducer", "SendTextBatchAsync")] = new("text", BindingIntrinsicKind.QueueSendBatch),
            [Key("Workers.IQueueProducer", "SendBytesBatchAsync")] = new("bytes", BindingIntrinsicKind.QueueSendBatch),
            [Key("Workers.IQueueProducer", "SendBatchAsync")] = Direct("sendBatch"),
            [Key("Workers.IQueueProducer", "MetricsAsync")] = Direct("metrics"),
            [Key("Workers.QueueSendRequest", "Json")] = new("json", BindingIntrinsicKind.QueueRequest),
            [Key("Workers.QueueSendRequest", "Text")] = new("text", BindingIntrinsicKind.QueueRequest),
            [Key("Workers.QueueSendRequest", "Bytes")] = new("bytes", BindingIntrinsicKind.QueueRequest),

        };
}
