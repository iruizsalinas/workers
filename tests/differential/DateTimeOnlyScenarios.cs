using System.Globalization;
using System.Text.Json;

// DateOnly and TimeOnly compiled both by the CLR and by Workers: construction, calendar arithmetic,
// comparison, invariant formatting, exact parsing, JSON contracts, LINQ and nullable values.
public static class DateTimeOnlyScenarios
{
    public static Dictionary<string, string> Run()
    {
        var results = new Dictionary<string, string>();
        results["construction"] = Construction();
        results["calendar"] = Calendar();
        results["dayNumbers"] = DayNumbers();
        results["dateArithmetic"] = DateArithmetic();
        results["timeArithmetic"] = TimeArithmetic();
        results["comparison"] = Comparison();
        results["dateFormatting"] = DateFormatting();
        results["timeFormatting"] = TimeFormatting();
        results["interpolation"] = Interpolation();
        results["parsing"] = Parsing();
        results["tryParsing"] = TryParsing();
        results["conversions"] = Conversions();
        results["json"] = Json();
        results["jsonReading"] = JsonReading();
        results["linq"] = Linq();
        results["nullable"] = Nullable();
        results["records"] = Records();
        results["ranges"] = Ranges();
        return results;
    }

    // Delegates are invoked through LINQ, which the Workers profile supports.
    private static string Attempt(Func<int, string> action)
    {
        try
        {
            return new[] { 0 }.Select(action).First();
        }
        catch (Exception)
        {
            return "error";
        }
    }

    private static string Construction()
    {
        var date = new DateOnly(2026, 10, 8);
        var time = new TimeOnly(14, 5, 9, 123, 456);
        var ticks = new TimeOnly(1);
        var empty = new DateOnly();
        var fallback = new DateOnly[1];
        return $"{date.Year}-{date.Month}-{date.Day} {(int)date.DayOfWeek} {date.DayOfYear}"
            + $" | {time.Hour}:{time.Minute}:{time.Second}.{time.Millisecond}.{time.Microsecond}.{time.Nanosecond} {time.Ticks}"
            + $" | {ticks.Ticks} {ticks.Nanosecond} {new TimeOnly(8, 30).Ticks} {new TimeOnly(8, 30, 15).Second}"
            + $" | {empty.Year} {fallback[0].DayNumber} {DateOnly.MinValue.DayNumber} {DateOnly.MaxValue.DayNumber}"
            + $" | {TimeOnly.MinValue.Ticks} {TimeOnly.MaxValue.Ticks}"
            + $" | {Attempt(_ => new DateOnly(2026, 2, 29).Year.ToString())} {Attempt(_ => new DateOnly(2024, 2, 29).Day.ToString())}"
            + $" {Attempt(_ => new DateOnly(0, 1, 1).Year.ToString())} {Attempt(_ => new DateOnly(10000, 1, 1).Year.ToString())}"
            + $" {Attempt(_ => new TimeOnly(24, 0).Hour.ToString())} {Attempt(_ => new TimeOnly(1, 2, 3, 1000).Hour.ToString())}"
            + $" {Attempt(_ => new TimeOnly(-1).Hour.ToString())}";
    }

    private static string Calendar()
    {
        var dates = new[]
        {
            new DateOnly(1, 1, 1), new DateOnly(1600, 2, 29), new DateOnly(1900, 3, 1), new DateOnly(1969, 12, 31),
            new DateOnly(1970, 1, 1), new DateOnly(2000, 2, 29), new DateOnly(2024, 12, 31), new DateOnly(9999, 12, 31)
        };
        return string.Join(" ", dates.Select(date => $"{(int)date.DayOfWeek}/{date.DayOfYear}/{date.DayNumber}"));
    }

