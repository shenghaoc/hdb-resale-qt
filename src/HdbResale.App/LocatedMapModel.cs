using System.Globalization;
using HdbResale.Domain;
using Qt.Bridge.Models;
using Qt.DotNet;
using Qt.Quick;
namespace HdbResale.App;

// The map has its own rows, sharing the owner's filtered immutable transactions.
// Unlocated transactions remain in Resales (the sidebar) and never enter this model.
public sealed class LocatedMapModel : Model
{
    private IReadOnlyList<ResaleTransaction> rows;
    internal LocatedMapModel(IReadOnlyList<ResaleTransaction> transactions) => rows = Located(transactions);
    internal int Count => rows.Count;
    internal void Replace(IReadOnlyList<ResaleTransaction> transactions)
    {
        BeginResetModel();
        try { rows = Located(transactions); }
        finally { EndResetModel(); }
    }
    private static IReadOnlyList<ResaleTransaction> Located(IReadOnlyList<ResaleTransaction> transactions) =>
        Array.AsReadOnly(transactions.Where(t => t.Location.Point is not null).ToArray());
    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= rows.Count
            ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : rows.Count;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [256] = "transactionId", [257] = "latitude", [258] = "longitude",
        [259] = "address", [260] = "priceLabel"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if (index is not { IsValid: true } || index.Row < 0 || index.Row >= rows.Count) return null;
        var t = rows[index.Row];
        return role switch
        {
            256 => t.Id, 257 => t.Location.Point!.Latitude, 258 => t.Location.Point!.Longitude,
            259 => t.Address, 260 => "S$" + t.Price.ToString(t.Price == decimal.Truncate(t.Price) ? "N0" : "N2", CultureInfo.InvariantCulture), _ => null
        };
    }
}
