using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed record AspNetApplication(
    CSharpCompilation Compilation,
    IReadOnlyList<AspNetEndpoint> Endpoints);

internal sealed record AspNetEndpoint(
    string HttpMethod,
    string Pattern,
    IReadOnlyList<ParameterSyntax> Parameters,
    ExpressionSyntax? ExpressionBody,
    BlockSyntax? Block,
    SemanticModel SemanticModel);
