using System.Reflection;
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

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: options.Symbols);
        var trees = options.SourcePaths
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
            .ToArray();

        EmittedWorker worker;
        try
        {
            worker = WorkerCompiler.CompileWorker(trees, options.Reference is null ? [] : [options.Reference]);
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
        var outputDirectory = Path.GetDirectoryName(options.Output)!;
        var unsatisfied = NpmDependencies.Unsatisfied(worker.ImportedModules, outputDirectory);
        var installDirectory = NpmDependencies.InstallDirectory(options.Project, outputDirectory);
        if (options.NpmInstallArguments is { } arguments)
        {
            // The build runs npm install with these arguments, then compiles again to verify the result.
            Directory.CreateDirectory(Path.GetDirectoryName(arguments)!);
            File.WriteAllLines(arguments, unsatisfied.Count == 0 || installDirectory is null
                ? []
                : ["--prefix", installDirectory, .. unsatisfied.Select(requirement => requirement.Package.InstallSpec)]);
        }
        if (unsatisfied.Count != 0 && (options.NpmInstallArguments is null || installDirectory is null))
        {
            var command = $"npm install {string.Join(" ", unsatisfied.Select(requirement => requirement.Package.InstallSpec))}";
            error.WriteLine($"error WRK121: The Worker imports npm packages that Wrangler cannot resolve: "
                + $"{string.Join(", ", unsatisfied.Select(requirement => requirement.Problem))}. "
                + (installDirectory is null
                    ? $"Run '{command}' in '{outputDirectory}' or a directory above it."
                    : $"Run '{command}' in '{installDirectory}'."));
            return 1;
        }
        // A Worker is only written once the packages it imports resolve, so a failed install never
        // leaves a deployable artifact.
        if (unsatisfied.Count != 0) return 0;
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(options.Output, worker.Source);
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
          --npm-install-arguments <path>
                                      Write npm install arguments for missing or outdated packages
                                      the Worker imports, instead of failing.
          --help                      Show command-line help.
          --version                   Show compiler version.

        """;
}
