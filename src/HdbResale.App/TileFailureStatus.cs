namespace HdbResale.App;

internal sealed class TileFailureStatus
{
    private ulong exhaustedRequests;
    public bool RepeatedFailures => exhaustedRequests >= 2;

    // Exhausted requests have already retried; latch the notice for this launch.
    // A later successful tile must not erase the fact that other tiles failed.
    public bool Observe(ulong count)
    {
        var previous = RepeatedFailures;
        exhaustedRequests = Math.Max(exhaustedRequests, count);
        return previous != RepeatedFailures;
    }
}
