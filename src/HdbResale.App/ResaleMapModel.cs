using System.ComponentModel;
using System.Globalization;
using HdbResale.Domain;
using Qt.Bridge.Models;
using Qt.DotNet;
using Qt.Quick;

namespace HdbResale.App;

// The window's single source of state. Every figure comes from the HDB Resale Explorer Worker API (the same
// endpoint the web app reads); this model only filters, orders, selects and formats what the API publishes.
[QmlElement(Name = "Resales", Singleton = true)]
public sealed class ResaleMapModel : Model, INotifyPropertyChanged
{
    private readonly WorkerApiClient? api;
    private readonly ApiRequests requests = new();
    private readonly UiMutationQueue mutations = new();
    private AddressExplorer? explorer;
    private string loadError = "";
    private int loadGeneration;
    private readonly AddressDetailState details = new();
    private string[] towns = [AddressFilters.AllTowns];
    private string[] flatTypes = [AddressFilters.AllFlatTypes];
    private int maximumAvailablePrice = 1_000_000;
    public LocatedMapModel MapPoints { get; } = new();

    public ResaleMapModel()
    {
        try { api = new(WorkerApiClient.ConfiguredBaseAddress(Environment.GetEnvironmentVariable("HDB_API_BASE_URL"))); }
        catch (Exception e) when (e is ArgumentException or UriFormatException)
        { loadError = "HDB_API_BASE_URL is not a usable API address: " + e.Message; return; }
        Load();
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    // ---- API requests --------------------------------------------------------------------------------

    public bool Busy => requests.Busy;
    // Called by QML on a short timer while Busy: applies arrived responses on the UI thread.
    public void Pump() => mutations.Enqueue(() =>
    {
        requests.Drain();
        if (!requests.Busy) Notify(nameof(Busy));
    });
    public bool Loading => api is not null && explorer is null && loadError.Length == 0;
    public bool CanRetry => api is not null && explorer is null && loadError.Length > 0;
    public string StatusText => Loading ? "Loading addresses from the HDB Resale Explorer API…"
        : loadError.Length > 0 ? "Could not load addresses. " + loadError : "";
    public void Retry() => mutations.Enqueue(() => { if (CanRetry) Load(); });

    private void Load()
    {
        var generation = ++loadGeneration;
        loadError = "";
        requests.Start(async cancellation =>
        {
            var manifest = api!.GetManifestAsync(cancellation);
            var addresses = api.GetAddressesAsync(cancellation);
            await Task.WhenAll(manifest, addresses);
            return (Manifest: manifest.Result, Addresses: addresses.Result);
        }, loaded =>
        {
            if (generation != loadGeneration) return;
            BeginResetModel();
            try { explorer = new(loaded.Manifest, loaded.Addresses); }
            finally { EndResetModel(); }
            towns = [AddressFilters.AllTowns, .. loaded.Manifest.FilterOptions.Towns];
            flatTypes = [AddressFilters.AllFlatTypes, .. loaded.Manifest.FilterOptions.FlatTypes];
            var highest = loaded.Addresses.SelectMany(a => (a.MedianPriceByFlatType?.Values ?? []).Append(a.MedianPrice))
                .DefaultIfEmpty(0).Max();
            maximumAvailablePrice = checked((int)Math.Max(1_000_000, decimal.Ceiling(highest / 50_000) * 50_000));
            MapPoints.Replace(MapAddresses(), SelectedMapKey);
            NotifyPresentation();
            NotifyAll();
        }, error =>
        {
            if (generation != loadGeneration) return;
            loadError = Explain(error);
            NotifyAll();
        });
        NotifyAll();
    }

    private static string Explain(Exception error) => error is WorkerApiException ? error.Message : "Unexpected error: " + error.GetType().Name;

    // ---- Filters -------------------------------------------------------------------------------------

    public string TownsJson => System.Text.Json.JsonSerializer.Serialize(towns);
    public int TownIndex => Array.IndexOf(towns, Town);
    public string FlatTypesJson => System.Text.Json.JsonSerializer.Serialize(flatTypes);
    public int FlatTypeIndex => Array.IndexOf(flatTypes, FlatType);
    private AddressFilters Filters => explorer?.Filters ?? AddressFilters.Default;
    public string Town => Filters.Town;
    public string FlatType => Filters.FlatType;
    public int MinimumPrice => (int)Filters.MinimumPrice;
    public int MaximumPrice => (int)Filters.MaximumPrice;
    public int MaximumAvailablePrice => maximumAvailablePrice;
    public int RecencyMonths => Filters.RecencyMonths;
    public string DatasetLatestMonth => explorer?.LatestDatasetMonth.ToString() ?? "unavailable";

    public void SetTown(string town) => mutations.Enqueue(() => ApplyFilter(Filters with { Town = town }));
    public void SetFlatType(string flatType) => mutations.Enqueue(() => ApplyFilter(Filters with { FlatType = flatType }));
    public void SetMinimumPrice(int price) => mutations.Enqueue(() => ApplyFilter(Filters with { MinimumPrice = price }));
    public void SetMaximumPrice(int price) => mutations.Enqueue(() => ApplyFilter(Filters with { MaximumPrice = price }));
    public void SetRecencyMonths(int months) => mutations.Enqueue(() => ApplyFilter(Filters with { RecencyMonths = months }));
    public void ResetFilters() => mutations.Enqueue(() => ApplyFilter(AddressFilters.Default));

    // Address search narrows the loaded summaries locally; typing never calls the API.
    public string SearchText => explorer?.SearchText ?? "";
    public void SetSearchText(string text) => mutations.Enqueue(() =>
    {
        if (explorer is null || (text ?? "") == explorer.SearchText) return;
        BeginResetModel();
        try { explorer.Search(text); }
        finally { EndResetModel(); }
        MapPoints.Replace(MapAddresses(), SelectedMapKey);
        Notify(nameof(SearchText), nameof(AddressCount), nameof(MappedCount), nameof(FirstAddressKey), nameof(FilterSummary),
            nameof(SearchMatchesOutsideFilters));
        SelectionChanged();
    });
    // When a search finds nothing, how many addresses it would find without the filters.
    public int SearchMatchesOutsideFilters => explorer is { SearchText.Length: > 0, Addresses.Count: 0 }
        ? explorer.CountSearchMatchesIgnoringFilters() : 0;

    private void ApplyFilter(AddressFilters next)
    {
        if (explorer is null || next == explorer.Filters) return;
        BeginResetModel();
        try { explorer.Filter(next); }
        finally { EndResetModel(); }
        MapPoints.Replace(MapAddresses(), SelectedMapKey);
        Notify(nameof(Town), nameof(TownIndex), nameof(FlatType), nameof(FlatTypeIndex), nameof(MinimumPrice),
            nameof(MaximumPrice), nameof(RecencyMonths), nameof(AddressCount), nameof(MappedCount), nameof(FirstAddressKey),
            nameof(FilterSummary), nameof(SearchMatchesOutsideFilters));
        SelectionChanged();
    }

    public int AddressCount => explorer?.Addresses.Count ?? 0;
    public string FirstAddressKey => explorer?.Addresses.FirstOrDefault()?.AddressKey ?? "";
    public string FilterSummary => explorer is null ? "" :
        $"{AddressCount:N0} of {explorer.TotalAddressCount:N0} addresses" +
        (explorer.WindowStart is { } start ? $" with a registration since {start}" : "");

    // The map shows the same addresses as the list, with the figures for the selected flat type.
    private IReadOnlyList<MapAddress> MapAddresses() => explorer is null ? [] : explorer.Addresses.Select(a =>
    {
        var cohort = AddressSemantics.Cohort(a, FlatType);
        return new MapAddress(a.AddressKey, a.Coordinates.Lat, a.Coordinates.Lng, a.Address, cohort.TransactionCount,
            AddressSemantics.EffectiveMedianPrice(a, FlatType), cohort.LatestMonth);
    }).ToArray();

    // ---- Map -----------------------------------------------------------------------------------------

    private readonly BasemapConfiguration basemap = BasemapConfiguration.FromEnvironment();
    public string BasemapTileEndpoint => basemap.TileEndpoint;
    public string BasemapCacheDirectory => basemap.CacheDirectory;
    private readonly TileFailureStatus tileFailures = new();
    public bool TileFailuresRepeated => tileFailures.RepeatedFailures;
    public void RefreshTileStatus()
    {
        if (tileFailures.Observe(NativeTileStatus.ExhaustedRequests()))
            Notify(nameof(TileFailuresRepeated));
    }
    public int MappedCount => MapPoints.Count;
    public int PresentationCount => MapPoints.PresentationCount;
    public int InViewAddressCount => MapPoints.InViewCount;
    public int ClusterCount => MapPoints.ClusterCount;
    public bool MapViewportReady => MapPoints.ViewportReady;
    public string PresentationSummary => $"{InViewAddressCount:N0} mapped addresses in view · {ClusterCount:N0} groups + {PresentationCount - ClusterCount:N0} individual markers";
    public void SetMapViewport(double latitude, double longitude, double zoom, double width, double height) =>
        mutations.Enqueue(() => {
            if (MapPoints.SetViewport(new(latitude, longitude, zoom, width, height))) NotifyPresentation();
        });
    private void NotifyPresentation() => Notify(nameof(PresentationCount), nameof(InViewAddressCount), nameof(ClusterCount),
        nameof(MapViewportReady), nameof(PresentationSummary), nameof(SelectionMapStatus), nameof(SelectedLatitude),
        nameof(SelectedLongitude), nameof(SelectedLocated));

    // ---- Selection and details -----------------------------------------------------------------------

    private AddressSummary? Selected => explorer?.Selected;
    public string SelectedMapKey => Selected?.AddressKey ?? "";
    public int SelectedAddressIndex => Selected is null ? -1 : explorer!.IndexOf(Selected.AddressKey);
    public void SelectAddress(string key) => mutations.Enqueue(() => { explorer?.Select(key); SelectionChanged(); });
    public void SelectAddressAt(int index) => mutations.Enqueue(() => {
        if (explorer is not null && index >= 0 && index < explorer.Addresses.Count)
        { explorer.Select(explorer.Addresses[index].AddressKey); SelectionChanged(); }
    });
    private void SelectionChanged()
    {
        var key = SelectedMapKey;
        if (explorer is not null && api is not null)
            details.Select(key, explorer.LatestDatasetMonth, api.GetAddressDetailAsync, requests, DetailChanged);
        MapPoints.Select(key);
        NotifyPresentation();
        NotifySelection();
    }
    private void NotifySelection()
    {
        Notify(nameof(SelectedMapKey), nameof(SelectedAddressIndex), nameof(SelectedHeading), nameof(SelectedMetrics),
            nameof(SelectedLease), nameof(SelectedLocation));
        NotifyDetail();
    }
    // A detail response leaves the selection as it was, so only what depends on the details changes: the list keeps
    // its scroll position and the selection is not announced again.
    private void NotifyDetail() => Notify(nameof(DetailStatus), nameof(DetailReady), nameof(CanRetryDetail),
        nameof(RecentTransactionsJson), nameof(TrendJson));

    public bool CanRetryDetail => details.CanRetry;
    public void RetryDetail() => mutations.Enqueue(() =>
    {
        if (explorer is not null && api is not null)
            details.Retry(explorer.LatestDatasetMonth, api.GetAddressDetailAsync, requests, DetailChanged);
    });
    private void DetailChanged()
    {
        Notify(nameof(Busy));
        NotifyDetail();
    }

    public string SelectionMapStatus => Selected is null ? "" : MapPoints.SelectedInView
        ? "Selected address is highlighted on this map." : "Selected address is outside this map view.";
    public bool SelectedLocated => Selected is not null;
    public double SelectedLatitude => Selected?.Coordinates.Lat ?? 1.3521;
    public double SelectedLongitude => Selected?.Coordinates.Lng ?? 103.8198;
    public string SelectedHeading => Selected?.Address ?? "Choose an address";
    public string SelectedMetrics => Selected is null
        ? "The list and map show the same addresses. Select one to see its registrations."
        : BuyerPresentation.Metrics(Selected, FlatType, details.Detail, explorer!.LatestDatasetMonth);
    public string SelectedLease => Selected is null ? "" : BuyerPresentation.Lease(Selected, DateTime.Now.Year);
    public string SelectedLocation => Selected is null ? "" : BuyerPresentation.Location(Selected);
    public bool DetailReady => details.Detail is not null && details.Detail.Summary.AddressKey == SelectedMapKey;
    public string DetailStatus => Selected is null ? "" : details.Loading ? "Loading registrations…" : details.Error;
    public string TrendJson => System.Text.Json.JsonSerializer.Serialize(details.Trend);
    public string RecentTransactionsJson => BuyerPresentation.RecentJson(DetailReady ? details.Detail : null);

    // ---- About ---------------------------------------------------------------------------------------

    public string AboutText => "HDB Resale Explorer 0.1.0 (release candidate)\nIndependent desktop research tool. Not affiliated with HDB, SLA or the Singapore Government.\nHistorical resale records are not current listings, valuations, affordability advice or eligibility decisions.\nApplication source: GPL-3.0-or-later. Qt Graphs: GPLv3; Qt/Bridge and other components retain their terms. Public data and OneMap assets are separate; see LICENSE and THIRD_PARTY_NOTICES.md.\nRepository: https://github.com/shenghaoc/hdb-resale-qt";
    public string DatasetSummary => explorer is null ? StatusText : string.Create(CultureInfo.InvariantCulture,
        $"Data: HDB Resale Explorer API ({api!.BaseAddress.Host}) · published {explorer.Manifest.GeneratedAt ?? "unknown"} · registrations {explorer.Manifest.DataWindow.MinMonth} to {explorer.Manifest.DataWindow.MaxMonth} · {explorer.Manifest.Counts.Transactions:N0} transactions at {explorer.Manifest.Counts.Blocks:N0} addresses.");
    public string ApiGate => Environment.GetEnvironmentVariable("HDB_API_GATE") ?? "";
    public bool PackageSmoke => Environment.GetEnvironmentVariable("HDB_PACKAGE_SMOKE") == "1";

    private void NotifyAll() => Notify(nameof(Busy), nameof(Loading), nameof(CanRetry), nameof(StatusText), nameof(TownsJson),
        nameof(TownIndex), nameof(FlatTypesJson), nameof(FlatTypeIndex), nameof(Town), nameof(FlatType), nameof(MinimumPrice),
        nameof(MaximumPrice), nameof(MaximumAvailablePrice), nameof(RecencyMonths), nameof(DatasetLatestMonth), nameof(AddressCount),
        nameof(MappedCount), nameof(FirstAddressKey), nameof(FilterSummary), nameof(DatasetSummary), nameof(SearchText),
        nameof(SearchMatchesOutsideFilters));
    private void Notify(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new(name));
    }

