using System.Globalization;
using System.Text;

// Typical invoice, scheduler and configuration requirements are run on the CLR and Workers.
public static class ApplicationLanguageScenarios
{
    public static Dictionary<string, string> Run() => new()
    {
        ["invoiceFormats"] = InvoiceFormats(),
        ["invoiceRounding"] = InvoiceRounding(),
        ["measurementFormatting"] = MeasurementFormatting(),
        ["singlePrecisionRounding"] = SinglePrecisionRounding(),
        ["configurationNumbers"] = ConfigurationNumbers(),
        ["configurationFlags"] = ConfigurationFlags(),
        ["billingCalendar"] = BillingCalendar(),
        ["policyText"] = PolicyText()
    };

    private static string InvoiceFormats()
    {
        var output = new StringBuilder();
        foreach (var amount in new double[] { 2.675, 2.685, 1.005, 1.015, -2.675, 0.125, 0.375, -0.004, 1e21, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            try
            {
                output.Append(amount.ToString("F2", CultureInfo.InvariantCulture)).Append('|')
                    .Append(amount.ToString("N2", CultureInfo.InvariantCulture)).Append('|')
                    .Append(amount.ToString("P1", CultureInfo.InvariantCulture)).Append(';');
            }
            catch (Exception) { output.Append("error;"); }
        }
        foreach (var amount in new float[] { 1.005f, 2.675f, 0.1f, float.NaN, float.PositiveInfinity })
        {
            try
            {
                output.Append(amount.ToString("F2", CultureInfo.InvariantCulture)).Append('|')
                    .Append(amount.ToString("N2", CultureInfo.InvariantCulture)).Append('|')
                    .Append(amount.ToString("P1", CultureInfo.InvariantCulture)).Append(';');
            }
            catch (Exception) { output.Append("error;"); }
        }
        return output.ToString();
    }

    private static string InvoiceRounding()
    {
        var output = new StringBuilder();
        foreach (var amount in new double[] { 2.675, 2.685, 1.005, -2.675, 1e16, 1.2345678901234567e17, 9999999999999998d, double.NaN, double.PositiveInfinity })
        {
            var line = Math.Round(amount, 2).ToString(CultureInfo.InvariantCulture) + "|"
                + Math.Round(amount, 15).ToString(CultureInfo.InvariantCulture) + ";";
            output.Append(line);
        }
        return output.ToString();
    }

    private static string ConfigurationNumbers()
    {
        var output = new StringBuilder();
        foreach (var text in new[] { "Infinity", "infinity", "+Infinity", "-infinity", "NaN", "nan", "1e9999", "1,000.5", " 42 ", "\u00a042\u00a0", "42\0", "1,,2", "1,2,", "42 \0", "42\0 ", "+nan", "-NaN", "\0", "true\0", "\u00a0NaN\u00a0", "\u00a0Infinity\u00a0", "Infinity\0" })
        {
            var valid = double.TryParse(text, CultureInfo.InvariantCulture, out var value);
            var validInteger = int.TryParse(text, CultureInfo.InvariantCulture, out var number);
            var line = $"{valid}:" + value.ToString(CultureInfo.InvariantCulture) + $":{validInteger}:{number};";
            output.Append(line);
        }
        return output.ToString();
    }

    private static string MeasurementFormatting()
    {
        var output = new StringBuilder();
        foreach (var amount in new[] { double.Epsilon, double.MaxValue, -double.Epsilon, -0.0, 1e-22, 1e23, 0.1, 0.05, 12.125, -12.125 })
        {
            output.Append(amount.ToString("F20", CultureInfo.InvariantCulture)).Append('|')
                .Append(amount.ToString("N0", CultureInfo.InvariantCulture)).Append('|')
                .Append(amount.ToString("P2", CultureInfo.InvariantCulture)).Append('|')
                .Append(amount.ToString("F100", CultureInfo.InvariantCulture)).Append(';');
        }
        int state = 42;
        for (int index = 0; index < 100; index++)
        {
            state = state * 1664525 + 1013904223;
            var amount = state / 1000.0;
            output.Append(amount.ToString("F20", CultureInfo.InvariantCulture)).Append('|')
                .Append(amount.ToString("P2", CultureInfo.InvariantCulture)).Append(';');
        }
        output.Append(0.1f.ToString("F20", CultureInfo.InvariantCulture));
        return output.ToString();
    }

    private static string ConfigurationFlags()
    {
        var output = new StringBuilder();
        foreach (var text in new[] { "true", " FALSE ", "\u00a0true\u00a0", "\0true\0", "false\0 ", "yes", "1", "", "\0" })
        {
            var valid = bool.TryParse(text, out var enabled);
            var line = $"{valid}:{enabled};";
            output.Append(line);
        }
        return output.ToString();
    }

    private static string SinglePrecisionRounding()
    {
        var output = new StringBuilder();
        foreach (var amount in new float[] { 2.675f, 2.685f, 1.005f, -2.675f, 1e8f, 12345678f, float.NaN, float.PositiveInfinity })
            output.Append(MathF.Round(amount, 2).ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(MathF.Round(amount, 6).ToString(CultureInfo.InvariantCulture)).Append(';');
        return output.ToString();
    }

    private static string BillingCalendar()
    {
        var output = new StringBuilder();
        foreach (var start in new[]
        {
            new DateTimeOffset(2024, 1, 31, 23, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 2, 29, 23, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(1, 1, 31, 23, 45, 0, TimeSpan.Zero)
        })
        {
            var renewal = start.AddMonths(1).AddDays(2).AddHours(1);
            var line = $"{renewal:O}|{start.AddYears(1):O}|{(renewal - start).TotalHours};";
            output.Append(line);
        }
        return output.ToString();
    }

    private static string PolicyText()
    {
        var parts = "read, write,, admin ,".Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var route = "/API/orders/OPEN";
        return $"{string.Join('|', parts)}:{route.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)}:"
            + $"{route.IndexOf("orders", 5, 6, StringComparison.Ordinal)}:{route.LastIndexOf("OPEN", StringComparison.Ordinal)}";
    }
}
