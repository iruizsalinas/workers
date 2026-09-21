internal static partial class HelperSource
{
    private static string DateTimeOffset(Func<string, string> name) => $$"""
        function {{name("dateTimeOffset")}}(year, month, day, hour, minute, second, millisecond = 0) {
          const value = new Date(0);
          value.setUTCFullYear(year, month - 1, day);
          value.setUTCHours(hour, minute, second, millisecond);
          if (year < 1 || year > 9999
              || value.getUTCFullYear() !== year || value.getUTCMonth() !== month - 1
              || value.getUTCDate() !== day || value.getUTCHours() !== hour
              || value.getUTCMinutes() !== minute || value.getUTCSeconds() !== second
              || value.getUTCMilliseconds() !== millisecond) {
            throw new RangeError("Invalid DateTimeOffset components.");
          }
          return value;
        }

        """;

    private static string DateTimeAddMonths(Func<string, string> name) => $$"""
        function {{name("dateTimeAddMonths")}}(input, months) {
          if (!Number.isInteger(months) || months < -120000 || months > 120000)
            throw new RangeError("Months must be between -120000 and 120000.");
          const value = new Date(input);
          const absoluteMonth = value.getUTCFullYear() * 12 + value.getUTCMonth() + months;
          const year = Math.floor(absoluteMonth / 12), month = absoluteMonth - year * 12;
          if (year < 1 || year > 9999) throw new RangeError("DateTime value is out of range.");
          const monthEnd = new Date(0);
          monthEnd.setUTCFullYear(year, month + 1, 0);
          const day = Math.min(value.getUTCDate(), monthEnd.getUTCDate());
          value.setUTCDate(1);
          value.setUTCFullYear(year, month, day);
          return value;
        }

        """;

    private static string DateTimeFromUnixTime(Func<string, string> name) => $$"""
        function {{name("dateTimeFromUnixTime")}}(value, seconds) {
          const minimum = seconds ? -62135596800 : -62135596800000;
          const maximum = seconds ? 253402300799 : 253402300799999;
          if (!Number.isInteger(value) || value < minimum || value > maximum)
            throw new RangeError("Unix time is out of range.");
          return new Date(seconds ? value * 1000 : value);
        }

        """;

    private static string DateTimeCompare(Func<string, string> name) => $$"""
        function {{name("dateTimeCompare")}}(left, right) {
          const difference = new Date(left).getTime() - new Date(right).getTime();
          return difference < 0 ? -1 : difference > 0 ? 1 : 0;
        }

        """;

    private static string DateTimeDayOfYear(Func<string, string> name) => $$"""
        function {{name("dateTimeDayOfYear")}}(input) {
          const value = new Date(input), start = new Date(0);
          start.setUTCFullYear(value.getUTCFullYear(), 0, 1);
          start.setUTCHours(0, 0, 0, 0);
          return Math.floor((value.getTime() - start.getTime()) / 86400000) + 1;
        }

        """;

    private static string DateTimeIsLeapYear(Func<string, string> name) => $$"""
        function {{name("dateTimeIsLeapYear")}}(year) {
          if (!Number.isInteger(year) || year < 1 || year > 9999)
            throw new RangeError("Year must be between 1 and 9999.");
          return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
        }

        """;

    private static string DateTimeDaysInMonth(Func<string, string> name) => $$"""
        function {{name("dateTimeDaysInMonth")}}(year, month) {
          if (!Number.isInteger(month) || month < 1 || month > 12)
            throw new RangeError("Month must be between 1 and 12.");
          return [31, {{name("dateTimeIsLeapYear")}}(year) ? 29 : 28, 31, 30, 31, 30,
            31, 31, 30, 31, 30, 31][month - 1];
        }

        """;

    private static string DateTimeAddMilliseconds(Func<string, string> name) => $$"""
        function {{name("dateTimeAddMilliseconds")}}(input, delta) {
          const milliseconds = new Date(input).getTime() + delta;
          if (!Number.isFinite(delta) || !Number.isFinite(milliseconds))
            throw new RangeError("DateTime value is out of range.");
          const value = new Date(milliseconds), year = value.getUTCFullYear();
          if (year < 1 || year > 9999)
            throw new RangeError("DateTime value is out of range.");
          return value;
        }

        """;
}

