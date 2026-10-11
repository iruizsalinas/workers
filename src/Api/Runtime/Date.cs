namespace Workers;

/// <summary>A JavaScript Date. Timestamps are milliseconds since the Unix epoch; months are zero-based.</summary>
public sealed class Date
{
    public Date() => WorkerApi.NotExecutable();
    public Date(double milliseconds) => WorkerApi.NotExecutable();
    public Date(string text) => WorkerApi.NotExecutable();
    public Date(Date value) => WorkerApi.NotExecutable();
    public static double Now() => WorkerApi.NotExecutable<double>();
    public static double Parse(string text) => WorkerApi.NotExecutable<double>();
    public static double Utc(int year, int month, int day = 1, int hours = 0, int minutes = 0, int seconds = 0, int milliseconds = 0) => WorkerApi.NotExecutable<double>();
    public double GetTime() => WorkerApi.NotExecutable<double>();
    public double GetUtcFullYear() => WorkerApi.NotExecutable<double>();
    public double GetUtcMonth() => WorkerApi.NotExecutable<double>();
    public double GetUtcDate() => WorkerApi.NotExecutable<double>();
    public double GetUtcDay() => WorkerApi.NotExecutable<double>();
    public double GetUtcHours() => WorkerApi.NotExecutable<double>();
    public double GetUtcMinutes() => WorkerApi.NotExecutable<double>();
    public double GetUtcSeconds() => WorkerApi.NotExecutable<double>();
    public double GetUtcMilliseconds() => WorkerApi.NotExecutable<double>();
    public double SetTime(double milliseconds) => WorkerApi.NotExecutable<double>();
    public double SetUtcFullYear(int year) => WorkerApi.NotExecutable<double>();
    public double SetUtcMonth(int month) => WorkerApi.NotExecutable<double>();
    public double SetUtcDate(int day) => WorkerApi.NotExecutable<double>();
    public double SetUtcHours(int hours) => WorkerApi.NotExecutable<double>();
    public double SetUtcMinutes(int minutes) => WorkerApi.NotExecutable<double>();
    public double SetUtcSeconds(int seconds) => WorkerApi.NotExecutable<double>();
    public double SetUtcMilliseconds(int milliseconds) => WorkerApi.NotExecutable<double>();
    public string ToIsoString() => WorkerApi.NotExecutable<string>();
    public string ToUtcString() => WorkerApi.NotExecutable<string>();
    public string? ToJson() => WorkerApi.NotExecutable<string?>();
    public override string ToString() => WorkerApi.NotExecutable<string>();
}
