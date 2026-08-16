using Microsoft.CodeAnalysis;

internal static class AspNetDiagnostic
{
    public static InvalidOperationException Unsupported(string code, SyntaxNode node, string message)
    {
        var position = node.GetLocation().GetLineSpan().StartLinePosition;
        return new InvalidOperationException(
            $"{code}: {message} ({node.SyntaxTree.FilePath}:{position.Line + 1}:{position.Character + 1})");
    }
}
