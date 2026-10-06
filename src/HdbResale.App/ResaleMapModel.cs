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
    private BuyerTrendData trend = BuyerTrendData.Empty;
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
    public int GateOtherBudgetCount { get; }
    public int GateOtherBudgetMapped { get; }
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
        FlatTypes = new[] { "All flat types" }.Concat(import.Accepted.Select(t => t.FlatType).Distinct().Order(StringComparer.Ordinal)).ToArray();
        MeasureStartup("town-labels");
        state = new(import.Accepted);
        // The startup-only probe uses all prices to exercise every located address marker.
        if (StartupProbe) state.Filter("All towns", MaximumAvailablePrice);
        MeasureStartup("state-construction");
        MapPoints = new(state.MappedAddresses, startupProfile is null ? null : MeasureStartup);
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
            if (ScaleTransitions || PresentationGate)
            {
                var allTown = import.Accepted.Where(t => t.Town == GateTown).ToArray();
                var otherTownName = GateOtherTown;
                var otherTown = import.Accepted.Where(t => t.Town == otherTownName).ToArray();
                GateAllTownCount = allTown.Length;
                GateAllTownMapped = BlockSummaries.Located(allTown).Count;
                GateOtherTownCount = otherTown.Length;
                GateOtherTownMapped = BlockSummaries.Located(otherTown).Count;
                var otherBudget=otherTown.Where(t=>t.Price<=500_000).ToArray();
                GateOtherBudgetCount=otherBudget.Length;
                GateOtherBudgetMapped=BlockSummaries.Located(otherBudget).Count;
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
        Console.WriteLine($"HDB_STARTUP_READY view={StartupView} rows={VisibleCount} markers={MappedCount} sidebar-role-reads={startupRoleReads} map-role-reads={MapPoints.StartupRoleReads} presentation={PresentationCount} in-view={InViewAddressCount} clusters={ClusterCount} viewport-width={MapViewportWidth} viewport-height={MapViewportHeight} zoom={MapViewportZoom} center-latitude={MapViewportLatitude} center-longitude={MapViewportLongitude}");
        if (Environment.GetEnvironmentVariable("HDB_STARTUP_HEAP") == "1")
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            MeasureStartup("diagnostic-retained-native");
        }
    }

    // Explicit test opt-in; normal application state and fixture are unchanged.
    public bool PresentationGate => Environment.GetEnvironmentVariable("HDB_PRESENTATION_GATE") == "1";
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
    public int PresentationCount => MapPoints.PresentationCount;
    public int InViewAddressCount => MapPoints.InViewCount;
    public int ClusterCount => MapPoints.ClusterCount;
    public bool MapViewportReady => MapPoints.ViewportReady;
    public string PresentationSummary => $"{InViewAddressCount:N0} mapped addresses in view · {ClusterCount:N0} groups + {PresentationCount - ClusterCount:N0} individual markers";
    public string SelectionMapStatus => state.SelectedAddress is null ? "" : !state.SelectedAddress.IsMapped
        ? "Selected address has no mapped coordinates." : MapPoints.SelectedInView
        ? "Selected address is highlighted on this map." : "Selected address is outside this map view.";
    public double SelectedLatitude => state.SelectedAddress is { IsMapped: true } b ? b.Latest.Location.Point!.Latitude : 1.3521;
    public double SelectedLongitude => state.SelectedAddress is { IsMapped: true } b ? b.Latest.Location.Point!.Longitude : 103.8198;
    public bool SelectedLocated => state.SelectedAddress?.IsMapped == true;
    public double MapViewportLatitude => MapPoints.Viewport?.Latitude ?? 0;
    public double MapViewportLongitude => MapPoints.Viewport?.Longitude ?? 0;
    public double MapViewportZoom => MapPoints.Viewport?.Zoom ?? 0;
    public double MapViewportWidth => MapPoints.Viewport?.Width ?? 0;
    public double MapViewportHeight => MapPoints.Viewport?.Height ?? 0;
    public int PresentationRevision { get; private set; }
    public void SetMapViewport(double latitude, double longitude, double zoom, double width, double height) =>
        mutations.Enqueue(() => {
            if (MapPoints.SetViewport(new(latitude, longitude, zoom, width, height))) NotifyPresentation();
        });
    private void NotifyPresentation()
    {
        PresentationRevision++;
        Notify(nameof(PresentationCount), nameof(InViewAddressCount), nameof(ClusterCount), nameof(MapViewportReady),
            nameof(PresentationSummary), nameof(SelectionMapStatus), nameof(SelectedLatitude), nameof(SelectedLongitude),
            nameof(SelectedLocated), nameof(PresentationRevision), nameof(MapViewportLatitude), nameof(MapViewportLongitude),
            nameof(MapViewportZoom), nameof(MapViewportWidth), nameof(MapViewportHeight));
    }
    public int MapRevision { get; private set; }
    public double LastFilterStartedMs { get; private set; }
    public double LastFilterCompletedMs { get; private set; }
    public string GateTruthRowsJson => ScaleGate ? System.Text.Json.JsonSerializer.Serialize(MapPoints.Summaries.Select(b => new {
        mapKey=b.Key, transactionId=b.Latest.Id, transactionCount=b.Count, latitude=b.Latest.Location.Point!.Latitude,
        longitude=b.Latest.Location.Point.Longitude, address=b.Latest.Address })) : "[]";
    public string GateTownSelectionId => ScaleGate ? MapPoints.Summaries.FirstOrDefault(b => b.Latest.Town == GateTown)?.Latest.Id ?? "" : "";
    public long GateInsertedCount => MapPoints.InsertedCount;
    public long GateRemovedCount => MapPoints.RemovedCount;
    public string GateMapRowsJson => ScaleGate ? MapPoints.GateRowsJson : "[]";
    public string GateTown => import.Accepted.FirstOrDefault()?.Town ?? "All towns";
    public string FirstVisibleId => state.Visible.FirstOrDefault()?.Id ?? "";
    // Immutable import: compute control labels once, rather than scanning the corpus per binding read.
    private string[] Towns { get; }
    private string[] FlatTypes { get; }
    public string FlatTypesJson => System.Text.Json.JsonSerializer.Serialize(FlatTypes);
    public int FlatTypeIndex => Array.IndexOf(FlatTypes, FlatType);
    public string FlatType => state.FlatType;
    public int MinimumPrice => (int)state.MinimumPrice;
    public int RecencyMonths => state.RecencyMonths;
    public string DatasetLatestMonth => state.LatestDatasetMonth?.ToString() ?? "unavailable";
    public int AddressCount => state.Addresses.Count;
    public int SelectedAddressIndex => state.SelectedAddress is null ? -1 : state.Addresses.ToList().FindIndex(b => b.Key == state.SelectedAddress.Key);
    public string FirstAddressKey => state.Addresses.FirstOrDefault()?.Key ?? "";
    public bool PackageSmoke => Environment.GetEnvironmentVariable("HDB_PACKAGE_SMOKE") == "1";
    public bool BuyerGate => Environment.GetEnvironmentVariable("HDB_BUYER_GATE") == "1";
    public string GateBuyerExpectedJson => BuyerGate ? File.ReadAllText(Environment.GetEnvironmentVariable("HDB_BUYER_EXPECTATION")!) : "[]";
    public string BuyerStateJson => BuyerPresentation.StateJson(state);
    public string TrendJson => System.Text.Json.JsonSerializer.Serialize(trend);
    public string RecentTransactionsJson => BuyerPresentation.RecentJson(state);
    public string SelectedHeading => state.SelectedAddress is { } b ? b.Latest.Address : "Choose an address";
    public string SelectedMetrics => BuyerPresentation.Metrics(state);
    public string SelectedLease => BuyerPresentation.Lease(state);
    public string SelectedEvidence => state.SelectedAddress is { } b ? $"Identity: {string.Join(", ", b.MatchQualities)}\nCoordinates: {string.Join(", ", b.CoordinateQualities)}\n{b.Latest.Match.Reason}\n{MatchSources(b.Latest.Match)}\n{b.Latest.Location.Source}" : "";
    public string AboutText => "HDB Resale Explorer 0.1.0 (release candidate)\nIndependent desktop research tool. Not affiliated with HDB, SLA or the Singapore Government.\nHistorical resale records are not current listings, valuations, affordability advice or eligibility decisions.\nApplication source: GPL-3.0-or-later. Qt Graphs: GPLv3; Qt/Bridge and other components retain their terms. Public data and OneMap assets are separate; see LICENSE and THIRD_PARTY_NOTICES.md.\nRepository: https://github.com/shenghaoc/hdb-resale-qt";
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
        ? $"{SelectedHeading}\n{SelectedMetrics}\n{SelectedLease}\n{SelectedEvidence}\nRepresentative local ID {t.Id}"
        : "Select an address in the results or an individual map marker.";
    public string FilterSummary => $"{AddressCount:N0} addresses · {VisibleCount:N0} matching transactions · {MappedCount:N0} mapped addresses · {VisibleCount - LocatedTransactions:N0} transactions without coordinates";
    public string ImportSummary => $"Import: {import.Accepted.Count} accepted · {import.MatchedCount} matched · {import.AmbiguousCount} ambiguous · {import.UnmatchedCount} unmatched · {import.Rejected.Count} rejected · {import.Diagnostics.Count} diagnostics.";
    public string ImportDiagnostics => string.Join("\n", import.Diagnostics.Select(d => $"{d.File}:{d.Row}: {d.Message}"));

    public void SetTown(string town)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        mutations.Enqueue(() => ApplyFilter(town, FlatType, MinimumPrice, MaximumPrice, RecencyMonths));
    }
    public void SetFlatType(string flatType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flatType);
        mutations.Enqueue(() => ApplyFilter(Town, flatType, MinimumPrice, MaximumPrice, RecencyMonths));
    }
    public void SetMinimumPrice(int price)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        mutations.Enqueue(() => ApplyFilter(Town, FlatType, price, MaximumPrice, RecencyMonths));
    }
    public void SetMaximumPrice(int price)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        mutations.Enqueue(() => ApplyFilter(Town, FlatType, MinimumPrice, price, RecencyMonths));
    }
    public void SetRecencyMonths(int months) => mutations.Enqueue(() => ApplyFilter(Town, FlatType, MinimumPrice, MaximumPrice, months));
    public void SetBuyerFilters(string town, string type, int minimum, int maximum, int months) =>
        mutations.Enqueue(() => ApplyFilter(town, type, minimum, maximum, months));
    public void ResetFilters() => mutations.Enqueue(() => ApplyFilter("All towns", "All flat types", 0, 1_000_000, 0));
    public void SelectAddress(string key) => mutations.Enqueue(() => { state.SelectAddress(key); SelectionChanged(); });
    public void SelectTransaction(string id) => mutations.Enqueue(() => { state.Select(id); SelectionChanged(); });
    public void SelectAddressAt(int index) => mutations.Enqueue(() => {
        if (index >= 0 && index < state.Addresses.Count) { state.SelectAddress(state.Addresses[index].Key); SelectionChanged(); }
    });
    private void SelectionChanged()
    {
        trend = BuyerTrend.Build(state);
        MapPoints.Select(SelectedMapKey);
        NotifyPresentation();
        NotifySelection();
    }
    private void NotifySelection() => Notify(nameof(SelectedId), nameof(SelectedMapKey), nameof(SelectionDetails),
        nameof(SelectedAddressIndex), nameof(SelectedHeading), nameof(SelectedMetrics), nameof(SelectedLease), nameof(SelectedEvidence),
        nameof(RecentTransactionsJson), nameof(BuyerStateJson), nameof(TrendJson));
    private void ApplyFilter(string town, string flatType, int minimum, int maximum, int months)
    {
        if (town == Town && flatType == FlatType && minimum == MinimumPrice && maximum == MaximumPrice && months == RecencyMonths) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        ArgumentException.ThrowIfNullOrWhiteSpace(flatType);
        ArgumentOutOfRangeException.ThrowIfNegative(minimum);
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        if (months is not (0 or 12 or 24)) throw new ArgumentOutOfRangeException(nameof(months));
        if (ScaleGate || BuyerGate) LastFilterStartedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timer = Stopwatch.StartNew();
        var stage = Stopwatch.StartNew();
        BeginResetModel();
        var listBeginMs = stage.Elapsed.TotalMilliseconds;
        stage.Restart();
        double filterMs;
        try { state.Filter(town, flatType, minimum, maximum, months); filterMs = stage.Elapsed.TotalMilliseconds; }
        finally { stage.Restart(); EndResetModel(); }
        var listEndMs = stage.Elapsed.TotalMilliseconds;
        MapPoints.Replace(state.MappedAddresses, SelectedMapKey);
        NotifyPresentation();
        var resetMs = timer.Elapsed.TotalMilliseconds;
        stage.Restart();
        Notify(nameof(Town), nameof(TownIndex), nameof(FlatType), nameof(FlatTypeIndex), nameof(MinimumPrice), nameof(MaximumPrice),
            nameof(RecencyMonths), nameof(VisibleCount), nameof(AddressCount), nameof(MappedCount), nameof(FirstVisibleId), nameof(FirstAddressKey), nameof(FilterSummary));
        trend = BuyerTrend.Build(state);
        NotifySelection();
        if (ScaleGate || BuyerGate)
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
        parent?.IsValid == true || column != 0 || row < 0 || row >= AddressCount
            ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : AddressCount;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [256] = "transactionId", [257] = "latitude", [258] = "longitude",
        [259] = "address", [260] = "priceLabel", [261] = "townName", [262] = "locationLabel",
        [263] = "addressKey", [264] = "summaryLabel"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if (index is not { IsValid: true } || index.Row < 0 || index.Row >= AddressCount) return null;
        if (startupProfile is not null) startupRoleReads++;
        var b = state.Addresses[index.Row];
        var t = b.Latest;
        return role switch
        {
            256 => t.Id, 257 => t.Location.Point?.Latitude, 258 => t.Location.Point?.Longitude,
            259 => t.Address, 260 => Money(b.MedianPrice), 261 => t.Town,
            262 => $"Identity: {string.Join(", ", b.MatchQualities)} · Coordinates: {string.Join(", ", b.CoordinateQualities)}",
            263 => b.Key, 264 => $"{b.Count:N0} sales · median {Money(b.MedianPrice)}\n{string.Join(", ", b.FlatTypes)} · latest {b.Latest.Facts.Month}", _ => null
        };
    }
}