    private static string DayNumbers()
    {
        var checks = new List<string>();
        foreach (var number in new[] { 0, 1, 58, 59, 365, 146096, 146097, 693594, 730119, 739896, 3652058 })
        {
            var date = DateOnly.FromDayNumber(number);
            checks.Add($"{number}={date.ToString("O")}:{date.DayNumber == number}");
        }
        var roundTrips = 0;
        for (var number = 0; number <= 3652058; number += 997)
            if (DateOnly.FromDayNumber(number).DayNumber == number) roundTrips++;
        return string.Join(" ", checks) + $" {roundTrips}"
            + $" {Attempt(_ => DateOnly.FromDayNumber(-1).ToString("O"))} {Attempt(_ => DateOnly.FromDayNumber(3652059).ToString("O"))}";
    }

    private static string DateArithmetic()
    {
        var date = new DateOnly(2024, 1, 31);
        return $"{date.AddMonths(1):O} {date.AddMonths(13):O} {date.AddMonths(-2):O} {new DateOnly(2024, 2, 29).AddYears(1):O}"
            + $" {new DateOnly(2024, 2, 29).AddYears(4):O} {date.AddDays(30):O} {date.AddDays(-31):O} {date.AddDays(366):O}"
            + $" {new DateOnly(2026, 3, 31).AddMonths(-1):O} {new DateOnly(1, 1, 1).AddMonths(119987):O}"
            + $" {Attempt(_ => DateOnly.MaxValue.AddDays(1).ToString("O"))} {Attempt(_ => DateOnly.MinValue.AddDays(-1).ToString("O"))}"
            + $" {Attempt(_ => DateOnly.MaxValue.AddMonths(1).ToString("O"))} {Attempt(_ => date.AddMonths(120001).ToString("O"))}"
            + $" {Attempt(_ => date.AddYears(-2024).ToString("O"))} {Attempt(_ => date.AddYears(10001).ToString("O"))}";
    }

    private static string TimeArithmetic()
    {
        var time = new TimeOnly(23, 0);
        var late = new TimeOnly(1, 0);
        var wrapped = time.Add(TimeSpan.FromHours(2));
        var backwards = late.Add(TimeSpan.FromHours(-2));
        var precise = new TimeOnly(0, 0).Add(TimeSpan.FromMilliseconds(1.5));
        return $"{wrapped:O} {backwards:O} {late.AddHours(-25.5):O} {time.AddMinutes(90):O} {late.AddMinutes(-0.25):O}"
            + $" {precise:O} {time.AddHours(48):O} {time.AddHours(double.NaN):O}"
            + $" {(late - time).TotalHours} {(time - late).TotalHours} {(time - time).TotalHours} {(precise - new TimeOnly(0, 0)).TotalMilliseconds}"
            + $" {late.IsBetween(time, new TimeOnly(2, 0))} {new TimeOnly(2, 0).IsBetween(late, new TimeOnly(2, 0))}"
            + $" {late.IsBetween(late, new TimeOnly(2, 0))} {late.IsBetween(late, late)} {new TimeOnly(12, 0).IsBetween(time, late)}"
            + $" {time.ToTimeSpan().TotalMinutes} {TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(90.5)):O}"
            + $" {Attempt(_ => TimeOnly.FromTimeSpan(TimeSpan.FromHours(24)).ToString("O"))}"
            + $" {Attempt(_ => TimeOnly.FromTimeSpan(TimeSpan.FromHours(-1)).ToString("O"))}";
    }

    private static string Comparison()
    {
        var first = new DateOnly(2026, 1, 2);
        var second = new DateOnly(2026, 10, 1);
        var morning = new TimeOnly(9, 0);
        var precise = new TimeOnly(9, 0, 0, 0, 1);
        return $"{first < second} {first <= new DateOnly(2026, 1, 2)} {first > second} {first >= second} {first == new DateOnly(2026, 1, 2)} {first != second}"
            + $" {first.CompareTo(second)} {second.CompareTo(first)} {first.CompareTo(first)} {first.Equals(second)}"
            + $" {morning < precise} {precise > morning} {morning == new TimeOnly(9, 0, 0)} {precise.CompareTo(morning)}"
            + $" {new TimeOnly(10, 0) > new TimeOnly(9, 59, 59, 999, 999)} {morning.Equals(new TimeOnly(9, 0))}"
            + $" {DateOnly.MinValue < DateOnly.MaxValue} {new DateOnly(999, 1, 1) < new DateOnly(1000, 1, 1)}";
    }