    // ---- Address list model --------------------------------------------------------------------------

    public override ModelIndex Parent(ModelIndex index) => ModelIndex.Empty;
    public override ModelIndex Index(int row, int column, ModelIndex parent) =>
        parent?.IsValid == true || column != 0 || row < 0 || row >= AddressCount
            ? ModelIndex.Empty : new(row, column);
    public override int RowCount(ModelIndex parent) => parent?.IsValid == true ? 0 : AddressCount;
    public override int ColumnCount(ModelIndex parent) => 1;
    public override Dictionary<int, string> RoleNames() => new()
    {
        [259] = "address", [260] = "priceLabel", [261] = "townName", [262] = "locationLabel",
        [263] = "addressKey", [265] = "saleCount", [266] = "flatTypes", [267] = "latestMonth"
    };
    public override object? Data(ModelIndex index, int role)
    {
        if (explorer is null || index is not { IsValid: true } || index.Row < 0 || index.Row >= AddressCount) return null;
        var a = explorer.Addresses[index.Row];
        var cohort = AddressSemantics.Cohort(a, FlatType);
        var median = BuyerPresentation.Money(AddressSemantics.EffectiveMedianPrice(a, FlatType));
        return role switch
        {
            259 => a.Address, 260 => median, 261 => a.Town,
            262 => "Approximate block location" + (string.IsNullOrWhiteSpace(a.PostalCode) ? "" : ", postal code " + a.PostalCode),
            263 => a.AddressKey,
            265 => cohort.TransactionCount,
            266 => string.Join(", ", a.FlatTypes),
            267 => cohort.LatestMonth,
            _ => null
        };
    }
}
