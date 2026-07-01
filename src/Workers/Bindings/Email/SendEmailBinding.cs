using System.Collections.ObjectModel;
using System.Text.Json;

namespace Workers;

/// <summary>The result of sending an email through a Send Email binding.</summary>
public sealed record EmailSendResult(string MessageId);

/// <summary>An email address with an optional display name.</summary>
public sealed record EmailAddress(string Email, string? Name = null)
{
    /// <summary>Creates an email address.</summary>
    public static EmailAddress Create(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return new EmailAddress(email, name);
    }
}

/// <summary>The disposition of an email attachment.</summary>
public enum EmailAttachmentDisposition
{
    /// <summary>An attachment disposition.</summary>
    Attachment,

    /// <summary>An inline disposition.</summary>
    Inline
}

/// <summary>An email attachment.</summary>
public sealed record EmailAttachment
{
    private EmailAttachment(
        EmailAttachmentDisposition disposition,
        string filename,
        string contentType,
        string? textContent,
        string? bodyBase64,
        string? contentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Disposition = disposition;
        Filename = filename;
        ContentType = contentType;
        TextContent = textContent;
        BodyBase64 = bodyBase64;
        ContentId = contentId;
    }

    /// <summary>The attachment disposition.</summary>
    public EmailAttachmentDisposition Disposition { get; }

    /// <summary>The attachment filename.</summary>
    public string Filename { get; }

    /// <summary>The attachment content type.</summary>
    public string ContentType { get; }

    /// <summary>The textual content, when this is a text attachment.</summary>
    public string? TextContent { get; }

    /// <summary>The base64-encoded binary content, when this is a binary attachment.</summary>
    public string? BodyBase64 { get; }

    /// <summary>The content id for inline attachments.</summary>
    public string? ContentId { get; }

    /// <summary>Creates a regular text attachment.</summary>
    public static EmailAttachment Text(string filename, string contentType, string content) =>
        new(EmailAttachmentDisposition.Attachment, filename, contentType, RequireContent(content), bodyBase64: null, contentId: null);

    /// <summary>Creates a regular binary attachment.</summary>
    public static EmailAttachment Bytes(string filename, string contentType, ReadOnlySpan<byte> content) =>
        new(EmailAttachmentDisposition.Attachment, filename, contentType, textContent: null, Convert.ToBase64String(content), contentId: null);

    /// <summary>Creates an inline text attachment.</summary>
    public static EmailAttachment InlineText(string contentId, string filename, string contentType, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        return new EmailAttachment(EmailAttachmentDisposition.Inline, filename, contentType, RequireContent(content), bodyBase64: null, contentId);
    }

    /// <summary>Creates an inline binary attachment.</summary>
    public static EmailAttachment InlineBytes(string contentId, string filename, string contentType, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        return new EmailAttachment(EmailAttachmentDisposition.Inline, filename, contentType, textContent: null, Convert.ToBase64String(content), contentId);
    }

    private static string RequireContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return content;
    }
}

/// <summary>A structured email message for the Workers Send Email binding.</summary>
public sealed record SendEmailMessage
{
    private SendEmailMessage(
        EmailAddress from,
        IReadOnlyList<string> to,
        string subject,
        EmailAddress? replyTo,
        IReadOnlyList<string> cc,
        IReadOnlyList<string> bcc,
        IReadOnlyDictionary<string, string> headers,
        string? text,
        string? html,
        IReadOnlyList<EmailAttachment> attachments)
    {
        From = from;
        To = CopyList(to);
        Subject = subject;
        ReplyTo = replyTo;
        Cc = CopyList(cc);
        Bcc = CopyList(bcc);
        Headers = CopyHeaders(headers);
        Text = text;
        Html = html;
        Attachments = CopyList(attachments);
    }

    /// <summary>The sender address.</summary>
    public EmailAddress From { get; }

    /// <summary>The recipient addresses.</summary>
    public IReadOnlyList<string> To { get; }

    /// <summary>The email subject.</summary>
    public string Subject { get; }

    /// <summary>The reply-to address.</summary>
    public EmailAddress? ReplyTo { get; }

    /// <summary>The carbon-copy recipients.</summary>
    public IReadOnlyList<string> Cc { get; }

    /// <summary>The blind-carbon-copy recipients.</summary>
    public IReadOnlyList<string> Bcc { get; }

