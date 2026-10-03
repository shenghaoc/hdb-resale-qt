using System.Globalization;
using System.Diagnostics;
using HdbResale.Domain;
using Qt.Bridge.Models;
using Qt.DotNet;
using Qt.Quick;
namespace HdbResale.App;

// The map has its own rows, sharing the owner's filtered immutable transactions.
// Unlocated transactions remain in Resales (the sidebar) and never enter this model.
public sealed class LocatedMapModel : Model
{
    private IReadOnlyList<BlockSummary> rows;
    internal LocatedMapModel(IReadOnlyList<ResaleTransaction> transactions) => rows = BlockSummaries.Located(transactions);
    internal int Count => rows.Count;
    internal void Replace(IReadOnlyList<ResaleTransaction> transactions)
    {
        var timer = Stopwatch.StartNew();
        var next = BlockSummaries.Located(transactions);
        var aggregateMs = timer.Elapsed.TotalMilliseconds;
        var previous = rows.Count;
        timer.Restart();
        BeginResetModel();
        try { rows = next; }
        finally { EndResetModel(); }
        if (Environment.GetEnvironmentVariable("HDB_SCALE_GATE") == "1")
            Console.WriteLine(FormattableString.Invariant($"HDB_MAP_UPDATE strategy=reset aggregate-ms={aggregateMs:F3} notifications-ms={timer.Elapsed.TotalMilliseconds:F3} before={previous} after={rows.Count} removed={previous} inserted={rows.Count} changed=0"));
    }
    internal string GateRowsJson => System.Text.Json.JsonSerializer.Serialize(rows.Select(block => new
    {
        mapKey = block.Key, transactionId = block.Latest.Id, transactionCount = block.Count,
        latitude = block.Latest.Location.Point!.Latitude, longitude = block.Latest.Location.Point!.Longitude,
        address = block.Latest.Address, priceLabel = PriceLabel(block)
    }));
    private static string PriceLabel(BlockSummary block) =>
        $"{block.Count} transactions · median S${block.MedianPrice.ToString("N0", CultureInfo.InvariantCulture)} · latest {block.Latest.Facts.Month}";
    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= rows.Count
            ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : rows.Count;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [256] = "transactionId", [257] = "latitude", [258] = "longitude",
        [259] = "address", [260] = "priceLabel", [261] = "mapKey", [262] = "transactionCount"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if (index is not { IsValid: true } || index.Row < 0 || index.Row >= rows.Count) return null;
        var block = rows[index.Row];
        var t = block.Latest;
        return role switch
        {
            256 => t.Id, 257 => t.Location.Point!.Latitude, 258 => t.Location.Point!.Longitude,
            259 => t.Address, 260 => PriceLabel(block),
            261 => block.Key, 262 => block.Count, _ => null
        };
    }
}
