namespace HdbResale.App;

// Bridge's synchronized model notifications can pump QML events. Queue UI intents
// on that same UI thread so a reentrant input cannot mutate an unfinished model
// transaction/edit plan. FIFO also preserves intermediate selection clearing.
internal sealed class UiMutationQueue
{
    private readonly Queue<Action> pending = new();
    private bool executing;
    internal int MaximumPendingCount { get; private set; }
    internal void Enqueue(Action intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        pending.Enqueue(intent);
        MaximumPendingCount = Math.Max(MaximumPendingCount, pending.Count);
        if (executing) return;
        executing = true;
        try
        {
            while (pending.TryDequeue(out var next)) next();
        }
        finally
        {
            // An exception is not swallowed; do not replay stale pending input later.
            pending.Clear();
            executing = false;
        }
    }
}
