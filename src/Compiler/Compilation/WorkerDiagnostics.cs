using Microsoft.CodeAnalysis;

// Compiler diagnostics are reported in the canonical MSBuild format so IDEs and CI annotate the
// offending source line: path(line,column): error CODE: message
internal static class WorkerDiagnostics
{
    private const string LocationKey = "Workers.Location";

    public static TException WithLocation<TException>(TException exception, SyntaxNode? node)
        where TException : Exception
    {
        if (node is not null && !exception.Data.Contains(LocationKey))
            exception.Data[LocationKey] = node.GetLocation();
        return exception;
    }

    public static string Format(Exception exception)
    {
        var message = exception.Message;
        var code = "WRK000";
        var separator = message.IndexOf(": ", StringComparison.Ordinal);
        if (separator > 0 && message.StartsWith("WRK", StringComparison.Ordinal))
        {
            code = message[..separator];
            message = message[(separator + 2)..];
        }
        message = string.Join(" ", message.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        if (message.Length > 400) message = message[..400] + "...";
        if (exception.Data[LocationKey] is Location { IsInSource: true } location)
        {
            var span = location.GetLineSpan();
            return $"{span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): error {code}: {message}";
        }
        return $"error {code}: {message}";
    }
}
