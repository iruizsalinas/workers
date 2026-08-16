using Microsoft.CodeAnalysis.CSharp;

internal static class CompilerCommand
{
    public static int Run(string[] args)
    {
        var options = CompilerOptions.Parse(args);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: options.Symbols);
        var trees = options.SourcePaths
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
            .ToArray();

        var references = options.References
            .Concat(options.Reference is null ? [] : [options.Reference]);
        var module = WorkerCompiler.Compile(trees, references, useTrustedPlatformAssemblies: options.References.Count == 0);
        Directory.CreateDirectory(Path.GetDirectoryName(options.Output)!);
        File.WriteAllText(options.Output, module);
        return 0;
    }
}
