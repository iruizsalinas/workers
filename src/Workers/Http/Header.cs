namespace Workers;

/// <summary>A single HTTP header name-value pair.</summary>
/// <param name="Name">The header name.</param>
/// <param name="Value">The header value.</param>
public sealed record Header(string Name, string Value);
