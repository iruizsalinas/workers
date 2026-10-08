// Independent checks of task batching and discard evaluation, including cancellation semantics.
public static class TaskReviewScenarios
{
    public static async Task<Dictionary<string, string>> RunAsync()
    {
        var results = new Dictionary<string, string>();
        var events = new List<string>();
        foreach (var reversed in new[] { false, true })
        {
            events.Clear();
            var slow = FailLater(events, "slow");
            var fast = Fail("fast");
            try { await Task.WhenAll(reversed ? new[] { fast, slow } : new[] { slow, fast }); }
            catch (Exception error) { events.Add(error.Message); }
            results[$"genericFaults-{reversed}"] = string.Join(",", events);
            events.Clear();
            slow = FailLater(events, "slow");
            fast = Fail("fast");
            try { await Task.WhenAll(reversed ? new Task[] { fast, slow } : new Task[] { slow, fast }); }
            catch (Exception error) { events.Add(error.Message); }
            results[$"nonGenericFaults-{reversed}"] = string.Join(",", events);
        }
        events.Clear();
        try { await Task.WhenAll(CancelByThrow(), FailLater(events, "fault")); }
        catch (Exception error) { events.Add(error.Message); }
        results["exceptionCancellationDoesNotMaskFault"] = string.Join(",", events);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        events.Clear();
        try { await Task.WhenAll(Task.Delay(1, cancelled.Token), FailLater(events, "fault")); }
        catch (Exception error) { events.Add(error.Message); }
        results["tokenCancellationDoesNotMaskFault"] = string.Join(",", events);

        var source = new List<int> { 3, 1, 2 }.Select(id => Task.FromResult(id));
        results["deferredTaskSources"] = string.Join(",", (await Task.WhenAll(source)).Select(id => id.ToString()));
        var nested = await Task.WhenAll(Task.WhenAll(Task.FromResult(3), Task.FromResult(1)),
            Task.WhenAll(Task.FromResult(2)));
        results["nestedBatches"] = string.Join("|", nested.Select(group => string.Join(",", group.Select(id => id.ToString()))));
        await Task.WhenAll();
        results["emptyNonGenericBatch"] = "completed";
        try { _ = Task.WhenAll((Task<int>[]?)null!); results["nullArray"] = "accepted"; }
        catch (Exception) { results["nullArray"] = "rejected"; }
        try { _ = Task.WhenAll((IEnumerable<Task<int>>)null!); results["nullEnumerable"] = "accepted"; }
        catch (Exception) { results["nullEnumerable"] = "rejected"; }
        results["discardContexts"] = DiscardContexts();
        events.Clear();
        try { _ = Task.WhenAll(JobCandidates(events)); }
        catch (Exception) { events.Add("rejected"); }
        results["genericNullJobStopsEnumeration"] = string.Join(",", events);
        events.Clear();
        IEnumerable<Task> nonGeneric = JobCandidates(events);
        try { _ = Task.WhenAll(nonGeneric); }
        catch (Exception) { events.Add("rejected"); }
        results["nonGenericNullJobEnumeration"] = string.Join(",", events);
        try { _ = Task.WhenAll(BadClosingCandidates()); }
        catch (Exception error) { results["invalidSourceStillCloses"] = error.Message; }
        return results;
    }

    private static IEnumerable<Task<int>> BadClosingCandidates()
    {
        try { yield return null!; }
        finally { throw new InvalidOperationException("closed-failure"); }
    }

    private static IEnumerable<Task<int>> JobCandidates(List<string> events)
    {
        try
        {
            events.Add("first");
            yield return Task.FromResult(1);
            events.Add("invalid");
            yield return null!;
            events.Add("later");
            yield return Task.FromResult(2);
        }
        finally { events.Add("closed"); }
    }

    private static string DiscardContexts()
    {
        var events = new List<int>();
        for (var index = 0; index < 2; index++) _ = Record(events, index);
        var assigned = (_ = Record(events, 2));
        Func<int, int> parameter = _ => (_ = Record(events, 3));
        var actualParameter = new[] { 9 }.Select(parameter).First();
        var holder = new TaskReviewHolder();
        holder._ = Record(events, 4);
        return $"{assigned} {actualParameter} {holder._} "
            + string.Join(",", events.Select(id => id.ToString()));
    }

    private static int Record(List<int> events, int id) { events.Add(id); return id; }
    private static async Task<int> Fail(string message)
    {
        await Task.CompletedTask;
        throw new InvalidOperationException(message);
    }
    private static async Task<int> FailLater(List<string> events, string message)
    {
        await Task.Delay(20);
        events.Add("finished");
        throw new InvalidOperationException(message);
    }
    private static async Task<int> CancelByThrow()
    {
        await Task.CompletedTask;
        throw new OperationCanceledException();
    }
}

public sealed class TaskReviewHolder
{
    public int _ { get; set; }
}
