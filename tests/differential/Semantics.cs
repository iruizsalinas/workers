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
    int LinqElementDefault,
    int[] LinqOrdered,
    int[] LinqNullableOrdered,
    int[] LinqDateOrdered,
    char[] LinqCharOrdered,
    int[] LinqGroupCounts,
    Dictionary<string, int> LinqDictionary,
    int LinqDictionaryValueSum,
    int[] LinqLookupValues,
    bool LinqLookupContains,
    int LinqLookupCount,
    int LinqSum,
    double LinqAverage,
    int LinqMinimum,
    int LinqMaximum,
    int LinqMinBy,
    int LinqMaxBy,
    int[] LinqUnion,
    int[] LinqIntersect,
    int[] LinqExcept,
    int[] LinqUnionBy,
    int[] LinqIntersectBy,
    int[] LinqExceptBy,
    int[] LinqReverse,
    int[] LinqDefaultIfEmpty,
    int[][] LinqChunks,
    int[] LinqZip,
    int LinqAggregate,
    int[] LinqJoin,
    int[] LinqGroupJoin,
    int SpanDays,
    int SpanHours,
    int SpanMinutes,
    int SpanSeconds,
    int SpanMilliseconds,
    double SpanTotalHours,
    double SpanFactoryMilliseconds,
    double SpanNegatedMilliseconds,
    bool SpanOrdered,
    bool SpanEqual,
    int SpanComparison,
    double DateSpanDifference,
    string DateSpanAddition,
    int?[] LinqNullableDistinct,
    int[] LinqNullableGroupCounts,
    string StringEmpty,
    bool StringNullOrEmpty,
    bool StringWhiteSpace,
    string StringJoined,
    string StringConcatenated,
    bool StringOrdinalIgnoreCase,
    bool StringNoExpansion,
    bool StringContainsIgnoreCase,
    int StringIndex,
    int StringLastIndex,
    string StringRemoved,
    string StringInserted,
    string StringPadded,
    int[] StringCharacters,
    string[] StringSplit,
    int ParsedInteger,
    uint ParsedUnsigned,
    float ParsedSingle,
    double ParsedDouble,
    bool ParsedBoolean,
    string FormattedDecimal,
    string FormattedHex,
    string FormattedFixed,
    int MathAbsolute,
    int MathClamped,
    double MathRoundedEvenDown,
    double MathRoundedEvenUp,
    double MathRoundedDigits,
    double MathTruncated,
    double MathPower,
    double MathLogarithm,
    int MathSign,
    float MathFloatRoot,
    string ParsedGuid,
    string ParsedHexGuid,
    string CompactGuid,
    string BracedGuid,
    string ParenthesizedGuid,
    bool EqualGuids,
    bool UnequalEmptyGuid);

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
        List<SortItem> sortItems = [
            new SortItem(2, 5, 1), new SortItem(1, 7, 2),
            new SortItem(1, 7, 3), new SortItem(1, 3, 4)
        ];
        var orderedItems = sortItems.OrderBy(item => item.Group).ThenByDescending(item => item.Score);
        sortItems.Add(new SortItem(1, 9, 5));
        List<NullableSortItem> nullableItems = [
            new NullableSortItem(2, 1), new NullableSortItem(null, 2), new NullableSortItem(1, 3)
        ];
        List<DateSortItem> dateItems = [
            new DateSortItem(laterInstant, 1), new DateSortItem(instant, 2)
        ];
        List<AggregateItem> aggregateItems = [
            new AggregateItem("a", 1, 3), new AggregateItem("a", 2, 1), new AggregateItem("b", 3, 2)
        ];
        var lookup = aggregateItems.ToLookup(item => item.Group, item => item.Value);
        var dictionary = words.ToDictionary(word => word, word => word.Length);
        var span = new TimeSpan(1, 2, 3, 4, 5);
        var factorySpan = TimeSpan.FromHours(1.5).Add(TimeSpan.FromMinutes(2)).Subtract(TimeSpan.FromSeconds(30));
        var spanEnd = instant + TimeSpan.FromMinutes(2);
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
            "😀".SequenceEqual("😀"), linqSource.ElementAt(1), linqSource.ElementAtOrDefault(-1),
            orderedItems.Select(item => item.Id).ToArray(),
            nullableItems.OrderBy(item => item.Key).Select(item => item.Id).ToArray(),
            dateItems.OrderBy(item => item.Instant).Select(item => item.Id).ToArray(),
            new List<char> { 'B', 'A', 'C' }.OrderBy(character => character).ToArray(),
            aggregateItems.GroupBy(item => item.Group).Select(group => group.Count()).ToArray(),
            dictionary, dictionary.Sum(pair => pair.Value),
            lookup["a"].ToArray(), lookup.Contains("b"), lookup.Count,
            linqSource.Sum(), linqSource.Average(), linqSource.Min(), linqSource.Max(),
            aggregateItems.MinBy(item => item.Score)!.Id, aggregateItems.MaxBy(item => item.Score)!.Id,
            repeated.Union(new List<int> { 3, 4 }).ToArray(),
            repeated.Intersect(new List<int> { 3, 1, 5 }).ToArray(),
            repeated.Except(new List<int> { 2 }).ToArray(),
            aggregateItems.UnionBy(new List<AggregateItem> { new AggregateItem("c", 4, 4) },
                item => item.Group).Select(item => item.Id).ToArray(),
            aggregateItems.IntersectBy(new List<string> { "a" }, item => item.Group)
                .Select(item => item.Id).ToArray(),
            aggregateItems.ExceptBy(new List<string> { "a" }, item => item.Group)
                .Select(item => item.Id).ToArray(),
            Enumerable.Reverse(linqSource).ToArray(), emptyInts.DefaultIfEmpty().ToArray(),
            linqSource.Chunk(2).ToArray(),
            linqSource.Zip(new List<int> { 10, 20 }, (left, right) => left + right).ToArray(),
            linqSource.Aggregate(10, (sum, value) => sum + value, sum => sum * 2),
            aggregateItems.Join(new List<string> { "a", "b" }, item => item.Group, group => group,
                (item, group) => item.Id).ToArray(),
            aggregateItems.GroupJoin(new List<string> { "a", "a", "b" }, item => item.Group, group => group,
                (item, groups) => groups.Count()).ToArray(),
            span.Days, span.Hours, span.Minutes, span.Seconds, span.Milliseconds, span.TotalHours,
            factorySpan.TotalMilliseconds, (-span).TotalMilliseconds,
            span > factorySpan, span == factorySpan, TimeSpan.Compare(span, factorySpan),
            (spanEnd - instant).TotalMilliseconds, spanEnd.ToString("O"),
            new List<int?> { 1, null, 1, null }.Distinct().ToArray(),
            new List<int?> { 1, null, 1 }.GroupBy(value => value)
                .Select(group => group.Count()).ToArray(),
            string.Empty, string.IsNullOrEmpty(null), string.IsNullOrWhiteSpace(" \t\u0085"),
            string.Join(",", words), string.Concat(words),
            string.Equals("Café", "CAFÉ", StringComparison.OrdinalIgnoreCase),
            string.Equals("Straße", "STRASSE", StringComparison.OrdinalIgnoreCase),
            "Alpha-Beta".Contains("BETA", StringComparison.OrdinalIgnoreCase),
            "Alpha-Beta".IndexOf("beta", StringComparison.OrdinalIgnoreCase),
            "Alpha-Beta".LastIndexOf("a", StringComparison.OrdinalIgnoreCase),
            "Alpha-Beta".Remove(5, 1), "Alpha".Insert(5, "-Beta"), "7".PadLeft(3, '0'),
            "😀".ToCharArray().Select(character => character + 0).ToArray(),
            " one , , two ".Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            int.Parse(" -42 "), uint.Parse("4294967295"), float.Parse("1.25e2"),
            double.Parse("-0.5"), bool.Parse("TrUe"),
            (-42).ToString("D5", System.Globalization.CultureInfo.InvariantCulture),
            (-42).ToString("X", System.Globalization.CultureInfo.InvariantCulture),
            1.25.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            Math.Abs(-42), Math.Clamp(12, 0, 10), Math.Round(2.5), Math.Round(3.5),
            Math.Round(1.2345, 2), Math.Truncate(-1.9), Math.Pow(2, 8), Math.Log(8, 2),
            Math.Sign(-2.0), MathF.Sqrt(9),
            Guid.Parse("{00112233-4455-6677-8899-AABBCCDDEEFF}").ToString(),
            Guid.Parse("{0x00112233,0x4455,0x6677,{0x88,0x99,0xaa,0xbb,0xcc,0xdd,0xee,0xff}}").ToString(),
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff").ToString("N"),
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff").ToString("B"),
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff").ToString("P"),
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
                == Guid.Parse("00112233445566778899AABBCCDDEEFF"),
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff") != Guid.Empty);
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

public sealed record SortItem(int Group, int Score, int Id);
public sealed record NullableSortItem(int? Key, int Id);
public sealed record DateSortItem(DateTimeOffset Instant, int Id);
public sealed record AggregateItem(string Group, int Id, int Score)
{
    public int Value => Id * 10;
}
