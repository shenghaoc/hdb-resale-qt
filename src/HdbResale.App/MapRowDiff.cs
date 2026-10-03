using HdbResale.Domain;

namespace HdbResale.App;

// Pure presentation edit plan, testable without loading Qt. Address keys are sorted,
// unique and stable; retained keys keep their delegate identity and order.
internal sealed record MapRowEdit(MapRowEditKind Kind, int First, int Count, IReadOnlyList<BlockSummary> Rows);
internal enum MapRowEditKind { Remove, Insert, Update }
internal static class MapRowDiff
{
    internal static IReadOnlyList<MapRowEdit> Plan(IReadOnlyList<BlockSummary> previous, IReadOnlyList<BlockSummary> next)
    {
        Validate(previous); Validate(next);
        var edits = new List<MapRowEdit>();
        var work = previous.ToList();
        var nextKeys = next.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        for (var last = work.Count - 1; last >= 0;)
        {
            if (nextKeys.Contains(work[last].Key)) { last--; continue; }
            var first = last;
            while (first > 0 && !nextKeys.Contains(work[first - 1].Key)) first--;
            edits.Add(new(MapRowEditKind.Remove, first, last - first + 1, []));
            work.RemoveRange(first, last - first + 1);
            last = first - 1;
        }
        for (var index = 0; index < next.Count;)
        {
            if (index < work.Count && work[index].Key == next[index].Key) { index++; continue; }
            var first = index;
            while (index < next.Count && (first >= work.Count || next[index].Key != work[first].Key)) index++;
            var added = next.Skip(first).Take(index - first).ToArray();
            edits.Add(new(MapRowEditKind.Insert, first, added.Length, added));
            work.InsertRange(first, added);
        }
        for (var index = 0; index < next.Count;)
        {
            if (work[index] == next[index]) { index++; continue; }
            var first = index;
            while (index < next.Count && work[index] != next[index]) index++;
            var changed = next.Skip(first).Take(index - first).ToArray();
            edits.Add(new(MapRowEditKind.Update, first, changed.Length, changed));
        }
        return edits;
    }
    internal static int[] ChangedRoles(IReadOnlyList<BlockSummary> previous, MapRowEdit edit)
    {
        var roles = new HashSet<int>();
        for (var i = 0; i < edit.Count; i++)
        {
            var a = previous[edit.First + i]; var b = edit.Rows[i];
            if (a.Latest.Id != b.Latest.Id) roles.Add(256);
            if (a.Latest.Location.Point!.Latitude != b.Latest.Location.Point!.Latitude) roles.Add(257);
            if (a.Latest.Location.Point!.Longitude != b.Latest.Location.Point!.Longitude) roles.Add(258);
            if (a.Latest.Address != b.Latest.Address) roles.Add(259);
            if (a.Count != b.Count || a.MedianPrice != b.MedianPrice || a.Latest.Facts.Month != b.Latest.Facts.Month) roles.Add(260);
            if (a.Count != b.Count) roles.Add(262);
        }
        return roles.Order().ToArray();
    }
    private static void Validate(IReadOnlyList<BlockSummary> rows)
    {
        for (var i = 1; i < rows.Count; i++)
            if (StringComparer.Ordinal.Compare(rows[i - 1].Key, rows[i].Key) >= 0)
                throw new ArgumentException("Map rows must have unique, ordinally sorted address keys.");
    }
}
