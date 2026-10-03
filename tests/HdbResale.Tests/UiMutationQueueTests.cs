using HdbResale.App;
using HdbResale.Domain;
using Xunit;

namespace HdbResale.Tests;
public sealed class UiMutationQueueTests
{
    [Fact]
    public void ReentrantIntentsRunInOrderAfterCurrentOperationCompletes()
    {
        var queue = new UiMutationQueue();
        var order = new List<int>();
        queue.Enqueue(() =>
        {
            order.Add(1);
            queue.Enqueue(() => order.Add(3));
            queue.Enqueue(() => order.Add(4));
            order.Add(2);
        });
        Assert.Equal(new[] { 1, 2, 3, 4 }, order);
        Assert.Equal(2, queue.MaximumPendingCount);
    }
    [Fact]
    public void ReentrantFilterCannotInvalidateAnOuterRemovalPlan()
    {
        var transaction = ExplorerStateTests.Fixture().Accepted[0];
        var rows = Enumerable.Range(0, 6).Select(i => new BlockSummary(i.ToString(), transaction, 1, 1, 1, 1)).ToList();
        var subset = rows.Take(2).ToArray();
        var queue = new UiMutationQueue();
        var injected = false;
        void Apply(IReadOnlyList<BlockSummary> next)
        {
            foreach (var edit in MapRowDiff.Plan(rows, next))
            {
                if (edit.Kind == MapRowEditKind.Remove)
                {
                    if (!injected)
                    {
                        injected = true;
                        queue.Enqueue(() => Apply([]));
                    }
                    rows.RemoveRange(edit.First, edit.Count);
                }
                else if (edit.Kind == MapRowEditKind.Insert) rows.InsertRange(edit.First, edit.Rows);
                else for (var i = 0; i < edit.Count; i++) rows[edit.First + i] = edit.Rows[i];
            }
        }
        queue.Enqueue(() => Apply(subset));
        Assert.True(injected);
        Assert.Empty(rows);
    }
    [Fact]
    public void CombinedIntentsComposeAtExecutionAndIntermediateEmptyClearsSelection()
    {
        var state = new ExplorerState(ExplorerStateTests.Fixture().Accepted);
        var id = state.Visible[0].Id;
        state.Select(id);
        var queue = new UiMutationQueue();
        queue.Enqueue(() =>
        {
            queue.Enqueue(() => state.Filter(state.Town, 0));
            queue.Enqueue(() => state.Filter("All towns", state.MaximumPrice));
            queue.Enqueue(() => state.Filter(state.Town, 1_000_000));
            state.Filter("ANG MO KIO", state.MaximumPrice);
        });
        Assert.Equal("All towns", state.Town);
        Assert.Equal(6, state.Visible.Count);
        Assert.Null(state.Selected);
        Assert.Equal(3, queue.MaximumPendingCount);
    }
    [Fact]
    public void FailurePropagatesWithoutReplayingStaleQueuedInput()
    {
        var queue = new UiMutationQueue(); var ran = false;
        Assert.Throws<InvalidOperationException>(() => queue.Enqueue(() =>
        {
            queue.Enqueue(() => ran = true);
            throw new InvalidOperationException("test");
        }));
        Assert.False(ran);
        queue.Enqueue(() => ran = true);
        Assert.True(ran);
    }
}
