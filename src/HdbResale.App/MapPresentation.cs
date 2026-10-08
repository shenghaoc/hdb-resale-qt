using System.Globalization;
using System.Text.Json;

namespace HdbResale.App;

// Pure presentation math. These are screen-space groupings, never evidence or
// address matches. Every filtered address is retained in the owner's truth set.
internal sealed record MapViewport(double Latitude, double Longitude, double Zoom, double Width, double Height)
{
    internal bool IsValid => double.IsFinite(Latitude) && Latitude is >= -85.05112878 and <= 85.05112878
        && double.IsFinite(Longitude) && Longitude is >= -180 and <= 180
        && double.IsFinite(Zoom) && Zoom is >= 0 and <= 22
        && double.IsFinite(Width) && Width > 0 && double.IsFinite(Height) && Height > 0;
}
// One listed address on the map, with the figures the list shows for it (the selected flat type's, when one is).
internal sealed record MapAddress(string Key, double Latitude, double Longitude, string Address, int SalesCount,
    decimal MedianPrice, string LatestMonth);
internal sealed record MapPresentationRow(string Key, double Latitude, double Longitude,
    string Address, string PriceLabel, int TransactionCount, int AddressCount, string MembershipJson)
{
    internal bool IsCluster => AddressCount > 1;
    internal static MapPresentationRow AddressRow(MapAddress address) => new(address.Key, address.Latitude, address.Longitude,
        address.Address,
        $"{address.SalesCount:N0} {(address.SalesCount == 1 ? "sale" : "sales")} · median S${address.MedianPrice.ToString("N0", CultureInfo.InvariantCulture)} · latest {address.LatestMonth}",
        address.SalesCount, 1, JsonSerializer.Serialize(new[] { address.Key }));
}
internal sealed record MapPresentationPlan(IReadOnlyList<MapPresentationRow> Rows, int MappedAddresses,
    int InViewAddresses, int ClusterCount, bool SelectedInView)
{
    internal int IndividualCount => Rows.Count - ClusterCount;
}
internal static class MapPresentation
{
    internal const double IndividualZoom = 15;
    internal const int CellPixels = 64;
    internal static (double X, double Y) World(double latitude, double longitude, double zoom)
    {
        var size = 256 * Math.Pow(2, zoom);
        var sin = Math.Sin(Math.Clamp(latitude, -85.05112878, 85.05112878) * Math.PI / 180);
        return ((longitude + 180) / 360 * size, (0.5 - Math.Log((1 + sin) / (1 - sin)) / (4 * Math.PI)) * size);
    }
    internal static bool Contains(MapViewport viewport, double latitude, double longitude)
    {
        if (!viewport.IsValid) return false;
        var center = World(viewport.Latitude, viewport.Longitude, viewport.Zoom);
        var point = World(latitude, longitude, viewport.Zoom);
        var size = 256 * Math.Pow(2, viewport.Zoom);
        var dx = Math.Abs(point.X - center.X); dx = Math.Min(dx, size - dx);
        return dx <= viewport.Width / 2 && Math.Abs(point.Y - center.Y) <= viewport.Height / 2;
    }
    internal static MapPresentationPlan Plan(IReadOnlyList<MapAddress> all, MapViewport? viewport, string selectedKey)
    {
        if (viewport is not { IsValid: true }) return new([], all.Count, 0, 0, false);
        var visible = all.Where(b => Contains(viewport, b.Latitude, b.Longitude)).ToArray();
        var selectedInView = visible.Any(b => b.Key == selectedKey);
        if (viewport.Zoom >= IndividualZoom)
            return new(visible.OrderBy(b => b.Key, StringComparer.Ordinal).Select(MapPresentationRow.AddressRow).ToArray(), all.Count, visible.Length, 0, selectedInView);
        var rows = new List<MapPresentationRow>();
        var gridZoom = (int)Math.Floor(viewport.Zoom);
        var groups = visible.Where(b => b.Key != selectedKey).GroupBy(b =>
        {
            var p = World(b.Latitude, b.Longitude, gridZoom);
            return (X: (long)Math.Floor(p.X / CellPixels), Y: (long)Math.Floor(p.Y / CellPixels));
        });
        foreach (var group in groups)
        {
            var members = group.ToArray();
            if (members.Length == 1) { rows.Add(MapPresentationRow.AddressRow(members[0])); continue; }
            var count = members.Sum(b => b.SalesCount);
            rows.Add(new($"@cell:{gridZoom}:{group.Key.X}:{group.Key.Y}",
                members.Average(b => b.Latitude), members.Average(b => b.Longitude),
                $"{members.Length} mapped addresses", $"{members.Length} addresses · {count} sales · zoom in",
                count, members.Length, JsonSerializer.Serialize(members.Select(b => b.Key))));
        }
        if (selectedInView) rows.Add(MapPresentationRow.AddressRow(visible.First(b => b.Key == selectedKey)));
        rows.Sort((a,b) => StringComparer.Ordinal.Compare(a.Key,b.Key));
        return new(rows, all.Count, visible.Length, rows.Count(r => r.IsCluster), selectedInView);
    }
}
internal sealed record PresentationEdit(MapRowEditKind Kind, int First, int Count, IReadOnlyList<MapPresentationRow> Rows);
internal static class PresentationDiff
{
    internal static IReadOnlyList<PresentationEdit> Plan(IReadOnlyList<MapPresentationRow> previous, IReadOnlyList<MapPresentationRow> next)
    {
        Validate(previous); Validate(next);
        var edits = new List<PresentationEdit>();
        var work = previous.ToList();
        var keys = next.Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
        for (var last = work.Count - 1; last >= 0;)
        {
            if (keys.Contains(work[last].Key)) { last--; continue; }
            var first = last;
            while (first > 0 && !keys.Contains(work[first - 1].Key)) first--;
            edits.Add(new(MapRowEditKind.Remove, first, last-first+1, []));
            work.RemoveRange(first,last-first+1); last=first-1;
        }
        for (var index = 0; index < next.Count;)
        {
            if (index < work.Count && work[index].Key == next[index].Key) { index++; continue; }
            var first=index;
            while (index<next.Count && (first>=work.Count || next[index].Key != work[first].Key)) index++;
            var added=next.Skip(first).Take(index-first).ToArray();
            edits.Add(new(MapRowEditKind.Insert,first,added.Length,added)); work.InsertRange(first,added);
        }
        for (var index=0;index<next.Count;)
        {
            if (work[index] == next[index]) { index++; continue; }
            var first=index;
            while(index<next.Count && work[index] != next[index]) index++;
            edits.Add(new(MapRowEditKind.Update,first,index-first,next.Skip(first).Take(index-first).ToArray()));
        }
        return edits;
    }
    private static void Validate(IReadOnlyList<MapPresentationRow> rows)
    {
        for (var i=1;i<rows.Count;i++)
            if (StringComparer.Ordinal.Compare(rows[i-1].Key,rows[i].Key)>=0)
                throw new ArgumentException("Presentation rows must have unique, ordinally sorted keys.");
    }
    internal static int[] ChangedRoles(IReadOnlyList<MapPresentationRow> previous, PresentationEdit edit)
    {
        var roles=new HashSet<int>();
        for(var i=0;i<edit.Count;i++)
        {
            var a=previous[edit.First+i]; var b=edit.Rows[i];
            if(a.Latitude!=b.Latitude)roles.Add(257);
            if(a.Longitude!=b.Longitude)roles.Add(258);
            if(a.Address!=b.Address)roles.Add(259);
            if(a.PriceLabel!=b.PriceLabel)roles.Add(260);
            if(a.TransactionCount!=b.TransactionCount)roles.Add(262);
            if(a.AddressCount!=b.AddressCount)roles.Add(263);
        }
        return roles.Order().ToArray();
    }
}