    private static string DateFormatting()
    {
        var date = new DateOnly(2026, 10, 8);
        var early = new DateOnly(5, 3, 7);
        return string.Join(" | ", new[]
        {
            $"{date.ToString("d", CultureInfo.InvariantCulture)}/{early.ToString("d", CultureInfo.InvariantCulture)}",
            $"{date.ToString("D", CultureInfo.InvariantCulture)}/{early.ToString("D", CultureInfo.InvariantCulture)}",
            $"{date.ToString("m", CultureInfo.InvariantCulture)}/{early.ToString("m", CultureInfo.InvariantCulture)}",
            $"{date.ToString("M", CultureInfo.InvariantCulture)}/{early.ToString("M", CultureInfo.InvariantCulture)}",
            $"{date.ToString("o", CultureInfo.InvariantCulture)}/{early.ToString("o", CultureInfo.InvariantCulture)}",
            $"{date.ToString("O", CultureInfo.InvariantCulture)}/{early.ToString("O", CultureInfo.InvariantCulture)}",
            $"{date.ToString("r", CultureInfo.InvariantCulture)}/{early.ToString("r", CultureInfo.InvariantCulture)}",
            $"{date.ToString("R", CultureInfo.InvariantCulture)}/{early.ToString("R", CultureInfo.InvariantCulture)}",
            $"{date.ToString("y", CultureInfo.InvariantCulture)}/{early.ToString("y", CultureInfo.InvariantCulture)}",
            $"{date.ToString("Y", CultureInfo.InvariantCulture)}/{early.ToString("Y", CultureInfo.InvariantCulture)}",
            $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}/{early.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            $"{date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}/{early.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
            $"{date.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture)}/{early.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture)}",
            $"{date.ToString("yy-M-d", CultureInfo.InvariantCulture)}/{early.ToString("yy-M-d", CultureInfo.InvariantCulture)}",
            $"{date.ToString("dddd", CultureInfo.InvariantCulture)}/{early.ToString("dddd", CultureInfo.InvariantCulture)}",
            $"{date.ToString("%d", CultureInfo.InvariantCulture)}/{early.ToString("%d", CultureInfo.InvariantCulture)}",
            $"{date.ToString("'Week' d", CultureInfo.InvariantCulture)}/{early.ToString("'Week' d", CultureInfo.InvariantCulture)}",
            $"{date.ToString("MMMM", CultureInfo.InvariantCulture)}/{early.ToString("MMMM", CultureInfo.InvariantCulture)}",
            $"{date.ToString("yyyyy", CultureInfo.InvariantCulture)}/{early.ToString("yyyyy", CultureInfo.InvariantCulture)}",
        }) + $" | {date.ToString("O")} {date.ToString("R")} {date.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string TimeFormatting()
    {
        var time = new TimeOnly(14, 5, 9, 123, 456);
        var midnight = new TimeOnly(0, 7);
        return string.Join(" | ", new[]
        {
            $"{time.ToString("t", CultureInfo.InvariantCulture)}/{midnight.ToString("t", CultureInfo.InvariantCulture)}",
            $"{time.ToString("T", CultureInfo.InvariantCulture)}/{midnight.ToString("T", CultureInfo.InvariantCulture)}",
            $"{time.ToString("o", CultureInfo.InvariantCulture)}/{midnight.ToString("o", CultureInfo.InvariantCulture)}",
            $"{time.ToString("O", CultureInfo.InvariantCulture)}/{midnight.ToString("O", CultureInfo.InvariantCulture)}",
            $"{time.ToString("r", CultureInfo.InvariantCulture)}/{midnight.ToString("r", CultureInfo.InvariantCulture)}",
            $"{time.ToString("R", CultureInfo.InvariantCulture)}/{midnight.ToString("R", CultureInfo.InvariantCulture)}",
            $"{time.ToString("HH:mm", CultureInfo.InvariantCulture)}/{midnight.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            $"{time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}/{midnight.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}",
            $"{time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}/{midnight.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}",
            $"{time.ToString("hh:mm tt", CultureInfo.InvariantCulture)}/{midnight.ToString("hh:mm tt", CultureInfo.InvariantCulture)}",
            $"{time.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture)}/{midnight.ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture)}",
            $"{time.ToString("H:m:s", CultureInfo.InvariantCulture)}/{midnight.ToString("H:m:s", CultureInfo.InvariantCulture)}",
            $"{time.ToString("h tt", CultureInfo.InvariantCulture)}/{midnight.ToString("h tt", CultureInfo.InvariantCulture)}",
            $"{time.ToString("%h", CultureInfo.InvariantCulture)}/{midnight.ToString("%h", CultureInfo.InvariantCulture)}",
            $"{time.ToString("ffff", CultureInfo.InvariantCulture)}/{midnight.ToString("ffff", CultureInfo.InvariantCulture)}",
            $"{time.ToString("FFFFFFF", CultureInfo.InvariantCulture)}/{midnight.ToString("FFFFFFF", CultureInfo.InvariantCulture)}",
            $"{time.ToString("HH'h'mm", CultureInfo.InvariantCulture)}/{midnight.ToString("HH'h'mm", CultureInfo.InvariantCulture)}",
        }) + $" | {time.ToString("O")} {midnight.ToString("R")} {time.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string Interpolation()
    {
        var date = new DateOnly(2026, 10, 8);
        var time = new TimeOnly(7, 5, 3);
        DateOnly? missing = null;
        DateOnly? present = date;
        return FormattableString.Invariant($"{date} {time} {date:yyyy/MM/dd} {time:HH.mm} {missing}|{present} {present:O}")
            + $" {date:O} {time:o} {date:R} {time:r}"
            + string.Create(CultureInfo.InvariantCulture, $" {date:D} {time:T}");
    }

    private static string Parsing()
    {
        var dates = new[] { "2026-10-08", "2026-1-08", "2026-10-8", "02026-10-08", " 2026-10-08", "2026-10-08 ", "2026-02-30",
            "0000-01-01", "2026-10-08T00:00", "999-10-08", "2024-02-29", "9999-12-31" };
        var times = new[] { "08:30", "8:30", "08:30:00", "08:5", "24:00", "23:59", "08:60" };
        var exact = new[] { "08:30:00", "08:30:00.1234567", "08:30:00.5", "08:30:00.5000000", "08:30" };
        return string.Join(",", dates.Select(text => Attempt(_ => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("O"))))
            + " | " + string.Join(",", dates.Select(text => Attempt(_ => DateOnly.ParseExact(text, "O").ToString("O"))))
            + " | " + string.Join(",", times.Select(text => Attempt(_ => TimeOnly.ParseExact(text, "HH:mm", CultureInfo.InvariantCulture).ToString("O"))))
            + " | " + string.Join(",", exact.Select(text => Attempt(_ => TimeOnly.ParseExact(text, "O", CultureInfo.InvariantCulture).ToString("O"))))
            + " | " + string.Join(",", exact.Select(text => Attempt(_ => TimeOnly.ParseExact(text, "HH:mm:ss", CultureInfo.InvariantCulture).ToString("O"))))
            + " | " + Attempt(_ => DateOnly.ParseExact("08/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture).ToString("O"))
            + " " + Attempt(_ => DateOnly.ParseExact("20261008", "yyyyMMdd", CultureInfo.InvariantCulture).ToString("O"))
            + " " + Attempt(_ => DateOnly.ParseExact("2026.10.08", "yyyy'.'MM'.'dd", CultureInfo.InvariantCulture).ToString("O"))
            + " " + Attempt(_ => TimeOnly.ParseExact("08h30", "HH'h'mm", CultureInfo.InvariantCulture).ToString("O"))
            + " " + Attempt(_ => TimeOnly.ParseExact("08:30:15.250", "HH:mm:ss.fff", CultureInfo.InvariantCulture).ToString("O"))
            + " " + Attempt(_ => DateOnly.ParseExact(null!, "O").ToString("O"));
    }

    private static string TryParsing()
    {
        var parsed = DateOnly.TryParseExact("2026-10-08", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
        var rejected = DateOnly.TryParseExact("2026-13-08", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var invalid);
        var time = TimeOnly.MinValue;
        var timeParsed = TimeOnly.TryParseExact("19:45:00.0000001", "O", out time);
        var nullParsed = TimeOnly.TryParseExact(null, "O", out var none);
        return $"{parsed} {date:O} {rejected} {invalid:O} {timeParsed} {time:O} {nullParsed} {none:O}";
    }

    private static string Conversions()
    {
        var instant = new DateTimeOffset(2026, 10, 8, 23, 59, 58, 750, TimeSpan.Zero).DateTime;
        var date = DateOnly.FromDateTime(instant);
        var time = TimeOnly.FromDateTime(instant);
        var combined = date.ToDateTime(new TimeOnly(5, 6, 7, 8));
        return $"{date:O} {time:O} {combined.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)} {combined.Year} {combined.Hour}"
            + $" {DateOnly.FromDateTime(new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero).DateTime):O}";
    }

    public sealed record Booking(string Name, DateOnly Day, TimeOnly Start, DateOnly? Until, TimeOnly? End);

    public sealed class Shift
    {
        public DateOnly Day { get; set; }
        public TimeOnly Start { get; set; }
        public List<DateOnly> Holidays { get; set; } = [];
    }

    private static string Json()
    {
        var booking = new Booking("standup", new DateOnly(2026, 10, 8), new TimeOnly(9, 30, 0, 250), null, new TimeOnly(10, 0));
        var text = JsonSerializer.Serialize(booking);
        var read = JsonSerializer.Deserialize<Booking>(text)!;
        var shift = new Shift { Day = new DateOnly(5, 1, 2), Start = new TimeOnly(0, 0, 0, 0, 1), Holidays = [new DateOnly(2026, 12, 25)] };
        var shiftText = JsonSerializer.Serialize(shift);
        var shiftRead = JsonSerializer.Deserialize<Shift>(shiftText)!;
        return $"{text} {read == booking} {read.Day:O} {read.Start:O} {read.Until is null} {read.End:O}"
            + $" {shiftText} {shiftRead.Day:O} {shiftRead.Start.Ticks} {shiftRead.Holidays.Count}"
            + $" {JsonSerializer.Serialize(new[] { TimeOnly.MaxValue, TimeOnly.MinValue })} {JsonSerializer.Serialize(DateOnly.MaxValue)}";
    }

    private static string JsonReading()
    {
        var dates = new[] { "\"2026-10-08\"", "\"2026-1-08\"", "\"02026-10-08\"", "\" 2026-10-08\"", "\"2026-02-30\"",
            "\"2026-10-08T00:00\"", "\"0000-01-01\"", "\"2024-02-29\"", "20261008", "null", "\"2026\\u002D10-08\"" };
        var times = new[] { "\"08:30\"", "\"08:30:00\"", "\"8:30\"", "\"08:30:00.5\"", "\"08:30:00.1234567\"", "\"08:30:00.12345678\"",
            "\"24:00:00\"", "\"23:59:59.9999999\"", "\"1.08:30:00\"", "\"0\"", "\"-08:30\"", "\" 08:30\"", "\"08:30:60\"", "\"08:60\"",
            "\"08:30:00.\"", "\"0:0\"", "\"08:3\"", "\"8:3:5\"", "\"008:30\"", "\"0000000008:30\"", "\"08:30:00000\"", "\"000\"",
            "\"0:0.5\"", "\"0:0:0.12345678\"", "\"99:00\"", "\"08:30Z\"", "30" };
        return string.Join(",", dates.Select(json => Attempt(_ => JsonSerializer.Deserialize<DateOnly>(json).ToString("O"))))
            + " | " + string.Join(",", times.Select(json => Attempt(_ => JsonSerializer.Deserialize<TimeOnly>(json).ToString("O"))))
            + " | " + Attempt(_ => JsonSerializer.Deserialize<DateOnly?>("null")?.ToString("O") ?? "none")
            + " " + Attempt(_ => string.Join(",", JsonSerializer.Deserialize<List<TimeOnly>>("[\"1:2\",\"03:04:05.6\"]")!.Select(time => time.ToString("O"))));
    }

    private static string Linq()
    {
        var dates = new List<DateOnly> { new(2026, 3, 1), new(2025, 12, 31), new(2026, 1, 15), new(2025, 12, 31), new(999, 5, 5) };
        var times = new[] { new TimeOnly(12, 0), new TimeOnly(9, 30), new TimeOnly(9, 30, 0, 0, 1), new TimeOnly(23, 59) };
        var byMonth = dates.GroupBy(date => date.Month).OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}");
        return string.Join(",", dates.OrderBy(date => date).Select(date => date.ToString("O")))
            + " " + string.Join(",", dates.OrderByDescending(date => date).ThenBy(date => date.Day).Take(2).Select(date => date.ToString("O")))
            + $" {dates.Min():O} {dates.Max():O} {dates.Distinct().Count()} {dates.Contains(new DateOnly(2026, 1, 15))}"
            + $" {dates.IndexOf(new DateOnly(2025, 12, 31))} {new HashSet<DateOnly>(dates).Count}"
            + " " + string.Join(",", byMonth)
            + " " + string.Join(",", times.OrderBy(time => time).Select(time => time.ToString("O")))
            + $" {times.Max():O} {times.Min():O} {times.Count(time => time.IsBetween(new TimeOnly(9, 0), new TimeOnly(12, 0)))}";
    }

    private static string Nullable()
    {
        DateOnly? missing = null;
        DateOnly? date = new DateOnly(2026, 10, 8);
        TimeOnly? time = new TimeOnly(9, 0);
        TimeOnly? noTime = null;
        return $"{missing is null} {date is not null} {((DateOnly)date!).Year} {date?.Year} {missing?.Year is null} {date?.AddDays(1):O}"
            + $" {date == new DateOnly(2026, 10, 8)} {missing == date} {missing < date} {date > missing} {date >= new DateOnly(2026, 1, 1)}"
            + $" {(time - noTime) is null} {((TimeSpan)(time - new TimeOnly(8, 0))!).TotalMinutes} {time?.Hour} {(missing ?? DateOnly.MinValue):O}";
    }

    private static string Records()
    {
        var first = new Booking("a", new DateOnly(2026, 1, 1), new TimeOnly(9, 0), null, null);
        var same = new Booking("a", new DateOnly(2026, 1, 1), new TimeOnly(9, 0), null, null);
        var later = first with { Start = new TimeOnly(9, 0, 0, 0, 1) };
        return $"{first == same} {first.Equals(later)} {later.Start > first.Start} {(later with { Start = new TimeOnly(9, 0) }) == first}";
    }

    private static string Ranges()
    {
        var start = new DateOnly(2026, 2, 26);
        var days = new List<string>();
        for (var day = start; day <= new DateOnly(2026, 3, 2); day = day.AddDays(1))
            days.Add($"{day.Day}:{(int)day.DayOfWeek}");
        var slots = new List<string>();
        for (var slot = new TimeOnly(22, 0); slots.Count < 4; slot = slot.AddMinutes(45))
            slots.Add(slot.ToString("HH:mm", CultureInfo.InvariantCulture));
        return string.Join(",", days) + " " + string.Join(",", slots)
            + $" {new DateOnly(2026, 3, 2).DayNumber - start.DayNumber}";
    }
}
