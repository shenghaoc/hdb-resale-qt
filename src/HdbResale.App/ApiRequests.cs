using System.Collections.Concurrent;

namespace HdbResale.App;

// Runs API requests off the UI thread and hands their outcomes back to it. Qt models and QML state are only
// touched on the UI thread: completions are queued here and applied when the UI calls Drain(), which QML
// does on a short timer while Busy. Nothing polls while no request is outstanding.
internal sealed class ApiRequests : IDisposable
{
    private readonly ConcurrentQueue<Action> completed = new();
    private readonly CancellationTokenSource lifetime = new();
    private int outstanding;

    internal bool Busy => Volatile.Read(ref outstanding) > 0 || !completed.IsEmpty;

    internal void Start<T>(Func<CancellationToken, Task<T>> request, Action<T> onSuccess, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(request);
        Interlocked.Increment(ref outstanding);
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await request(lifetime.Token).ConfigureAwait(false);
                completed.Enqueue(() => onSuccess(result));
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception e) { completed.Enqueue(() => onFailure(e)); }
            // Decrement after enqueueing, so Busy never reads false while an outcome is still to be applied.
            finally { Interlocked.Decrement(ref outstanding); }
        });
    }

    // UI thread only: applies every outcome that has arrived, in arrival order.
    internal int Drain()
    {
        var applied = 0;
        while (completed.TryDequeue(out var outcome)) { outcome(); applied++; }
        return applied;
    }

    public void Dispose()
    {
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
