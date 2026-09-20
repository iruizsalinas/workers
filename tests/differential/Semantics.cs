public sealed record CoreSemanticsResult(
    int WrappedInteger,
    uint WrappedUnsigned,
    float RoundedSingle,
    int NegatedMinimum,
    bool NegatedSingleMatches,
    string BooleanText,
    string BooleanToString,
    int CharacterLiteralAdd,
    int CharacterLiteralDifference,
    int CharacterVariableAdd,
    int CharacterVariableDifference,
    int LoopTotal,
    int CalendarYear,
    int CalendarMonth,
    int CalendarDay,
    DayOfWeek CalendarDayOfWeek,
    int CalendarHour,
    int CalendarMinute,
    int CalendarSecond,
    int CalendarMillisecond,
    bool EqualInstants,
    bool OrderedInstants,
    string AddedInstant,
    int FieldPropertyCollision,
    int UpperPropertyCollision,
    int LowerPropertyCollision,
    string AddedMonth,
    string AddedYear,
    bool PreEpochSecondsFloor,
    bool UnixSecondsConversion,
    int DateYear,
    DayOfWeek DateDayOfWeek,
    bool EqualDates,
    string DateRoundTrip,
    string DateInterpolation,
    string OffsetInterpolation,
    int OffsetDayOfYear,
    int DateDayOfYear,
    bool LeapYear,
    int LeapFebruaryDays,
    int OffsetStaticCompare,
    int DateStaticCompare,
    int InstanceCompare,
    bool InstanceEquals,
    bool UnixEpochIsZero,
    string UnspecifiedDateTime,
    string UniversalTime,
    int[] LinqProjection,
    int LinqRepeatedCount,
    bool LinqAny,
    bool LinqAll,
    int LinqCount,
    bool LinqContains,
    int LinqFirst,
    int LinqLast,
    int LinqSingle,
    int LinqIntDefault,
    bool LinqBoolDefault,
    int LinqStringCount,
    int[] LinqStringValues,
    int[] LinqSelectMany,
    int[] LinqSelectManyResult,
    int[] LinqWhile,
    int[] LinqDistinct,
    string[] LinqDistinctBy,
    bool LinqSequenceEqual,
    int LinqElementAt,
    int LinqElementDefault);

public static class CoreSemantics
{
    public static CoreSemanticsResult Run()
    {
        var signed = int.MaxValue;
        signed++;
        var unsigned = uint.MaxValue;
        unsigned++;
        var single = 16_777_216f;
        single += 1f;
        var minimum = int.MinValue;
        var unarySingle = 0.1f;
        var character = 'C';
        var otherCharacter = 'A';
        var total = 0;
        for (var index = 0; index < 4; index++)
            total += index;
        var instant = new DateTimeOffset(2024, 2, 29, 23, 58, 57, 123, TimeSpan.Zero);
        var sameInstant = new DateTimeOffset(2024, 2, 29, 23, 58, 57, 123, TimeSpan.Zero);
        var laterInstant = instant.AddMinutes(2);
        var collision = new NamingCollision();
        var propertyCollision = new PropertyCollision { Value = 3, value = 4 };
        var monthEnd = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
        var leapDay = new DateTimeOffset(2024, 2, 29, 12, 30, 0, TimeSpan.Zero);
        var date = leapDay.Date;
        var sameDate = new DateTimeOffset(2024, 2, 29, 23, 59, 59, TimeSpan.Zero).Date;
        var linqSource = new List<int> { 1, 2, 3 };
        var lazyQuery = linqSource.Where(value => value % 2 == 1)
            .Select((value, index) => value + index);
        linqSource.Add(5);
        List<int> linqTail = [9];
        List<int> emptyInts = [];
        List<bool> emptyBools = [];
        List<List<int>> linqGroups = [new List<int> { 1, 2 }, new List<int> { 3 }];
        List<int> repeated = [1, 2, 1, 3];
        List<string> words = ["a", "b", "cc", "dd"];
        return new(signed, unsigned, single, -minimum, -unarySingle == -0.1f, $"{true}:{false}", true.ToString(),
            'A' + 1, 'B' - 'A', character + 5, character - otherCharacter, total,
            instant.Year, instant.Month, instant.Day, instant.DayOfWeek, instant.Hour, instant.Minute,
            instant.Second, instant.Millisecond, instant == sameInstant, instant < laterInstant,
            laterInstant.ToString("O"), collision.Value, propertyCollision.Value, propertyCollision.value,
            monthEnd.AddMonths(1).ToString("O"), leapDay.AddYears(1).ToString("O"),
            DateTimeOffset.FromUnixTimeMilliseconds(-1).ToUnixTimeSeconds() == -1,
            DateTimeOffset.FromUnixTimeSeconds(-1).ToUnixTimeMilliseconds() == -1000,
            date.Year, date.DayOfWeek, date == sameDate, date.ToString("O"), $"{date:O}", $"{leapDay:O}",
            leapDay.DayOfYear, date.DayOfYear, DateTime.IsLeapYear(2024), DateTime.DaysInMonth(2024, 2),
            DateTimeOffset.Compare(instant, laterInstant), DateTime.Compare(date, sameDate),
            instant.CompareTo(laterInstant), instant.Equals(sameInstant),
            DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds() == 0,
            leapDay.DateTime.ToString("O"), leapDay.ToUniversalTime().ToString("O"),
            lazyQuery.Skip(1).Take(2).Concat(linqTail).ToArray(),
            lazyQuery.Count() + lazyQuery.Count(), linqSource.Any(value => value > 4),
            linqSource.All(value => value > 0), linqSource.Count(value => value % 2 == 1),
            linqSource.Contains(3), linqSource.First(), linqSource.Last(),
            linqSource.Single(value => value == 2), emptyInts.FirstOrDefault(), emptyBools.LastOrDefault(),
            "😀".Count(), "😀".Select(character => character + 0).ToArray(),
            linqGroups.SelectMany((group, index) => group.Select(value => value + index))
                .Prepend(0).Append(8).ToArray(),
            linqGroups.SelectMany(group => group, (group, value) => value + group.Count).ToArray(),
            linqSource.SkipWhile((value, index) => value <= index + 1).TakeWhile(value => value <= 5).ToArray(),
            repeated.Distinct().ToArray(), words.DistinctBy(word => word.Length).ToArray(),
            "😀".SequenceEqual("😀"), linqSource.ElementAt(1), linqSource.ElementAtOrDefault(-1));
    }
}

public sealed class NamingCollision
{
    private int value = 2;
    public int Value => value * 2;
}

public sealed class PropertyCollision
{
    public int Value { get; init; }
    public int value { get; init; }
}
