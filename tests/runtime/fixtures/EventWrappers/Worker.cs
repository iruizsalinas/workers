using Workers;

namespace EventWrappers;

public static class Worker
{
    [Email]
    public static async Task EmailAsync(
        ForwardableEmailMessage message,
        Env environment,
        Context context)
    {
        await message.ForwardAsync("archive@example.test");
    }

    [Tail]
    public static async Task TailAsync(
        TailEvent events,
        Env environment,
        Context context)
    {
        await environment.Service("TAIL_SINK").InvokeVoidAsync("record", [events]);
    }
}
