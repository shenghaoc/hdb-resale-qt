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
    private readonly ImportResult import;
    private readonly ExplorerState state;
    public LocatedMapModel MapPoints { get; }
    public ResaleMapModel()
    {
        import = CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        state = new(import.Accepted);
        MapPoints = new(state.Visible);
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    // Explicit test opt-in; normal application state and fixture are unchanged.
    public bool RuntimeGate => Environment.GetEnvironmentVariable("HDB_RUNTIME_GATE") == "1";
    public string RuntimeGateFault => RuntimeGate ? Environment.GetEnvironmentVariable("HDB_GATE_FAULT") ?? "" : "";
    public int VisibleCount => state.Visible.Count;
    public string Town => state.Town;
    public int MaximumPrice => (int)state.MaximumPrice;
    public string SelectedId => state.Selected?.Id ?? "";
    public string SelectionDetails => state.Selected is { } t
        ? $"{t.Address}\n{t.Town} · {t.FlatType}\n{Money(t.Price)}\n{t.Facts.Month} registration · local ID {t.Id}\nIdentity: {t.Match.Quality}\nCoordinates: {t.Location.Quality}\n{t.Match.Reason}\n{MatchSources(t.Match)}\n{t.Location.Source}"
        : "Select a marker or a transaction below.";
    public string FilterSummary => $"{VisibleCount} of {import.Accepted.Count} transactions · {MapPoints.Count} mapped · {VisibleCount - MapPoints.Count} unlocated · {Town} · up to {Money(MaximumPrice)}";
    public string ImportSummary => $"Import: {import.Accepted.Count} accepted · {import.MatchedCount} matched · {import.AmbiguousCount} ambiguous · {import.UnmatchedCount} unmatched · {import.Rejected.Count} rejected · {import.Diagnostics.Count} diagnostics.";
    public string ImportDiagnostics => string.Join("\n", import.Diagnostics.Select(d => $"{d.File}:{d.Row}: {d.Message}"));

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
        try { state.Filter(town, price); MapPoints.Replace(state.Visible); }
        finally { EndResetModel(); }
        Notify(nameof(Town), nameof(MaximumPrice), nameof(VisibleCount), nameof(FilterSummary),
            nameof(SelectedId), nameof(SelectionDetails));
    }
    private void Notify(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new(name));
    }
    private static string MatchSources(AddressMatch match)
    {
        var properties = string.Join(", ", match.PropertyCandidates.Select(p => p.SourceRow));
        var postals = string.Join(", ", match.PostalAssertions.Select(p => p.SourceRow));
        return $"HDB property CSV rows: {properties}\nACRA B CSV rows: {postals}";
    }
    private static string Money(decimal price) => "S$" + price.ToString(price == decimal.Truncate(price) ? "N0" : "N2", CultureInfo.InvariantCulture);

    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= VisibleCount
            ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : VisibleCount;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [256] = "transactionId", [257] = "latitude", [258] = "longitude",
        [259] = "address", [260] = "priceLabel", [261] = "townName", [262] = "locationLabel"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if (index is not { IsValid: true } || index.Row < 0 || index.Row >= VisibleCount) return null;
        var t = state.Visible[index.Row];
        return role switch
        {
            256 => t.Id, 257 => t.Location.Point?.Latitude, 258 => t.Location.Point?.Longitude,
            259 => t.Address, 260 => Money(t.Price), 261 => t.Town, 262 => $"Identity: {t.Match.Quality}\nCoordinates: {t.Location.Quality}", _ => null
        };
    }
}
