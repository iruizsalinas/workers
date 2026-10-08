// A batch API must finish all submitted work before reporting failure or releasing its request.
public static class VerificationAsyncScenarios
{
    public static async Task<Dictionary<string, string>> RunAsync()
    {
        var results = new Dictionary<string, string>();
        results["discardedJobsStillRunOnce"] = DiscardedJobs();
        var events = new List<string>();
        try { await Task.WhenAll(CompleteLater(events), Fail("fast")); }
        catch (BatchFailure error) { events.Add(error.Message); }
        results["failedBatchWaitsForCompletedWork"] = string.Join(",", events);

        events.Clear();
        try { await Task.WhenAll(FailLater(events), Fail("fast")); }
        catch (BatchFailure error) { events.Add(error.Message); }
        results["genericFailureFollowsSubmissionOrder"] = string.Join(",", events);

        events.Clear();
        try { await Task.WhenAll(Fail("fast"), FailLater(events)); }
        catch (BatchFailure error) { events.Add(error.Message); }
        results["reversedGenericSubmissionOrder"] = string.Join(",", events);

        events.Clear();
        Task[] jobs = [FailLater(events), Fail("fast")];
        try { await Task.WhenAll(jobs); }
        catch (BatchFailure error) { events.Add(error.Message); }
        results["nonGenericFailureFollowsCompletionOrder"] = string.Join(",", events);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        events.Clear();
        try { await Task.WhenAll(Task.Delay(1, cancelled.Token), FailLater(events)); }
        catch (BatchFailure error) { events.Add(error.Message); }
        catch (Exception) { events.Add("cancelled"); }
        results["faultTakesPriorityOverCancellation"] = string.Join(",", events);

        var invalid = "accepted";
        try { _ = Task.WhenAll(new Task<int>[] { Task.FromResult(1), null! }); }
        catch (Exception) { invalid = "rejected"; }
        results["nullJobsRejectedBeforeStartingBatch"] = invalid;

        events.Clear();
        try { await Task.WhenAll(Task.Delay(1, cancelled.Token), CompleteLater(events)); }
        catch (Exception) { events.Add("cancelled"); }
        results["cancelledBatchWaitsForOtherJobs"] = string.Join(",", events);

        events.Clear();
        try { await Task.WhenAll(CancelledJob(), FailLater(events)); }
        catch (BatchFailure error) { events.Add(error.Message); }
        catch (Exception) { events.Add("cancelled"); }
        results["explicitCancellationDoesNotHideFailure"] = string.Join(",", events);

        var valid = await Task.WhenAll(Task.FromResult(3), Task.FromResult(1), Task.FromResult(2));
        results["successfulJobsKeepSubmissionOrder"] = string.Join(",", valid.Select(value => value.ToString()));
        results["emptyBatch"] = (await Task.WhenAll(new Task<int>[0])).Length.ToString();
        return results;
    }

    private static async Task<int> CompleteLater(List<string> events)
    {
        await Task.Delay(20);
        events.Add("completed");
        return 1;
    }

    private static string DiscardedJobs()
    {
        var events = new List<string>();
        _ = RecordJob(events, "first");
        var value = (_ = RecordJob(events, "second"));
        _ = new { Value = RecordJob(events, "third") };
        return $"{value}:{string.Join(",", events)}:{NamedUnderscore()}";
    }

    private static int RecordJob(List<string> events, string job)
    {
        events.Add(job);
        return events.Count;
    }

    private static int NamedUnderscore()
    {
        var _ = 1;
        _ = 2;
        return _;
    }

    private static async Task<int> FailLater(List<string> events)
    {
        await Task.Delay(20);
        events.Add("later");
        throw new BatchFailure("slow");
    }

    private static async Task<int> Fail(string message)
    {
        await Task.CompletedTask;
        throw new BatchFailure(message);
    }

    private static async Task<int> CancelledJob()
    {
        await Task.CompletedTask;
        throw new OperationCanceledException();
    }
}

public sealed class BatchFailure : Exception
{
    public BatchFailure(string message) : base(message) { }
}
