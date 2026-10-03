using System.ComponentModel;
using System.Diagnostics;
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
    private readonly StartupProfiler? startupProfile;
    private long startupRoleReads;
    private readonly ExplorerState state;
    private readonly UiMutationQueue mutations = new();
    public LocatedMapModel MapPoints { get; }
    public int GateTownCount { get; }
    public int GateTownMapped { get; }
    public int GateBudgetCount { get; }
    public int GateBudgetMapped { get; }
    public int GateInitialCount { get; }
    public int GateInitialMapped { get; }
    public int GateAllCount { get; }
    public int GateAllMapped { get; }
    public int GateAllTownCount { get; }
    public int GateAllTownMapped { get; }
    public int GateOtherTownCount { get; }
    public int GateOtherTownMapped { get; }
    public string GateHiddenSelectionId { get; } = "";
    public string GateRetainedSelectionKey { get; } = "";
    public string GateOtherTown => Towns.First(t => t != "All towns" && t != GateTown);
    public ResaleMapModel()
    {
        var timer = Stopwatch.StartNew();
        if (Environment.GetEnvironmentVariable("HDB_STARTUP_PROFILE") == "1") startupProfile = new();
        import = CsvImport.LoadDirectory(Environment.GetEnvironmentVariable("HDB_DATA_DIRECTORY") ?? Path.Combine(AppContext.BaseDirectory, "data"),
            stage: startupProfile is null ? null : MeasureStartup);
        MeasureStartup("import-return");
        Towns = new[] { "All towns" }.Concat(import.Accepted.Select(t => t.Town).Distinct().Order(StringComparer.Ordinal)).ToArray();
        MeasureStartup("town-labels");
        state = new(import.Accepted);
        // The startup-only probe uses all prices to exercise every located address marker.
        if (StartupProbe) state.Filter("All towns", MaximumAvailablePrice);
        MeasureStartup("state-construction");
        MapPoints = new(state.Visible, startupProfile is null ? null : MeasureStartup);
        MeasureStartup("models-constructed");
        if (ScaleGate) Console.WriteLine($"HDB_SCALE_CONSTRUCT {timer.ElapsedMilliseconds} rows={VisibleCount} mapped={MapPoints.Count} managed={GC.GetTotalMemory(false)} working={Environment.WorkingSet}");
        if (ScaleGate)
        {
            timer.Restart();
            var expectedInitial = import.Accepted.Where(t => t.Price <= 1_000_000).ToArray();
            var expectedTown = expectedInitial.Where(t => t.Town == GateTown).ToArray();
            GateAllCount = import.Accepted.Count;
            GateAllMapped = BlockSummaries.Located(import.Accepted).Count;
            GateInitialCount = expectedInitial.Length;
            GateInitialMapped = BlockSummaries.Located(expectedInitial).Count;
            GateTownCount = expectedTown.Length;
            GateTownMapped = BlockSummaries.Located(expectedTown).Count;
            var budget = expectedTown.Where(t => t.Price <= 500_000).ToArray();
            GateBudgetCount = budget.Length;
            var budgetBlocks = BlockSummaries.Located(budget);
            GateBudgetMapped = budgetBlocks.Count;
            if (ScaleReentrant)
            {
                var retainedKeys = budgetBlocks.Select(b => b.Key).ToHashSet(StringComparer.Ordinal);
                var hidden = expectedTown.FirstOrDefault(t => t.Price > 500_000 && t.Location.Point is not null && retainedKeys.Contains(BlockSummaries.Key(t)));
                GateHiddenSelectionId = hidden?.Id ?? "";
                GateRetainedSelectionKey = hidden is null ? "" : BlockSummaries.Key(hidden);
            }
            if (ScaleTransitions)
            {
                var allTown = import.Accepted.Where(t => t.Town == GateTown).ToArray();
                var otherTownName = GateOtherTown;
                var otherTown = import.Accepted.Where(t => t.Town == otherTownName).ToArray();
                GateAllTownCount = allTown.Length;
                GateAllTownMapped = BlockSummaries.Located(allTown).Count;
                GateOtherTownCount = otherTown.Length;
                GateOtherTownMapped = BlockSummaries.Located(otherTown).Count;
            }
            Console.WriteLine($"HDB_SCALE_ORACLE {timer.ElapsedMilliseconds}");
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool StartupProbe => startupProfile is not null && StartupView != "normal";
    public string StartupView => Environment.GetEnvironmentVariable("HDB_STARTUP_VIEW") switch
    {
        "qml-shell" => "qml-shell", "map-shell" => "map-shell", "full" => "full", _ => "normal"
    };
    private void MeasureStartup(string name)
    {
        if (startupProfile is not null)
            Console.WriteLine("HDB_STARTUP_STAGE " + System.Text.Json.JsonSerializer.Serialize(startupProfile.Measure(name)));
    }
    public void StartupReady()
    {
        if (!StartupProbe) return;
        MeasureStartup("qml-ready-" + StartupView);
        Console.WriteLine($"HDB_STARTUP_READY view={StartupView} rows={VisibleCount} markers={MappedCount} sidebar-role-reads={startupRoleReads} map-role-reads={MapPoints.StartupRoleReads}");
        if (Environment.GetEnvironmentVariable("HDB_STARTUP_HEAP") == "1")
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            MeasureStartup("diagnostic-retained-native");
        }
    }

    // Explicit test opt-in; normal application state and fixture are unchanged.
    public bool ScaleGate => Environment.GetEnvironmentVariable("HDB_SCALE_GATE") == "1";
    public bool ScaleReentrant => ScaleGate && Environment.GetEnvironmentVariable("HDB_MAP_REENTRANT") == "1";
    public int GateMaximumQueuedMutations => mutations.MaximumPendingCount;
    public bool ScaleTransitions => ScaleGate && Environment.GetEnvironmentVariable("HDB_MAP_TRANSITIONS") == "1";
    public bool ScaleLifecycle => ScaleGate && Environment.GetEnvironmentVariable("HDB_MAP_LIFECYCLE") != "0";
    public bool ScaleExpandedCoverage => ScaleGate && Environment.GetEnvironmentVariable("HDB_SCALE_EXPANDED_COVERAGE") == "1";
    public bool ScaleMeasurement => ScaleGate && Environment.GetEnvironmentVariable("HDB_MAP_MEASUREMENT") == "1";
    public bool ScaleHeap => ScaleGate && Environment.GetEnvironmentVariable("HDB_SCALE_HEAP") == "1";
    public void MeasureScaleHeap()
    {
        if (!ScaleHeap) return;
        Console.WriteLine($"HDB_SCALE_HEAP before managed={GC.GetTotalMemory(false)} working={Environment.WorkingSet}");
        var timer = Stopwatch.StartNew();
        // Diagnostic opt-in after measured UI transitions; never a runtime memory policy.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        Console.WriteLine($"HDB_SCALE_HEAP after managed={GC.GetTotalMemory(false)} working={Environment.WorkingSet} collection-ms={timer.ElapsedMilliseconds}");
    }
    public string BasemapCacheDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HdbResaleQt", "onemap-default-v1");
    public string MapUpdateStrategy => MapPoints.UseReset ? "reset" : "incremental";
    public int MappedCount => MapPoints.Count;
    public int MapRevision { get; private set; }
    public double LastFilterStartedMs { get; private set; }
    public double LastFilterCompletedMs { get; private set; }
    public string GateMapRowsJson => ScaleGate ? MapPoints.GateRowsJson : "[]";
    public string GateTown => import.Accepted.FirstOrDefault()?.Town ?? "All towns";
    public string FirstVisibleId => state.Visible.FirstOrDefault()?.Id ?? "";
    // Immutable import: compute control labels once, rather than scanning the corpus per binding read.
    private string[] Towns { get; }
    public string TownsJson => System.Text.Json.JsonSerializer.Serialize(Towns);
    public int TownIndex => Array.IndexOf(Towns, Town);
    public bool RuntimeGate => Environment.GetEnvironmentVariable("HDB_RUNTIME_GATE") == "1";
    public string RuntimeGateFault => (RuntimeGate || ScaleGate) ? Environment.GetEnvironmentVariable("HDB_GATE_FAULT") ?? "" : "";
    public int VisibleCount => state.Visible.Count;
    public string Town => state.Town;
    public int MaximumAvailablePrice => checked((int)Math.Max(1_000_000, decimal.Ceiling(import.Accepted.Select(t => t.Price).DefaultIfEmpty(1_000_000).Max())));
    public int MaximumPrice => (int)state.MaximumPrice;
    public string SelectedMapKey => state.Selected is { } t ? BlockSummaries.Key(t) : "";
    public int LocatedTransactions => state.Visible.Count(t => t.Location.Point is not null);
    public string SelectedId => state.Selected?.Id ?? "";
    public string SelectionDetails => state.Selected is { } t
        ? $"{t.Address}\n{t.Town} · {t.FlatType}\n{Money(t.Price)}\n{t.Facts.Month} registration · local ID {t.Id}\nIdentity: {t.Match.Quality}\nCoordinates: {t.Location.Quality}\n{t.Match.Reason}\n{MatchSources(t.Match)}\n{t.Location.Source}"
        : "Select a marker (latest transaction at that address) or a transaction below.";
    public string FilterSummary => $"{VisibleCount} of {import.Accepted.Count} transactions · {LocatedTransactions} located in {MapPoints.Count} address markers · {VisibleCount - LocatedTransactions} unlocated · {Town} · up to {Money(MaximumPrice)}";
    public string ImportSummary => $"Import: {import.Accepted.Count} accepted · {import.MatchedCount} matched · {import.AmbiguousCount} ambiguous · {import.UnmatchedCount} unmatched · {import.Rejected.Count} rejected · {import.Diagnostics.Count} diagnostics.";
    public string ImportDiagnostics => string.Join("\n", import.Diagnostics.Select(d => $"{d.File}:{d.Row}: {d.Message}"));

    public void SetTown(string town)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        mutations.Enqueue(() => ApplyFilter(town, MaximumPrice));
    }
    public void SetMaximumPrice(int price)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        mutations.Enqueue(() => ApplyFilter(Town, price));
    }
    public void ResetFilters() => mutations.Enqueue(() => ApplyFilter("All towns", 1_000_000));
    public void SelectTransaction(string id) => mutations.Enqueue(() =>
    {
        state.Select(id);
        Notify(nameof(SelectedId), nameof(SelectedMapKey), nameof(SelectionDetails));
    });

    private void ApplyFilter(string town, int price)
    {
        if (town == Town && price == MaximumPrice) return;
        // Validate before opening the Qt model reset transaction.
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        if (ScaleGate) LastFilterStartedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timer = Stopwatch.StartNew();
        var stage = Stopwatch.StartNew();
        BeginResetModel();
        var listBeginMs = stage.Elapsed.TotalMilliseconds;
        stage.Restart();
        double filterMs;
        try { state.Filter(town, price); filterMs = stage.Elapsed.TotalMilliseconds; }
        finally { stage.Restart(); EndResetModel(); }
        var listEndMs = stage.Elapsed.TotalMilliseconds;
        MapPoints.Replace(state.Visible);
        var resetMs = timer.Elapsed.TotalMilliseconds;
        stage.Restart();
        Notify(nameof(Town), nameof(TownIndex), nameof(MaximumPrice), nameof(VisibleCount), nameof(MappedCount), nameof(FirstVisibleId), nameof(FilterSummary),
            nameof(SelectedId), nameof(SelectedMapKey), nameof(SelectionDetails));
        if (ScaleGate)
        {
            LastFilterCompletedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            MapRevision++;
            Notify(nameof(LastFilterStartedMs), nameof(LastFilterCompletedMs), nameof(MapRevision));
            Console.WriteLine(FormattableString.Invariant($"HDB_FILTER_STAGE revision={MapRevision} list-begin-ms={listBeginMs:F3} filter-ms={filterMs:F3} list-end-ms={listEndMs:F3} property-notify-ms={stage.Elapsed.TotalMilliseconds:F3} total-ms={timer.Elapsed.TotalMilliseconds:F3}"));
            Console.WriteLine($"HDB_SCALE_RESET {(long)resetMs} rows={VisibleCount} mapped={MappedCount}");
        }
    }
    private void Notify(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new(name));
    }
    private static string MatchSources(AddressMatch match)
    {
        var properties = string.Join(", ", match.PropertyCandidates.Select(p => p.SourceRow));
        var postals = string.Join("; ", match.PostalAssertions.GroupBy(p => p.SourceDataset)
            .Select(g => $"{(g.Key is null ? "ACRA B" : "ACRA " + g.Key)} CSV rows: {string.Join(", ", g.Select(p => p.SourceRow))}"));
        if (postals.Length == 0) postals = "ACRA B CSV rows: ";
        var historical = match.HistoricalOneMap is { } h
            ? $"\nHistorical first-page/first-result assertion: {h.Status}; postal {h.Postal}; cache write {h.UpdatedAt}. Returned identity/candidates unavailable."
            : "";
        return $"HDB property CSV rows: {properties}\n{postals}{historical}";
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
        if (startupProfile is not null) startupRoleReads++;
        var t = state.Visible[index.Row];
        return role switch
        {
            256 => t.Id, 257 => t.Location.Point?.Latitude, 258 => t.Location.Point?.Longitude,
            259 => t.Address, 260 => Money(t.Price), 261 => t.Town, 262 => $"Identity: {t.Match.Quality}\nCoordinates: {t.Location.Quality}", _ => null
        };
    }
}
