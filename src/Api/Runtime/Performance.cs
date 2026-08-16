namespace Workers;

public static class Performance
{
    public static double Now() => WorkerApi.NotExecutable<double>();
}
