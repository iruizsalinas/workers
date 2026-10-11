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

public sealed class EmailMessageBuilder
{
    public required string From { get; init; }
    public required string Subject { get; init; }
    public IReadOnlyList<string>? To { get; init; }
    public IReadOnlyList<string>? Cc { get; init; }
    public IReadOnlyList<string>? Bcc { get; init; }
    public string? ReplyTo { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
    public string? Text { get; init; }
    public string? Html { get; init; }
}

public sealed class EmailSendResult
{
    private EmailSendResult() { }

    public string MessageId => WorkerApi.NotExecutable<string>();
}
