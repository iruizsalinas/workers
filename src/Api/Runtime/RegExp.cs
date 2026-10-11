namespace Workers;

/// <summary>A JavaScript regular expression, using JavaScript pattern syntax and flags.</summary>
public sealed class RegExp
{
    public RegExp(string pattern, string flags = "") => WorkerApi.NotExecutable();
    public string Source => WorkerApi.NotExecutable<string>();
    public string Flags => WorkerApi.NotExecutable<string>();
    public int LastIndex { get => WorkerApi.NotExecutable<int>(); set => WorkerApi.NotExecutable(); }
    public bool Test(string text) => WorkerApi.NotExecutable<bool>();
    public RegExpMatch? Exec(string text) => WorkerApi.NotExecutable<RegExpMatch?>();
    public string Replace(string text, string replacement) => WorkerApi.NotExecutable<string>();
    public string[] Split(string text) => WorkerApi.NotExecutable<string[]>();
    public override string ToString() => WorkerApi.NotExecutable<string>();
}

public sealed class RegExpMatch
{
    private RegExpMatch() { }
    public string? this[int index] => WorkerApi.NotExecutable<string?>();
    public int Index => WorkerApi.NotExecutable<int>();
    public string Input => WorkerApi.NotExecutable<string>();
    public int Length => WorkerApi.NotExecutable<int>();
    public IReadOnlyDictionary<string, string?>? Groups => WorkerApi.NotExecutable<IReadOnlyDictionary<string, string?>?>();
}
