namespace Workers;

/// <summary>JavaScript Number and Math operations, including their NaN and rounding behavior.</summary>
public static class Numbers
{
    public static double From(object? value) => WorkerApi.NotExecutable<double>();
    public static double ParseInt(string text, int radix = 10) => WorkerApi.NotExecutable<double>();
    public static double ParseFloat(string text) => WorkerApi.NotExecutable<double>();
    public static bool IsNaN(double value) => WorkerApi.NotExecutable<bool>();
    public static bool IsFinite(double value) => WorkerApi.NotExecutable<bool>();
    public static bool IsInteger(double value) => WorkerApi.NotExecutable<bool>();
    public static bool IsSafeInteger(double value) => WorkerApi.NotExecutable<bool>();
    public static string ToFixed(double value, int digits = 0) => WorkerApi.NotExecutable<string>();
    public static string ToPrecision(double value, int digits) => WorkerApi.NotExecutable<string>();
    public static string ToRadixString(double value, int radix = 10) => WorkerApi.NotExecutable<string>();
    public static double Abs(double value) => WorkerApi.NotExecutable<double>();
    public static double Round(double value) => WorkerApi.NotExecutable<double>();
    public static double Floor(double value) => WorkerApi.NotExecutable<double>();
    public static double Ceil(double value) => WorkerApi.NotExecutable<double>();
    public static double Trunc(double value) => WorkerApi.NotExecutable<double>();
    public static double Min(double left, double right) => WorkerApi.NotExecutable<double>();
    public static double Max(double left, double right) => WorkerApi.NotExecutable<double>();
    public static double Pow(double value, double exponent) => WorkerApi.NotExecutable<double>();
    public static double Sqrt(double value) => WorkerApi.NotExecutable<double>();
    public static double Log(double value) => WorkerApi.NotExecutable<double>();
    public static double Exp(double value) => WorkerApi.NotExecutable<double>();
    public static double Sin(double value) => WorkerApi.NotExecutable<double>();
    public static double Cos(double value) => WorkerApi.NotExecutable<double>();
    public static double Tan(double value) => WorkerApi.NotExecutable<double>();
    public static double Sign(double value) => WorkerApi.NotExecutable<double>();
    public static double Random() => WorkerApi.NotExecutable<double>();
}
