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
    public Task<EmailSendResult> ReplyAsync(EmailMessage message) => WorkerApi.NotExecutable<Task<EmailSendResult>>();
}
