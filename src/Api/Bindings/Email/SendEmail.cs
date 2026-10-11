namespace Workers;

/// <summary>A <c>send_email</c> binding.</summary>
public sealed class SendEmail
{
    private SendEmail() { }

    /// <summary>Sends a raw MIME message.</summary>
    public Task<EmailSendResult> SendAsync(EmailMessage message) => WorkerApi.NotExecutable<Task<EmailSendResult>>();
    /// <summary>Builds and sends a message from its parts.</summary>
    public Task<EmailSendResult> SendAsync(EmailMessageBuilder builder) => WorkerApi.NotExecutable<Task<EmailSendResult>>();
}

/// <summary>A raw MIME message, as constructed by <c>new EmailMessage(from, to, raw)</c> from <c>cloudflare:email</c>.</summary>
public sealed class EmailMessage
{
    public EmailMessage(string from, string to, string raw) => WorkerApi.NotExecutable();
    public EmailMessage(string from, string to, ReadableStream raw) => WorkerApi.NotExecutable();

    public string From => WorkerApi.NotExecutable<string>();
    public string To => WorkerApi.NotExecutable<string>();
}

/// <summary>A reply built from its parts, for <see cref="ForwardableEmailMessage.ReplyAsync(EmailReplyMessageBuilder)"/>.</summary>
public class EmailReplyMessageBuilder
{
    public required EmailAddressInit From { get; init; }
    public required string Subject { get; init; }
    public EmailAddressInit? ReplyTo { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
    public string? Text { get; init; }
    public string? Html { get; init; }
    public IReadOnlyList<EmailAttachment>? Attachments { get; init; }
}

/// <summary>A message built from its parts. At least one of <see cref="To"/>, <see cref="Cc"/> or <see cref="Bcc"/> is required.</summary>
public sealed class EmailMessageBuilder : EmailReplyMessageBuilder
{
    public IReadOnlyList<EmailAddressInit>? To { get; init; }
    public IReadOnlyList<EmailAddressInit>? Cc { get; init; }
    public IReadOnlyList<EmailAddressInit>? Bcc { get; init; }
}

/// <summary>An address with a display name.</summary>
public sealed class EmailAddress
{
    public required string Name { get; init; }
    public required string Email { get; init; }
}

/// <summary>An email address: a string such as <c>"team@example.com"</c>, or an <see cref="EmailAddress"/> with a display name.</summary>
public sealed class EmailAddressInit
{
    private EmailAddressInit() { }

    public static implicit operator EmailAddressInit(string address) => WorkerApi.NotExecutable<EmailAddressInit>();
    public static implicit operator EmailAddressInit(EmailAddress address) => WorkerApi.NotExecutable<EmailAddressInit>();
}

/// <summary>A file attached to a message. Inline attachments are referenced from HTML as <c>cid:</c> plus their <see cref="ContentId"/>.</summary>
public sealed class EmailAttachment
{
    public required EmailAttachmentDisposition Disposition { get; init; }
    /// <summary>Required for inline attachments; not allowed for regular ones.</summary>
    public string? ContentId { get; init; }
    public required string Filename { get; init; }
    /// <summary>The media type, such as <c>image/png</c>.</summary>
    public required string Type { get; init; }
    public required EmailAttachmentContent Content { get; init; }
}

public enum EmailAttachmentDisposition
{
    Attachment,
    Inline
}

/// <summary>Attachment data: text, or bytes.</summary>
public sealed class EmailAttachmentContent
{
    private EmailAttachmentContent() { }

    public static implicit operator EmailAttachmentContent(string text) => WorkerApi.NotExecutable<EmailAttachmentContent>();
    public static implicit operator EmailAttachmentContent(byte[] bytes) => WorkerApi.NotExecutable<EmailAttachmentContent>();
    public static implicit operator EmailAttachmentContent(ReadOnlyMemory<byte> bytes) => WorkerApi.NotExecutable<EmailAttachmentContent>();
}

public sealed class EmailSendResult
{
    private EmailSendResult() { }

    public string MessageId => WorkerApi.NotExecutable<string>();
}
