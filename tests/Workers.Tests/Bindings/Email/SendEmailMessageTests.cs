using System.Collections.ObjectModel;
using Xunit;

namespace Workers.Tests;

public sealed class SendEmailMessageTests
{
    [Fact]
    public void BuildCreatesReadOnlyCollectionSnapshots()
    {
        var builder = SendEmailMessage.Create(
                EmailAddress.Create("noreply@example.com", "Worker"),
                ["ada@example.com"],
                "Report")
            .Cc("ops@example.com")
            .Bcc("audit@example.com")
            .Header("x-worker", "dotnet")
            .Attachment(EmailAttachment.Text("report.txt", "text/plain", "hello"));

        var message = builder.Build();
        builder
            .Cc("late-cc@example.com")
            .Bcc("late-bcc@example.com")
            .Header("x-worker", "changed")
            .Attachment(EmailAttachment.Text("late.txt", "text/plain", "late"));

        Assert.Equal(["ada@example.com"], message.To);
        Assert.Equal(["ops@example.com"], message.Cc);
        Assert.Equal(["audit@example.com"], message.Bcc);
        Assert.Equal("dotnet", message.Headers["x-worker"]);
        Assert.Single(message.Attachments);

        Assert.IsType<ReadOnlyCollection<string>>(message.To);
        Assert.IsType<ReadOnlyCollection<string>>(message.Cc);
        Assert.IsType<ReadOnlyCollection<string>>(message.Bcc);
        Assert.IsType<ReadOnlyDictionary<string, string>>(message.Headers);
        Assert.IsType<ReadOnlyCollection<EmailAttachment>>(message.Attachments);
    }

    [Fact]
    public void CreateCopiesRecipientSequence()
    {
        var recipients = new List<string> { "ada@example.com" };

        var builder = SendEmailMessage.Create(
            EmailAddress.Create("noreply@example.com"),
            recipients,
            "Report");
        recipients[0] = "changed@example.com";

        var message = builder.Build();

        Assert.Equal(["ada@example.com"], message.To);
    }
}
