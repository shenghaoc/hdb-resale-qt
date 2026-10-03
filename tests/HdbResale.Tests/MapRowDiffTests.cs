using HdbResale.App;
using HdbResale.Domain;
using Xunit;

namespace HdbResale.Tests;
public sealed class MapRowDiffTests
{
    private static readonly ResaleTransaction Transaction = ExplorerStateTests.Fixture().Accepted[0];
    private static BlockSummary Row(string key, int count = 1) =>
        new(key, Transaction, count, 100, 200, 150);
    private static List<BlockSummary> Apply(IReadOnlyList<BlockSummary> before, IReadOnlyList<MapRowEdit> edits)
    {
        var rows = before.ToList();
        foreach (var edit in edits)
        {
            Assert.True(edit.Count > 0);
            switch (edit.Kind)
            {
                case MapRowEditKind.Remove: rows.RemoveRange(edit.First, edit.Count); break;
                case MapRowEditKind.Insert: rows.InsertRange(edit.First, edit.Rows); break;
                case MapRowEditKind.Update:
                    for (var i = 0; i < edit.Count; i++)
                    {
                        Assert.Equal(rows[edit.First + i].Key, edit.Rows[i].Key);
                        rows[edit.First + i] = edit.Rows[i];
                    }
                    break;
            }
            Assert.Equal(rows.Select(r => r.Key).Order(StringComparer.Ordinal), rows.Select(r => r.Key));
            Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct().Count());
        }
        return rows;
    }
    [Fact]
    public void IdenticalRowsNeedNoModelNotifications()
    {
        var rows = new[] { Row("a"), Row("b") };
        Assert.Empty(MapRowDiff.Plan(rows, rows.ToArray()));
    }
    [Fact]
    public void RetainedKeysOnlyUpdateAndMissingKeysAreBatched()
    {
        var before = new[] { Row("a"), Row("b"), Row("c"), Row("d"), Row("e") };
        var next = new[] { before[0] with { Count = 4 }, before[3], Row("f"), Row("g") };
        var edits = MapRowDiff.Plan(before, next);
        Assert.Equal(next, Apply(before, edits));
        Assert.Equal(3, edits.Where(e => e.Kind == MapRowEditKind.Remove).Sum(e => e.Count));
        Assert.Equal(2, edits.Where(e => e.Kind == MapRowEditKind.Insert).Sum(e => e.Count));
        Assert.Equal(1, edits.Where(e => e.Kind == MapRowEditKind.Update).Sum(e => e.Count));
    }
    [Fact]
    public void RepeatedFullSubsetDifferentEmptyAndFullTransitionsStayExact()
    {
        var random = new Random(7103);
        var full = Enumerable.Range(0, 1921).Select(i => Row($"address-{i:D4}")).ToArray();
        IReadOnlyList<BlockSummary> current = full;
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var next = iteration % 10 == 0 ? [] : iteration % 10 == 1 ? full :
                full.Where(_ => random.Next(4) == 0).Select(r => r with { Count = random.Next(1, 50) }).ToArray();
            var edits = MapRowDiff.Plan(current, next);
            Assert.Equal(next, Apply(current, edits));
            var retained = current.Select(r => r.Key).Intersect(next.Select(r => r.Key)).Count();
            Assert.Equal(current.Count - retained, edits.Where(e => e.Kind == MapRowEditKind.Remove).Sum(e => e.Count));
            Assert.Equal(next.Length - retained, edits.Where(e => e.Kind == MapRowEditKind.Insert).Sum(e => e.Count));
            current = next;
        }
    }
    [Fact]
    public void UpdatesAnnounceOnlyChangedRolesButIncludeChangedCoordinates()
    {
        var a = Row("a"); var b = a with { Count = 5 };
        Assert.Equal(new[] { 260, 262 }, MapRowDiff.ChangedRoles([a], new(MapRowEditKind.Update, 0, 1, [b])));
        var moved = a with { Latest = a.Latest with { Id = "new", Location = new(new(1.2, 103.7), CoordinateQuality.BlockApproximation, "test") } };
        Assert.Equal(new[] { 256, 257, 258 }, MapRowDiff.ChangedRoles([a], new(MapRowEditKind.Update, 0, 1, [moved])));
        Assert.Empty(MapRowDiff.ChangedRoles([a], new(MapRowEditKind.Update, 0, 1, [a with { MinimumPrice = 50 }])));
    }
    [Fact]
    public void DuplicateOrUnsortedKeysAreRejectedBeforeAnyNotification()
    {
        Assert.Throws<ArgumentException>(() => MapRowDiff.Plan([Row("b"), Row("a")], []));
        Assert.Throws<ArgumentException>(() => MapRowDiff.Plan([], [Row("a"), Row("a")]));
    }
}
