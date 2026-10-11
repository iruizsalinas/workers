namespace Workers;

/// <summary>The module's <c>fetch</c> handler: <c>(Request, Env, ExecutionContext)</c> returning a <see cref="Response"/>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FetchAttribute : Attribute;

/// <summary>The module's <c>scheduled</c> handler: <c>(ScheduledController, Env, ExecutionContext)</c>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScheduledAttribute : Attribute;

/// <summary>The module's <c>queue</c> handler: <c>(MessageBatch&lt;T&gt;, Env, ExecutionContext)</c>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class QueueAttribute : Attribute;

/// <summary>The module's <c>email</c> handler: <c>(ForwardableEmailMessage, Env, ExecutionContext)</c>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmailAttribute : Attribute;

/// <summary>The module's <c>tail</c> handler: <c>(IReadOnlyList&lt;TraceItem&gt;, Env, ExecutionContext)</c>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TailAttribute : Attribute;
