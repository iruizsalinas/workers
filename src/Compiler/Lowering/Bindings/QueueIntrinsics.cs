using static BindingIntrinsicRegistry;

internal static class QueueIntrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.Queue", "SendAsync")] = Direct("send"),
            [Key("Workers.Queue", "SendBatchAsync")] = Direct("sendBatch"),
            [Key("Workers.Queue", "MetricsAsync")] = Direct("metrics"),
            [Key("Workers.MessageBatch<T>", "AckAll")] = Direct("ackAll"),
            [Key("Workers.MessageBatch<T>", "RetryAll")] = Direct("retryAll"),
            [Key("Workers.Message<T>", "Ack")] = Direct("ack"),
            [Key("Workers.Message<T>", "Retry")] = Direct("retry")
        };
}
