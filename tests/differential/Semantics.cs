public sealed record CoreSemanticsResult(
    int WrappedInteger,
    uint WrappedUnsigned,
    float RoundedSingle,
    string BooleanText,
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
        var total = 0;
        for (var index = 0; index < 4; index++)
            total += index;
        return new(signed, unsigned, single, $"{true}:{false}", total);
    }
}
