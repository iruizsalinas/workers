using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private static bool IsClassPrimaryParameter(IParameterSymbol parameter) =>
        parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is ParameterSyntax
            { Parent.Parent: ClassDeclarationSyntax };

    private string PrimaryParameterStorage(IParameterSymbol parameter, string receiver = "this") =>
        $"{receiver}.{_names.Get($"primary-parameter:{parameter.ContainingType.OriginalDefinition}:{parameter.Ordinal}", $"primary${parameter.ContainingType.Name}${parameter.Name}")}";

    private void EmitPrimaryParameterInitialization(TypeDeclarationSyntax declaration, bool defaults = false, string receiver = "this")
    {
        if (declaration is not ClassDeclarationSyntax { ParameterList: { } parameters }) return;
        foreach (var syntax in parameters.Parameters)
        {
            var parameter = (IParameterSymbol)_compilation.GetSemanticModel(syntax.SyntaxTree).GetDeclaredSymbol(syntax)!;
            _output.Append("    ").Append(PrimaryParameterStorage(parameter, receiver)).Append(" = ")
                .Append(defaults ? DefaultFieldValue(parameter.Type, syntax) : ParameterName(syntax)).AppendLine(";");
        }
    }

    private static SeparatedSyntaxList<ParameterSyntax> PrimaryParameters(TypeDeclarationSyntax declaration) => declaration switch
    {
        ClassDeclarationSyntax { ParameterList: { } list } => list.Parameters,
        RecordDeclarationSyntax { ParameterList: { } list } => list.Parameters,
        _ => default
    };
}
