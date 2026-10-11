using Workers;

namespace EventWrappers;

public static class Worker
{
    [Email]
    public static async Task EmailAsync(
        ForwardableEmailMessage message,
        Env environment,
        ExecutionContext context)
    {
        await message.ForwardAsync("archive@example.test");
    }

    [Tail]
    public static async Task TailAsync(
        IReadOnlyList<TraceItem> events,
        Env environment,
        ExecutionContext context)
    {
        await environment.Service("TAIL_SINK").InvokeAsync("record", events);
    }
}
