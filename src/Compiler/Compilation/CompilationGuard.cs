using Microsoft.CodeAnalysis;

internal static class CompilationGuard
{
    public static void ThrowIfInvalid(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
            throw new InvalidOperationException("WRK002: C# compilation failed:\n" + string.Join("\n", errors));
    }
}
