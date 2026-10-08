using Qt.Bridge.Models;
using Qt.DotNet;
using Qt.Quick;
namespace HdbResale.App;

// Complete filtered address truth is separate from the bounded screen projection.
public sealed class LocatedMapModel : Model
{
    private readonly List<MapPresentationRow> rows = [];
    private IReadOnlyList<MapAddress> addresses = [];
    private MapPresentationPlan plan = new([], 0, 0, 0, false);
    private MapViewport? viewport;
    private string selectedKey = "";
    internal int Count => addresses.Count;
    internal int PresentationCount => rows.Count;
    internal int InViewCount => plan.InViewAddresses;
    internal int ClusterCount => plan.ClusterCount;
    internal bool SelectedInView => plan.SelectedInView;
    internal bool ViewportReady => viewport is { IsValid: true };
    internal MapViewport? Viewport => viewport;
    internal void Replace(IReadOnlyList<MapAddress> next, string selection)
    {
        addresses = next;
        selectedKey = selection;
        Refresh();
    }
    internal void Select(string key)
    {
        if (selectedKey == key) return;
        selectedKey = key;
        Refresh();
    }
    internal bool SetViewport(MapViewport next)
    {
        if (!next.IsValid || next == viewport) return false;
        viewport = next;
        Refresh();
        return true;
    }
    private void Refresh()
    {
        plan = MapPresentation.Plan(addresses, viewport, selectedKey);
        foreach (var edit in PresentationDiff.Plan(rows, plan.Rows))
        {
            switch (edit.Kind)
            {
                case MapRowEditKind.Remove:
                    BeginRemoveRows(ModelIndex.Empty, edit.First, edit.First + edit.Count - 1);
                    try { rows.RemoveRange(edit.First, edit.Count); }
                    finally { EndRemoveRows(); }
                    break;
                case MapRowEditKind.Insert:
                    BeginInsertRows(ModelIndex.Empty, edit.First, edit.First + edit.Count - 1);
                    try { rows.InsertRange(edit.First, edit.Rows); }
                    finally { EndInsertRows(); }
                    break;
                case MapRowEditKind.Update:
                    var roles = PresentationDiff.ChangedRoles(rows, edit);
                    for (var i = 0; i < edit.Count; i++) rows[edit.First + i] = edit.Rows[i];
                    if (roles.Length > 0) DataChanged(new(edit.First, 0), new(edit.First + edit.Count - 1, 0), roles);
                    break;
            }
        }
    }
    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= rows.Count ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : rows.Count;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [257]="latitude", [258]="longitude", [259]="address", [260]="priceLabel",
        [261]="mapKey", [262]="transactionCount", [263]="addressCount"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if(index is not { IsValid: true } || index.Row < 0 || index.Row >= rows.Count) return null;
        var row=rows[index.Row];
        return role switch
        {
            257=>row.Latitude,258=>row.Longitude,259=>row.Address,260=>row.PriceLabel,
            261=>row.Key,262=>row.TransactionCount,263=>row.AddressCount,_=>null
        };
    }
}
