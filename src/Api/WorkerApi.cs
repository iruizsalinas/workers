namespace Workers;

internal static class WorkerApi
{
    internal static T NotExecutable<T>() => throw CreateException();

    internal static void NotExecutable() => throw CreateException();

    private static PlatformNotSupportedException CreateException() =>
        new("Workers APIs are compile-time declarations and cannot execute on the .NET runtime.");
}
