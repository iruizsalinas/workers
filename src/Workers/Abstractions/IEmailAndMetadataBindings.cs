namespace Workers;

/// <summary>Represents a Workers Send Email binding.</summary>
public interface ISendEmailBinding : IBinding
{
    /// <summary>Sends a structured email message.</summary>
    Task<EmailSendResult> SendAsync(SendEmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>Sends a raw MIME email message.</summary>
    Task<EmailSendResult> SendRawAsync(
        string from,
        string to,
        string raw,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents a Workers Version Metadata binding.</summary>
public interface IVersionMetadataBinding : IBinding
{
    /// <summary>Reads the current Worker version metadata.</summary>
    Task<VersionMetadata> GetAsync(CancellationToken cancellationToken = default);
}
