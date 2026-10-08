using Microsoft.CodeAnalysis;

internal static class CompilationGuard
{
    public static void ThrowIfInvalid(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
            throw new CompilationFailedException(errors);
    }
}

internal sealed class CompilationFailedException(IReadOnlyList<Diagnostic> diagnostics)
    : InvalidOperationException("WRK002: C# compilation failed:\n" + string.Join("\n", diagnostics))
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; } = diagnostics;
}
