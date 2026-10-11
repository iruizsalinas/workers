namespace Workers;

public static class Log
{
    public static void Info(object? value) => WorkerApi.NotExecutable();
    public static void Debug(object? value) => WorkerApi.NotExecutable();
    public static void Warn(object? value) => WorkerApi.NotExecutable();
    public static void Error(object? value) => WorkerApi.NotExecutable();
}
