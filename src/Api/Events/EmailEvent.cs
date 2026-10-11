namespace Workers;

public sealed class ForwardableEmailMessage
{
    private ForwardableEmailMessage() { }

    public string From => WorkerApi.NotExecutable<string>();
    public string To => WorkerApi.NotExecutable<string>();
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    public ReadableStream Raw => WorkerApi.NotExecutable<ReadableStream>();
    public long RawSize => WorkerApi.NotExecutable<long>();

    public void SetReject(string reason) => WorkerApi.NotExecutable();
    public Task<EmailSendResult> ForwardAsync(string rcptTo, Headers? headers = null) => WorkerApi.NotExecutable<Task<EmailSendResult>>();
    /// <summary>Replies with a raw MIME message; set its <c>In-Reply-To</c> header to this message's <c>Message-ID</c> to thread it.</summary>
    public Task<EmailSendResult> ReplyAsync(EmailMessage message) => WorkerApi.NotExecutable<Task<EmailSendResult>>();
    /// <summary>Replies to the sender with a message built from its parts. One reply is allowed per message, and
    /// <see cref="EmailReplyMessageBuilder.From"/> must use the domain that received it.</summary>
    public Task<EmailSendResult> ReplyAsync(EmailReplyMessageBuilder builder) => WorkerApi.NotExecutable<Task<EmailSendResult>>();
}
