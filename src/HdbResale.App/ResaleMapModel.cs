using System.ComponentModel;
using System.Globalization;
using HdbResale.Domain;
using Qt.Bridge.Models;
using Qt.DotNet;
using Qt.Quick;

namespace HdbResale.App;

[QmlElement(Name = "Resales", Singleton = true)]
public sealed class ResaleMapModel : Model, INotifyPropertyChanged
{
    private readonly ExplorerState state = new(Fixture.Transactions);
    public event PropertyChangedEventHandler? PropertyChanged;

    public int VisibleCount => state.Visible.Count;
    public string Town => state.Town;
    public int MaximumPrice => state.MaximumPrice;
    public string SelectedId => state.Selected?.Id ?? "";
    public string SelectionDetails => state.Selected is { } t
        ? $"{t.Address}\n{t.Town} · {t.FlatType}\n{Money(t.Price)}\nSynthetic fixture transaction {t.Id}"
        : "Select a marker or a transaction below.";
    public string FilterSummary => $"{VisibleCount} of {Fixture.Transactions.Count} fixture transactions · {Town} · up to {Money(MaximumPrice)}";

    public void SetTown(string town) => ApplyFilter(town, MaximumPrice);
    public void SetMaximumPrice(int price) => ApplyFilter(Town, price);
    public void ResetFilters() => ApplyFilter("All towns", 1_000_000);
    public void SelectTransaction(string id)
    {
        state.Select(id);
        Notify(nameof(SelectedId), nameof(SelectionDetails));
    }

    private void ApplyFilter(string town, int price)
    {
        if (town == Town && price == MaximumPrice) return;
        // Validate before opening the Qt model reset transaction.
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        BeginResetModel();
        try { state.Filter(town, price); }
        finally { EndResetModel(); }
        Notify(nameof(Town), nameof(MaximumPrice), nameof(VisibleCount), nameof(FilterSummary),
            nameof(SelectedId), nameof(SelectionDetails));
    }
    private void Notify(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new(name));
    }
    private static string Money(int price) => "S$" + price.ToString("N0", CultureInfo.InvariantCulture);

    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= VisibleCount
            ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : VisibleCount;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [256] = "transactionId", [257] = "latitude", [258] = "longitude",
        [259] = "address", [260] = "priceLabel", [261] = "townName"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if (index is not { IsValid: true } || index.Row < 0 || index.Row >= VisibleCount) return null;
        var t = state.Visible[index.Row];
        return role switch
        {
            256 => t.Id, 257 => t.Latitude, 258 => t.Longitude,
            259 => t.Address, 260 => Money(t.Price), 261 => t.Town, _ => null
        };
    }
}
