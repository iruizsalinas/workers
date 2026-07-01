using System.Collections.ObjectModel;
using Xunit;

namespace Workers.Tests;

public sealed class ContextTests
{
    [Fact]
    public void PendingTasksReturnsLiveReadOnlyView()
    {
        var context = new Context();
        var pendingTasks = context.PendingTasks;
        var task = Task.CompletedTask;

        context.WaitUntil(task);

        Assert.Same(task, Assert.Single(pendingTasks));
        Assert.IsType<ReadOnlyCollection<Task>>(pendingTasks);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<Task>>(pendingTasks).Add(Task.CompletedTask));
    }
}
