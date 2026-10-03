using System.Diagnostics;
using System.Text.Json;
using HdbResale.Domain;
using Qt.Bridge.Models;
using Qt.DotNet;
using Qt.Quick;
namespace HdbResale.App;

// Complete filtered address truth is separate from the bounded screen projection.
public sealed class LocatedMapModel : Model
{
    private readonly List<MapPresentationRow> rows = [];
    private IReadOnlyList<BlockSummary> summaries;
    private MapPresentationPlan plan;
    private MapViewport? viewport;
    private string selectedKey = "";
    internal bool UseReset => Environment.GetEnvironmentVariable("HDB_SCALE_GATE") == "1"
        && Environment.GetEnvironmentVariable("HDB_MAP_UPDATE") == "reset";
    internal long StartupRoleReads { get; private set; }
    private readonly bool startupProfile = Environment.GetEnvironmentVariable("HDB_STARTUP_PROFILE") == "1";
    private readonly bool trace = Environment.GetEnvironmentVariable("HDB_SCALE_GATE") == "1";
    internal LocatedMapModel(IReadOnlyList<ResaleTransaction> transactions, Action<string>? stage = null)
    {
        summaries = BlockSummaries.Located(transactions);
        stage?.Invoke("map-aggregation");
        plan = MapPresentation.Plan(summaries, viewport, selectedKey);
        rows.AddRange(plan.Rows);
        stage?.Invoke("map-model-population");
    }
    internal int Count => summaries.Count;
    internal int PresentationCount => rows.Count;
    internal int InViewCount => plan.InViewAddresses;
    internal int ClusterCount => plan.ClusterCount;
    internal bool SelectedInView => plan.SelectedInView;
    internal bool ViewportReady => viewport is { IsValid: true };
    internal long InsertedCount { get; private set; }
    internal long RemovedCount { get; private set; }
    internal IReadOnlyList<BlockSummary> Summaries => summaries;
    internal MapViewport? Viewport => viewport;
    internal void Replace(IReadOnlyList<ResaleTransaction> transactions, string selection)
    {
        var timer = Stopwatch.StartNew();
        summaries = BlockSummaries.Located(transactions);
        var aggregateMs = timer.Elapsed.TotalMilliseconds;
        selectedKey = selection;
        Refresh(aggregateMs, "filter");
    }
    internal void Select(string key)
    {
        if (selectedKey == key) return;
        selectedKey = key;
        Refresh(0, "selection");
    }
    internal bool SetViewport(MapViewport next)
    {
        if (!next.IsValid || next == viewport) return false;
        viewport = next;
        Refresh(0, "viewport");
        return true;
    }
    private void Refresh(double aggregateMs, string reason)
    {
        var timer = Stopwatch.StartNew();
        plan = MapPresentation.Plan(summaries, viewport, selectedKey);
        if (trace && Environment.GetEnvironmentVariable("HDB_PRESENTATION_GATE") == "1"
            && Environment.GetEnvironmentVariable("HDB_GATE_FAULT") == "drop-presentation" && plan.Rows.Count > 0)
        {
            var dropped=plan.Rows[0]; var remaining=plan.Rows.Skip(1).ToArray();
            plan=plan with {Rows=remaining,InViewAddresses=plan.InViewAddresses-dropped.AddressCount,ClusterCount=plan.ClusterCount-(dropped.IsCluster?1:0)};
        }
        var projectionMs = timer.Elapsed.TotalMilliseconds;
        var previous = rows.Count;
        timer.Restart();
        IReadOnlyList<PresentationEdit> edits = UseReset ? [] : PresentationDiff.Plan(rows, plan.Rows);
        var planMs = timer.Elapsed.TotalMilliseconds;
        var removed = 0; var inserted = 0; var changed = 0;
        timer.Restart();
        if (UseReset)
        {
            BeginResetModel();
            try { rows.Clear(); rows.AddRange(plan.Rows); }
            finally { EndResetModel(); }
            removed = previous; inserted = rows.Count;
        }
        else foreach (var edit in edits)
        {
            switch (edit.Kind)
            {
                case MapRowEditKind.Remove:
                    BeginRemoveRows(ModelIndex.Empty, edit.First, edit.First + edit.Count - 1);
                    try { rows.RemoveRange(edit.First, edit.Count); }
                    finally { EndRemoveRows(); }
                    removed += edit.Count; break;
                case MapRowEditKind.Insert:
                    BeginInsertRows(ModelIndex.Empty, edit.First, edit.First + edit.Count - 1);
                    try { rows.InsertRange(edit.First, edit.Rows); }
                    finally { EndInsertRows(); }
                    inserted += edit.Count; break;
                case MapRowEditKind.Update:
                    var roles = PresentationDiff.ChangedRoles(rows, edit);
                    for (var i = 0; i < edit.Count; i++) rows[edit.First + i] = edit.Rows[i];
                    if (roles.Length > 0) DataChanged(new(edit.First, 0), new(edit.First + edit.Count - 1, 0), roles);
                    changed += edit.Count; break;
            }
        }
        InsertedCount += inserted; RemovedCount += removed;
        if (trace)
            Console.WriteLine(FormattableString.Invariant($"HDB_MAP_UPDATE strategy={(UseReset ? "reset" : "adaptive")} reason={reason} aggregate-ms={aggregateMs:F3} projection-ms={projectionMs:F3} plan-ms={planMs:F3} notifications-ms={timer.Elapsed.TotalMilliseconds:F3} before={previous} after={rows.Count} mapped={Count} in-view={InViewCount} clusters={ClusterCount} removed={removed} inserted={inserted} changed={changed}"));
    }
    internal string GateRowsJson => JsonSerializer.Serialize(plan.Rows.Select(r => new
    {
        mapKey=r.Key, transactionId=r.TransactionId, transactionCount=r.TransactionCount,
        latitude=r.Latitude, longitude=r.Longitude, address=r.Address, priceLabel=r.PriceLabel,
        addressCount=r.AddressCount, members=JsonSerializer.Deserialize<string[]>(r.MembershipJson)
    }));
    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= rows.Count ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : rows.Count;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [256]="transactionId", [257]="latitude", [258]="longitude", [259]="address", [260]="priceLabel",
        [261]="mapKey", [262]="transactionCount", [263]="addressCount"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if(index is not { IsValid: true } || index.Row < 0 || index.Row >= rows.Count) return null;
        if(startupProfile) StartupRoleReads++;
        var row=rows[index.Row];
        return role switch
        {
            256=>row.TransactionId,257=>row.Latitude,258=>row.Longitude,259=>row.Address,260=>row.PriceLabel,
            261=>row.Key,262=>row.TransactionCount,263=>row.AddressCount,_=>null
        };
    }
}
