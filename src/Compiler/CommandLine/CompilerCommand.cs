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

        var module = WorkerCompiler.Compile(trees, options.Reference is null ? [] : [options.Reference]);
        Directory.CreateDirectory(Path.GetDirectoryName(options.Output)!);
        File.WriteAllText(options.Output, module);
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
          --help                      Show command-line help.
          --version                   Show compiler version.

        """;
}
