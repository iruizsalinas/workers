namespace Workers.Build;

/// <summary>Raised when Worker entrypoints cannot be discovered or validated.</summary>
internal sealed class EntrypointException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public EntrypointException(string message)
        : base(message)
    {
    }
}
