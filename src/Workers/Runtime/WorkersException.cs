namespace Workers;

/// <summary>Represents an SDK-level failure before a platform response can be produced.</summary>
public sealed class WorkersException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    public WorkersException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    public WorkersException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
