using System.Text.RegularExpressions;

internal sealed record AspNetRoute(string Regex, IReadOnlyList<AspNetRouteParameter> Parameters)
{
    public static AspNetRoute Parse(string pattern)
    {
        var parameters = new List<AspNetRouteParameter>();
        var cursor = 0;
        var regex = "^";
        foreach (Match match in System.Text.RegularExpressions.Regex.Matches(pattern, "\\{([^}:?]+)(?::([^}?]+))?(\\?)?\\}"))
        {
            regex += System.Text.RegularExpressions.Regex.Escape(pattern[cursor..match.Index]);
            var name = match.Groups[1].Value;
            var constraint = match.Groups[2].Value;
            var optional = match.Groups[3].Success;
            var capture = constraint switch
            {
                "int" or "long" => "-?\\d+",
                "guid" => "[0-9a-fA-F-]{36}",
                _ => "[^/]+"
            };
            regex += optional ? $"(?:({capture}))?" : $"({capture})";
            parameters.Add(new AspNetRouteParameter(name, constraint, optional, parameters.Count + 1));
            cursor = match.Index + match.Length;
        }
        regex += System.Text.RegularExpressions.Regex.Escape(pattern[cursor..]);
        if (!pattern.EndsWith('/')) regex += "/?";
        regex += "$";
        return new AspNetRoute(regex, parameters);
    }
}

internal sealed record AspNetRouteParameter(string Name, string Constraint, bool Optional, int Group);
