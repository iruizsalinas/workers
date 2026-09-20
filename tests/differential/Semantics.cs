public sealed record CoreSemanticsResult(
    int WrappedInteger,
    uint WrappedUnsigned,
    float RoundedSingle,
    int NegatedMinimum,
    bool NegatedSingleMatches,
    string BooleanText,
    string BooleanToString,
    int LoopTotal);

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
        var total = 0;
        for (var index = 0; index < 4; index++)
            total += index;
        return new(signed, unsigned, single, -minimum, -unarySingle == -0.1f, $"{true}:{false}", true.ToString(), total);
    }
}
