using System.Globalization;

// Independent verification of billing display, adjustment rounding and configuration parsing.
public static class VerificationNumericScenarios
{
    public static Dictionary<string, string> Run()
    {
        var results = new Dictionary<string, string>();
        VerifyRounding(results);
        VerifyFormatting(results);
        VerifyConfiguration(results);
        VerifySingleParsing(results);
        VerifySchedulingBounds(results);
        return results;
    }

    private static void VerifyRounding(Dictionary<string, string> results)
    {
        double[] amounts = [-0.0, 0, double.Epsilon, -double.Epsilon, 0.49999999999999994,
            -0.49999999999999994, 0.5, -0.5, 1.5, -1.5, 2.5, -2.5, 2.675, -2.675,
            0.0000005, -0.0000005, 12345678.125, -12345678.125, 99999999.5, -99999999.5,
            9999999999999998d, -9999999999999998d, 1e16, -1e16, double.MaxValue,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        for (var index = 0; index < amounts.Length; index++)
        {
            var amount = amounts[index];
            var single = (float)amount;
            foreach (var digits in new[] { 0, 1, 2, 6, 15 })
                results[$"adjustment-double-{index}-{digits}"] = Math.Round(amount, digits).ToString(CultureInfo.InvariantCulture);
            foreach (var digits in new[] { 0, 1, 2, 6 })
                results[$"adjustment-single-{index}-{digits}"] = MathF.Round(single, digits).ToString(CultureInfo.InvariantCulture);
        }
        var state = 8675309;
        for (var index = 0; index < 120; index++)
        {
            state = state * 1664525 + 1013904223;
            var amount = state / 1000000.0;
            var single = (float)amount;
            foreach (var digits in new[] { 0, 2, 6 })
            {
                results[$"adjustment-random-double-{index}-{digits}"] = Math.Round(amount, digits).ToString(CultureInfo.InvariantCulture);
                results[$"adjustment-random-single-{index}-{digits}"] = MathF.Round(single, digits).ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private static void VerifyFormatting(Dictionary<string, string> results)
    {
        double[] amounts = [-0.0, 0, double.Epsilon, -double.Epsilon, 0.005, -0.005,
            0.125, -0.125, 1.005, -1.005, 2.675, -2.675, 2.685, -2.685, 0.1,
            2147483647, -2147483648, 4294967295, 1e20, -1e20, double.MaxValue,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        for (var index = 0; index < amounts.Length; index++)
        {
            var amount = amounts[index];
            var single = (float)amount;
            results[$"invoice-double-{index}"] = amount.ToString("F2", CultureInfo.InvariantCulture) + "|"
                + amount.ToString("N0", CultureInfo.InvariantCulture) + "|"
                + amount.ToString("P2", CultureInfo.InvariantCulture) + "|"
                + amount.ToString("F30", CultureInfo.InvariantCulture);
            results[$"invoice-single-{index}"] = single.ToString("F2", CultureInfo.InvariantCulture) + "|"
                + single.ToString("N0", CultureInfo.InvariantCulture) + "|"
                + single.ToString("P2", CultureInfo.InvariantCulture) + "|"
                + single.ToString("F30", CultureInfo.InvariantCulture);
        }
    }

    private static void VerifyConfiguration(Dictionary<string, string> results)
    {
        string?[] values = [null, "", "42", "-0", "+0", "4294967295", "4294967296", "-2147483648",
            "2147483648", "1,2", "1,,2", "1,,,", "1,", ",1", "1,.5", "1.2,3", "1,2e3",
            "1e3,2", "1e+", ".5", "+.5", "-0.0", "1e-9999", "-1e-9999", "1e9999", "NaN",
            "+NaN", "-NaN", "Infinity", "+Infinity", "-Infinity", "true", "false",
            "1.00000005960464477539062500000000000000001",
            "-1.00000005960464477539062500000000000000001",
            "1.00000017881393432617187499999999999999999",
            "-1.00000017881393432617187499999999999999999",
            "3.4028235677973366163753939545814256844799e38"];
        string[] borders = ["", " ", "\t", "\r\n", "\0", "\0\0", " \0", "\0 ", "\u00a0", "\u0085", "\u2028"];
        for (var index = 0; index < values.Length; index++)
        {
            results[$"configuration-{index}"] = ParseConfiguration(values[index]);
            if (values[index] is null) continue;
            for (var edge = 0; edge < borders.Length; edge++)
            {
                results[$"configuration-left-{index}-{edge}"] = ParseConfiguration(borders[edge] + values[index]);
                results[$"configuration-right-{index}-{edge}"] = ParseConfiguration(values[index] + borders[edge]);
            }
        }
    }

    private static string ParseConfiguration(string? text)
    {
        var integerValid = int.TryParse(text, CultureInfo.InvariantCulture, out var integer);
        var unsignedValid = uint.TryParse(text, CultureInfo.InvariantCulture, out var unsigned);
        var doubleValid = double.TryParse(text, CultureInfo.InvariantCulture, out var amount);
        var singleValid = float.TryParse(text, CultureInfo.InvariantCulture, out var single);
        var flagValid = bool.TryParse(text, out var enabled);
        return $"{integerValid}:{integer}|{unsignedValid}:{unsigned}|{doubleValid}:"
            + amount.ToString(CultureInfo.InvariantCulture) + $"|{singleValid}:"
            + single.ToString(CultureInfo.InvariantCulture) + $"|{flagValid}:{enabled}";
    }

    private static void VerifySchedulingBounds(Dictionary<string, string> results)
    {
        double[] values = [double.NaN, double.NegativeInfinity, -1, -0.0, 0, 1, 2, double.PositiveInfinity];
        double[] limits = [double.NaN, double.NegativeInfinity, -1, -0.0, 0, 1, double.PositiveInfinity];
        for (var value = 0; value < values.Length; value++)
            for (var minimum = 0; minimum < limits.Length; minimum++)
                for (var maximum = 0; maximum < limits.Length; maximum++)
                {
                    try
                    {
                        results[$"retry-bound-{value}-{minimum}-{maximum}"] =
                            Math.Clamp(values[value], limits[minimum], limits[maximum]).ToString(CultureInfo.InvariantCulture);
                    }
                    catch (Exception) { results[$"retry-bound-{value}-{minimum}-{maximum}"] = "rejected"; }
                }
        double[] progress = [0, -0.0, 1, -1, double.NaN, double.PositiveInfinity];
        double[] bases = [0, -0.0, 1, -1, 2, double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        for (var amount = 0; amount < progress.Length; amount++)
            for (var basis = 0; basis < bases.Length; basis++)
                results[$"backoff-level-{amount}-{basis}"] =
                    Math.Log(progress[amount], bases[basis]).ToString(CultureInfo.InvariantCulture);
    }

    private static void VerifySingleParsing(Dictionary<string, string> results)
    {
        string[] boundaries = [
            "1.000000059604644775390625", "1.000000178813934326171875",
            "1.00000005960464477539062499999999999999999",
            "1.00000017881393432617187500000000000000001",
            "3.40282356779733661637539395458142568448e38",
            "3.4028235677973366163753939545814256844801e38",
            "7.00649232162408535461864791644958065640130970938257885878534141944895541342930300743319094181060791015625e-46",
            "7.006492321624085354618647916449580656401309709382578858785341419448955413429303007433190941810607910156251e-46",
            "7.006492321624085354618647916449580656401309709382578858785341419448955413429303007433190941810607910156249e-46"];
        for (var index = 0; index < boundaries.Length; index++)
        {
            results[$"single-boundary-positive-{index}"] = ParseConfiguration(boundaries[index]);
            results[$"single-boundary-negative-{index}"] = ParseConfiguration("-" + boundaries[index]);
        }
        var state = 42;
        for (var index = 0; index < 80; index++)
        {
            state = state * 1664525 + 1013904223;
            var value = state / 1000000.0;
            results[$"single-configuration-random-{index}"] =
                ParseConfiguration(value.ToString("F30", CultureInfo.InvariantCulture));
        }
    }
}
