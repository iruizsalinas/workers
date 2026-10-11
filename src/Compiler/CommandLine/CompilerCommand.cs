using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class CompilerCommand
{
    public static int Run(string[] args, TextWriter? output = null, TextWriter? error = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            output.Write(HelpText);
            return 0;
        }
        if (args.Contains("--version", StringComparer.Ordinal))
        {
            output.WriteLine(Version);
            return 0;
        }

        CompilerOptions options;
        try
        {
            options = CompilerOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine($"error: {exception.Message}");
            error.WriteLine("Run 'Workers.Compiler --help' for usage.");
            return 2;
        }

        var compilerArguments = options.CompilerArguments is null ? null : CSharpCommandLineParser.Default.Parse(
            CSharpCommandLineParser.SplitCommandLineIntoArguments(File.ReadAllText(options.CompilerArguments), removeHashComments: false),
            options.Project, sdkDirectory: null);
        if (compilerArguments?.Errors.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) == true)
        {
            foreach (var diagnostic in compilerArguments.Errors) error.WriteLine(diagnostic);
            return 1;
        }
        var parseOptions = compilerArguments?.ParseOptions
            ?? new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: options.Symbols);
        var trees = options.SourcePaths
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
            .ToArray();

        string source;
        try
        {
            source = compilerArguments is null
                ? WorkerCompiler.Compile(trees, options.Reference is null ? [] : [options.Reference])
                : WorkerCompiler.Compile(CSharpCompilation.Create("Workers.UserCode", trees,
                    compilerArguments.MetadataReferences.Select(reference => MetadataReference.CreateFromFile(
                        Path.GetFullPath(reference.Reference, options.Project), reference.Properties)),
                    // CoreCompile already enforced warning policy, including analyzer configs.
                    // Replay language semantics, but do not promote warnings without those configs.
                    compilerArguments.CompilationOptions.WithGeneralDiagnosticOption(ReportDiagnostic.Default)
                        .WithSpecificDiagnosticOptions([])));
        }
        catch (CompilationFailedException exception)
        {
            foreach (var diagnostic in exception.Diagnostics)
                error.WriteLine(diagnostic.ToString());
            return 1;
        }
        catch (NotSupportedException exception)
        {
            error.WriteLine(WorkerDiagnostics.Format(exception));
            return 1;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(options.Output)!);
        File.WriteAllText(options.Output, source);
        return 0;
    }

    internal static string Version
    {
        get
        {
            var informationalVersion = typeof(CompilerCommand).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            return informationalVersion?.Split('+', 2)[0] ?? "unknown";
        }
    }

    internal static string HelpText => $$"""
        Workers.Compiler {{Version}}
        Compile C# source files to a Cloudflare Workers JavaScript module.

        Usage:
          Workers.Compiler --project <path> --output <path> [options]

        Options:
          --project <path>            Project directory and source path base. Required.
          --output <path>             Output JavaScript module path. Required.
          --sources <path>            File containing one C# source path per line.
          --reference <path>          Workers API assembly reference.
          --define <symbols>          Preprocessor symbols separated by commas or semicolons.
          --compiler-arguments <path> MSBuild C# compiler arguments; preserves project references and options.
          --help                      Show command-line help.
          --version                   Show compiler version.

        """;
}