    /// <summary>Additional headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>The text body.</summary>
    public string? Text { get; }

    /// <summary>The HTML body.</summary>
    public string? Html { get; }

    /// <summary>The attachments.</summary>
    public IReadOnlyList<EmailAttachment> Attachments { get; }

    /// <summary>Creates a structured email builder.</summary>
    public static SendEmailMessageBuilder Create(string from, string to, string subject) =>
        new(EmailAddress.Create(from), [RequireAddress(to)], subject);

    /// <summary>Creates a structured email builder.</summary>
    public static SendEmailMessageBuilder Create(EmailAddress from, IEnumerable<string> to, string subject) =>
        new(from, to.Select(RequireAddress).ToArray(), subject);

    internal static SendEmailMessage FromBuilder(
        EmailAddress from,
        IReadOnlyList<string> to,
        string subject,
        EmailAddress? replyTo,
        IReadOnlyList<string> cc,
        IReadOnlyList<string> bcc,
        IReadOnlyDictionary<string, string> headers,
        string? text,
        string? html,
        IReadOnlyList<EmailAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (to.Count == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(to));

        return new SendEmailMessage(from, to, subject, replyTo, cc, bcc, headers, text, html, attachments);
    }

    internal static string RequireAddress(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }

    private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new ReadOnlyCollection<T>(values.ToArray());
    }

    private static IReadOnlyDictionary<string, string> CopyHeaders(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>Builds structured email messages.</summary>
public sealed class SendEmailMessageBuilder
{
    private readonly EmailAddress _from;
    private readonly IReadOnlyList<string> _to;
    private readonly string _subject;
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _cc = [];
    private readonly List<string> _bcc = [];
    private readonly List<EmailAttachment> _attachments = [];
    private EmailAddress? _replyTo;
    private string? _text;
    private string? _html;

    internal SendEmailMessageBuilder(EmailAddress from, IReadOnlyList<string> to, string subject)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        _from = from;
        _to = to;
        _subject = subject;
    }

    /// <summary>Sets the reply-to address.</summary>
    public SendEmailMessageBuilder ReplyTo(string email, string? name = null)
    {
        _replyTo = EmailAddress.Create(email, name);
        return this;
    }

    /// <summary>Adds carbon-copy recipients.</summary>
    public SendEmailMessageBuilder Cc(params string[] recipients)
    {
        AddAddresses(_cc, recipients);
        return this;
    }

    /// <summary>Adds blind-carbon-copy recipients.</summary>
    public SendEmailMessageBuilder Bcc(params string[] recipients)
    {
        AddAddresses(_bcc, recipients);
        return this;
    }

    /// <summary>Sets a header.</summary>
    public SendEmailMessageBuilder Header(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _headers[name] = value;
        return this;
    }

    /// <summary>Sets the text body.</summary>
    public SendEmailMessageBuilder Text(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _text = value;
        return this;
    }

    /// <summary>Sets the HTML body.</summary>
    public SendEmailMessageBuilder Html(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _html = value;
        return this;
    }

    /// <summary>Adds an attachment.</summary>
    public SendEmailMessageBuilder Attachment(EmailAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        return this;
    }

    /// <summary>Builds the message.</summary>
    public SendEmailMessage Build() =>
        SendEmailMessage.FromBuilder(
            _from,
            _to,
            _subject,
            _replyTo,
            _cc.ToArray(),
            _bcc.ToArray(),
            new Dictionary<string, string>(_headers, StringComparer.OrdinalIgnoreCase),
            _text,
            _html,
            _attachments.ToArray());

    private static void AddAddresses(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
            target.Add(SendEmailMessage.RequireAddress(value));
    }
}

internal sealed class SendEmailBinding : ISendEmailBinding
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public SendEmailBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<EmailSendResult> SendRawAsync(
        string from,
        string to,
        string raw,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(raw);

        return SendAsync("email.sendRaw", new { from, to, raw }, cancellationToken);
    }

    public Task<EmailSendResult> SendAsync(SendEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendAsync("email.send", new { message }, cancellationToken);
    }

    private async Task<EmailSendResult> SendAsync(
        string operation,
        object payload,
        CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<EmailSendResult>(result, JsonOptions)
            ?? throw new WorkersException("Send Email returned an empty result.");
    }
}
