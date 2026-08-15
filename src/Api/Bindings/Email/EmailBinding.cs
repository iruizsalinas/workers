namespace Workers;

public interface ISendEmailBinding : IBinding
{
    Task<EmailSendResult> SendAsync(SendEmailMessage message, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendRawAsync(string from, string to, ReadOnlyMemory<byte> raw, CancellationToken cancellationToken = default);
}

public sealed record EmailSendResult(string MessageId);
public sealed record SendEmailMessage(string From, IReadOnlyList<string> To, string Subject, string? Text = null, string? Html = null);
public sealed record EmailAddress(string Email, string? Name = null);
public enum EmailAttachmentDisposition
{
    Attachment,
    Inline
}

public sealed record EmailAttachment(
    string Filename,
    string ContentType,
    ReadOnlyMemory<byte> Content,
    EmailAttachmentDisposition Disposition = EmailAttachmentDisposition.Attachment,
    string? ContentId = null);
public sealed class SendEmailMessageBuilder;
