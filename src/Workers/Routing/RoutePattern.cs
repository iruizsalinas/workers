namespace Workers;

internal sealed class RoutePattern
{
    private readonly Segment[] _segments;

    private RoutePattern(Segment[] segments)
    {
        _segments = segments;
    }

    public static RoutePattern Parse(string pattern)
    {
        if (!pattern.StartsWith('/'))
            throw new ArgumentException("Route patterns must start with '/'.", nameof(pattern));

        var parts = Split(pattern);
        var segments = new Segment[parts.Length];

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            segments[index] = part switch
            {
                ['*', .. var name] when name.Length > 0 && index == parts.Length - 1 => Segment.Wildcard(name),
                [':', .. var name] when name.Length > 0 => Segment.Parameter(name),
                _ => Segment.Literal(part)
            };
        }

        return new RoutePattern(segments);
    }

    public bool TryMatch(string path, out Dictionary<string, string> parameters)
    {
        parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        var parts = Split(path);
        var hasWildcard = _segments.Length > 0 && _segments[^1].Kind == SegmentKind.Wildcard;
        if ((!hasWildcard && parts.Length != _segments.Length) || (hasWildcard && parts.Length < _segments.Length - 1))
            return false;

        for (var index = 0; index < _segments.Length; index++)
        {
            var segment = _segments[index];

            if (segment.Kind == SegmentKind.Wildcard)
            {
                parameters[segment.Value] = string.Join('/', parts[index..]);
                return true;
            }

            if (index >= parts.Length)
                return false;

            if (segment.Kind == SegmentKind.Parameter)
            {
                parameters[segment.Value] = Uri.UnescapeDataString(parts[index]);
                continue;
            }

            if (!string.Equals(segment.Value, parts[index], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string[] Split(string path) =>
        path.Trim('/').Length == 0 ? [] : path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private readonly record struct Segment(SegmentKind Kind, string Value)
    {
        public static Segment Literal(string value) => new(SegmentKind.Literal, value);

        public static Segment Parameter(string value) => new(SegmentKind.Parameter, value);

        public static Segment Wildcard(string value) => new(SegmentKind.Wildcard, value);
    }

    private enum SegmentKind
    {
        Literal,
        Parameter,
        Wildcard
    }
}
