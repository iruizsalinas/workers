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
    int LowerPropertyCollision);

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
        return new(signed, unsigned, single, -minimum, -unarySingle == -0.1f, $"{true}:{false}", true.ToString(),
            'A' + 1, 'B' - 'A', character + 5, character - otherCharacter, total,
            instant.Year, instant.Month, instant.Day, instant.DayOfWeek, instant.Hour, instant.Minute,
            instant.Second, instant.Millisecond, instant == sameInstant, instant < laterInstant,
            laterInstant.ToString("O"), collision.Value, propertyCollision.Value, propertyCollision.value);
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
