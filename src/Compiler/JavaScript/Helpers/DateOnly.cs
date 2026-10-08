internal static partial class HelperSource
{
    // DateOnly values are "yyyy-MM-dd" strings, the System.Text.Json form, so equality, ordering and
    // JSON need no conversion. Day numbers count days since 0001-01-01 like DateOnly.DayNumber.
    private static string DateOnly(Func<string, string> name) => $$"""
        const {{name("dateOnlyMonthStarts")}} = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
        function {{name("dateOnlyIsLeap")}}(year) {
          return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
        }
        function {{name("dateOnlyDaysInMonth")}}(year, month) {
          return month === 2 ? ({{name("dateOnlyIsLeap")}}(year) ? 29 : 28) : [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1];
        }
        function {{name("dateOnlyText")}}(year, month, day) {
          return String(year).padStart(4, "0") + "-" + String(month).padStart(2, "0") + "-" + String(day).padStart(2, "0");
        }
        function {{name("dateOnlyCreate")}}(year, month, day) {
          if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)
              || year < 1 || year > 9999 || month < 1 || month > 12
              || day < 1 || day > {{name("dateOnlyDaysInMonth")}}(year, month))
            throw new RangeError("Year, Month, and Day parameters describe an un-representable DateTime.");
          return {{name("dateOnlyText")}}(year, month, day);
        }
        function {{name("dateOnlyParts")}}(value) {
          return [Number(value.slice(0, 4)), Number(value.slice(5, 7)), Number(value.slice(8, 10))];
        }
        function {{name("dateOnlyDayOfYear")}}(value) {
          const [year, month, day] = {{name("dateOnlyParts")}}(value);
          return {{name("dateOnlyMonthStarts")}}[month - 1] + (month > 2 && {{name("dateOnlyIsLeap")}}(year) ? 1 : 0) + day;
        }
        function {{name("dateOnlyDayNumber")}}(value) {
          const previous = Number(value.slice(0, 4)) - 1;
          return previous * 365 + Math.floor(previous / 4) - Math.floor(previous / 100) + Math.floor(previous / 400)
            + {{name("dateOnlyDayOfYear")}}(value) - 1;
        }
        function {{name("dateOnlyFromDayNumber")}}(number) {
          if (!Number.isInteger(number) || number < 0 || number > 3652058)
            throw new RangeError("Day number must be between 0 and DateOnly.MaxValue.DayNumber.");
          let days = number;
          const cycles400 = Math.floor(days / 146097);
          days -= cycles400 * 146097;
          const cycles100 = Math.min(Math.floor(days / 36524), 3);
          days -= cycles100 * 36524;
          const cycles4 = Math.floor(days / 1461);
          days -= cycles4 * 1461;
          const years = Math.min(Math.floor(days / 365), 3);
          days -= years * 365;
          const year = cycles400 * 400 + cycles100 * 100 + cycles4 * 4 + years + 1;
          const leap = {{name("dateOnlyIsLeap")}}(year) ? 1 : 0;
          const starts = {{name("dateOnlyMonthStarts")}};
          let month = 1;
          while (month < 12 && days >= starts[month] + (month >= 2 ? leap : 0)) month++;
          return {{name("dateOnlyText")}}(year, month, days - starts[month - 1] - (month > 2 ? leap : 0) + 1);
        }
        function {{name("dateOnlyAddDays")}}(value, days) {
          const number = {{name("dateOnlyDayNumber")}}(value) + days;
          if (!Number.isInteger(days) || number < 0 || number > 3652058)
            throw new RangeError("Value to add was out of range.");
          return {{name("dateOnlyFromDayNumber")}}(number);
        }
        function {{name("dateOnlyAddMonths")}}(value, months) {
          if (!Number.isInteger(months) || months < -120000 || months > 120000)
            throw new RangeError("Months value must be between +/-120000.");
          const [year, month, day] = {{name("dateOnlyParts")}}(value);
          const total = year * 12 + month - 1 + months;
          const resultYear = Math.floor(total / 12), resultMonth = total - resultYear * 12 + 1;
          if (resultYear < 1 || resultYear > 9999)
            throw new RangeError("The added or subtracted value results in an un-representable DateTime.");
          return {{name("dateOnlyText")}}(resultYear, resultMonth,
            Math.min(day, {{name("dateOnlyDaysInMonth")}}(resultYear, resultMonth)));
        }
        function {{name("dateOnlyAddYears")}}(value, years) {
          if (!Number.isInteger(years) || years < -10000 || years > 10000)
            throw new RangeError("Years value must be between +/-10000.");
          const [year, month, day] = {{name("dateOnlyParts")}}(value);
          const resultYear = year + years;
          if (resultYear < 1 || resultYear > 9999)
            throw new RangeError("The added or subtracted value results in an un-representable DateTime.");
          return {{name("dateOnlyText")}}(resultYear, month,
            Math.min(day, {{name("dateOnlyDaysInMonth")}}(resultYear, month)));
        }
        function {{name("dateOnlyFromDate")}}(input) {
          const value = new Date(input);
          return {{name("dateOnlyText")}}(value.getUTCFullYear(), value.getUTCMonth() + 1, value.getUTCDate());
        }
        function {{name("dateOnlyToDate")}}(value, ticks) {
          const [year, month, day] = {{name("dateOnlyParts")}}(value);
          const result = new Date(0);
          result.setUTCFullYear(year, month - 1, day);
          result.setUTCHours(0, 0, 0, Math.floor(ticks / 10000));
          return result;
        }
        function {{name("dateOnlyParse")}}(value, pattern, fields) {
          if (value == null) throw new TypeError("Value cannot be null. (Parameter 's')");
          const match = pattern.exec(value);
          if (match) {
            let year = 1, month = 1, day = 1;
            fields.forEach((field, index) => {
              const number = Number(match[index + 1]);
              if (field === "y") year = number;
              else if (field === "M") month = number;
              else day = number;
            });
            if (year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= {{name("dateOnlyDaysInMonth")}}(year, month))
              return {{name("dateOnlyText")}}(year, month, day);
          }
          throw new RangeError(`String '${value}' was not recognized as a valid DateOnly.`);
        }

        """;

    // TimeOnly values are "HH:mm:ss" strings with a seven-digit fraction when it is not zero, the
    // System.Text.Json form. The fixed-width prefix keeps string ordering chronological.
    private static string TimeOnly(Func<string, string> name) => $$"""
        const {{name("timeOnlyTicksPerDay")}} = 864000000000;
        function {{name("timeOnlyText")}}(ticks) {
          const pad = number => String(number).padStart(2, "0");
          const fraction = ticks % 10000000;
          return pad(Math.floor(ticks / 36000000000)) + ":" + pad(Math.floor(ticks / 600000000) % 60) + ":"
            + pad(Math.floor(ticks / 10000000) % 60) + (fraction ? "." + String(fraction).padStart(7, "0") : "");
        }
        function {{name("timeOnlyFromTicks")}}(ticks) {
          if (!Number.isInteger(ticks) || ticks < 0 || ticks >= {{name("timeOnlyTicksPerDay")}})
            throw new RangeError("Ticks must be between 0 and and TimeOnly.MaxValue.Ticks.");
          return {{name("timeOnlyText")}}(ticks);
        }
        function {{name("timeOnlyCreate")}}(hour, minute, second = 0, millisecond = 0, microsecond = 0) {
          if (![hour, minute, second, millisecond, microsecond].every(Number.isInteger)
              || hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
            throw new RangeError("Hour, Minute, and Second parameters describe an un-representable DateTime.");
          if (millisecond < 0 || millisecond > 999 || microsecond < 0 || microsecond > 999)
            throw new RangeError("Valid values are between 0 and 999, inclusive.");
          return {{name("timeOnlyText")}}(((hour * 60 + minute) * 60 + second) * 10000000 + millisecond * 10000 + microsecond * 10);
        }
        function {{name("timeOnlyFraction")}}(value) {
          return value.length > 8 ? Number(value.slice(9)) : 0;
        }
        function {{name("timeOnlyTicks")}}(value) {
          return ((Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5))) * 60 + Number(value.slice(6, 8))) * 10000000
            + (value.length > 8 ? Number(value.slice(9)) : 0);
        }
        function {{name("timeOnlyAddTicks")}}(value, ticks) {
          const day = {{name("timeOnlyTicksPerDay")}};
          let delta;
          if (Number.isSafeInteger(ticks)) delta = ticks % day;
          else {
            // A double outside the long range saturates like the CLR conversion before wrapping.
            const maximum = 9223372036854775807n, minimum = -9223372036854775808n;
            let exact = ticks === Infinity ? maximum : ticks === -Infinity ? minimum : BigInt(ticks);
            if (exact > maximum) exact = maximum;
            if (exact < minimum) exact = minimum;
            delta = Number(exact % BigInt(day));
          }
          return {{name("timeOnlyText")}}((({{name("timeOnlyTicks")}}(value) + delta) % day + day) % day);
        }
        function {{name("timeOnlyAdd")}}(value, milliseconds, scale) {
          const ticks = milliseconds * scale;
          return {{name("timeOnlyAddTicks")}}(value, Number.isNaN(ticks) ? 0 : scale === 10000 ? Math.round(ticks) : Math.trunc(ticks));
        }
        function {{name("timeOnlyIsBetween")}}(value, start, end) {
          return start <= end ? start <= value && end > value : start <= value || end > value;
        }
        function {{name("timeOnlySubtract")}}(left, right) {
          const difference = {{name("timeOnlyTicks")}}(left) - {{name("timeOnlyTicks")}}(right);
          return (difference < 0 ? difference + {{name("timeOnlyTicksPerDay")}} : difference) / 10000;
        }
        function {{name("timeOnlyFromTimeSpan")}}(milliseconds) {
          return {{name("timeOnlyFromTicks")}}(Math.round(milliseconds * 10000));
        }
        function {{name("timeOnlyFromDate")}}(input) {
          const value = new Date(input);
          return {{name("timeOnlyText")}}(((value.getUTCHours() * 60 + value.getUTCMinutes()) * 60 + value.getUTCSeconds()) * 10000000
            + value.getUTCMilliseconds() * 10000);
        }
        function {{name("timeOnlyParse")}}(value, pattern, fields) {
          if (value == null) throw new TypeError("Value cannot be null. (Parameter 's')");
          const match = pattern.exec(value);
          if (match) {
            let hour = 0, minute = 0, second = 0, fraction = 0;
            fields.forEach((field, index) => {
              const text = match[index + 1];
              if (field === "H") hour = Number(text);
              else if (field === "m") minute = Number(text);
              else if (field === "s") second = Number(text);
              else fraction = Number(text.padEnd(7, "0"));
            });
            if (hour <= 23 && minute <= 59 && second <= 59)
              return {{name("timeOnlyText")}}(((hour * 60 + minute) * 60 + second) * 10000000 + fraction);
          }
          throw new RangeError(`String '${value}' was not recognized as a valid TimeOnly.`);
        }

        """;
}
