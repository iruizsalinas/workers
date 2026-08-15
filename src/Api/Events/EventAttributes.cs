namespace Workers;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FetchAttribute : Attribute;
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScheduledAttribute : Attribute;
[AttributeUsage(AttributeTargets.Method)]
public sealed class QueueAttribute : Attribute;
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmailAttribute : Attribute;
[AttributeUsage(AttributeTargets.Method)]
public sealed class TailAttribute : Attribute;
[AttributeUsage(AttributeTargets.Interface)]
public sealed class RpcClientAttribute : Attribute;
