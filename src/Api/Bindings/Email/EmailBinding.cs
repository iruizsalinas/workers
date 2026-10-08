namespace Workers;

public interface ISendEmailBinding : IBinding
{
    Task<EmailSendResult> SendAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendRawAsync(string from, string to, ReadOnlyMemory<byte> raw, CancellationToken cancellationToken = default);
}

public sealed record EmailSendResult(string MessageId);
public sealed record SendEmailMessage(string From, IReadOnlyList<string> To, string Subject, string? Text = null, string? Html = null);
