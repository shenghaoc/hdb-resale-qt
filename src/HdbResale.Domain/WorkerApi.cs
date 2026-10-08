using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HdbResale.Domain;

// Read-only client for the HDB Resale Explorer Worker API: the same `/api/*` routes the web app reads.
// The Worker owns the data and its storage; this client never sees a database, provider or snapshot.
// Responses are validated before use, and a response that does not hold is refused as a whole.

public sealed record DataWindow(string MinMonth, string MaxMonth);
public sealed record FilterOptions(IReadOnlyList<string> Towns, IReadOnlyList<string> FlatTypes, IReadOnlyList<string> FlatModels);
public sealed record DatasetCounts(int Blocks, int Transactions, int Towns, int MrtStations);
public sealed record DatasetManifest(string SchemaVersion, DataWindow DataWindow, FilterOptions FilterOptions, DatasetCounts Counts)
{
    public string? GeneratedAt { get; init; }
    public YearMonth LatestMonth => ApiValidation.Month(DataWindow.MaxMonth, "dataWindow.maxMonth");
}

public sealed record ApiCoordinates(double Lat, double Lng);
public sealed record NearestMrt(string StationName, decimal DistanceMeters, decimal WalkingTimeSeconds);
public sealed record FlatTypeCohort(int TransactionCount, string LatestMonth, IReadOnlyList<decimal> FloorAreaRange, IReadOnlyList<string> FlatModels);

// One address as the API summarizes it: statistics cover the dataset's latest 24 source months, or the
// address's whole history when it had no sale in them; per-type figures follow the same rule per flat type.
public record AddressSummary(string AddressKey, string Town, string Block, string StreetName, ApiCoordinates Coordinates,
    decimal MedianPrice, decimal PricePerSqmMedian, int TransactionCount, IReadOnlyList<decimal> FloorAreaRange,
    IReadOnlyList<int> LeaseCommenceRange, string LatestMonth, IReadOnlyList<string> AvailableDateRange,
    IReadOnlyList<string> FlatTypes, IReadOnlyList<string> FlatModels)
{
    public string? DisplayName { get; init; }
    public IReadOnlyDictionary<string, decimal>? MedianPriceByFlatType { get; init; }
    public IReadOnlyDictionary<string, decimal>? MedianPricePerSqmByFlatType { get; init; }
    public IReadOnlyDictionary<string, FlatTypeCohort>? FlatTypeCohorts { get; init; }
    public NearestMrt? NearestMrt { get; init; }
    public string? PostalCode { get; init; }
    [JsonIgnore] public string Address => $"{Block} {StreetName}";
}

public sealed record AddressDetailSummary(string AddressKey, string Town, string Block, string StreetName, ApiCoordinates Coordinates,
    decimal MedianPrice, decimal PricePerSqmMedian, int TransactionCount, IReadOnlyList<decimal> FloorAreaRange,
    IReadOnlyList<int> LeaseCommenceRange, string LatestMonth, IReadOnlyList<string> AvailableDateRange,
    IReadOnlyList<string> FlatTypes, IReadOnlyList<string> FlatModels, IReadOnlyList<decimal> PriceIqr)
    : AddressSummary(AddressKey, Town, Block, StreetName, Coordinates, MedianPrice, PricePerSqmMedian, TransactionCount,
        FloorAreaRange, LeaseCommenceRange, LatestMonth, AvailableDateRange, FlatTypes, FlatModels);

// The address's newest registrations (at most 20, every flat type), as recorded at the source.
public sealed record AddressTransaction(string Id, string Month, string FlatType, string StoreyRange, decimal FloorAreaSqm,
    string FlatModel, int LeaseCommenceDate, string RemainingLease, decimal ResalePrice, decimal PricePerSqm);
// One month with at least one registration at the address; months without a sale are absent.
public sealed record AddressTrendPoint(string Month, decimal MedianPrice, int TransactionCount, decimal MedianPricePerSqm);
public sealed record AddressDetail(AddressDetailSummary Summary, IReadOnlyList<AddressTransaction> RecentTransactions,
    IReadOnlyList<AddressTrendPoint> MonthlyTrend);

public sealed class WorkerApiException(string message, HttpStatusCode? status = null, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? Status { get; } = status;
}

public sealed class WorkerApiClient : IDisposable
{
    public static readonly Uri ProductionBaseAddress = new("https://hdb-resale-visualizer.shenghaoc.workers.dev/");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        NumberHandling = JsonNumberHandling.Strict,
        // A missing or null field this app relies on refuses the response instead of becoming 0 or null.
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };
    private readonly HttpClient http;

    public WorkerApiClient(Uri baseAddress, HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (!baseAddress.IsAbsoluteUri || baseAddress.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(baseAddress.UserInfo)
            || !string.IsNullOrEmpty(baseAddress.Query) || !string.IsNullOrEmpty(baseAddress.Fragment))
            throw new ArgumentException("The API address must be an absolute http(s) URL without credentials, query or fragment.", nameof(baseAddress));
        if (baseAddress.Scheme == "http" && !baseAddress.IsLoopback)
            throw new ArgumentException("Plain http is only accepted for a loopback test server.", nameof(baseAddress));
        BaseAddress = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");
        http = new HttpClient(handler ?? new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = false,
        }, disposeHandler: true)
        { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HdbResaleExplorer/0.1.0");
    }

    public Uri BaseAddress { get; }

    // HDB_API_BASE_URL selects another deployment (a preview or a local test server); production otherwise.
    public static Uri ConfiguredBaseAddress(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? ProductionBaseAddress : new Uri(configured.Trim(), UriKind.Absolute);

    public async Task<DatasetManifest> GetManifestAsync(CancellationToken cancellation = default) =>
        ApiValidation.Manifest(await GetAsync<DatasetManifest>("api/manifest", cancellation)
            ?? throw new WorkerApiException("The API has no published dataset yet.", HttpStatusCode.NotFound));

    public async Task<IReadOnlyList<AddressSummary>> GetAddressesAsync(CancellationToken cancellation = default) =>
        ApiValidation.Addresses(await GetAsync<AddressSummary[]>("api/block-summaries", cancellation)
            ?? throw new WorkerApiException("The API returned no addresses.", HttpStatusCode.NotFound));

    // Null when the API has no detail for the address (404).
    public async Task<AddressDetail?> GetAddressDetailAsync(string addressKey, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressKey);
        var detail = await GetAsync<AddressDetail>("api/details/" + Uri.EscapeDataString(addressKey), cancellation);
        return detail is null ? null : ApiValidation.Detail(detail, addressKey);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellation) where T : class
    {
        HttpResponseMessage response;
        try { response = await http.GetAsync(new Uri(BaseAddress, path), HttpCompletionOption.ResponseHeadersRead, cancellation); }
        catch (HttpRequestException e) { throw new WorkerApiException("The HDB Resale API could not be reached.", null, e); }
        catch (TaskCanceledException e) when (!cancellation.IsCancellationRequested)
        { throw new WorkerApiException("The HDB Resale API did not answer in time.", null, e); }
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode)
                throw new WorkerApiException($"The HDB Resale API answered {(int)response.StatusCode}.", response.StatusCode);
            try
            {
                await using var body = await response.Content.ReadAsStreamAsync(cancellation);
                return await JsonSerializer.DeserializeAsync<T>(body, Json, cancellation)
                    ?? throw new WorkerApiException("The HDB Resale API returned an empty response.", response.StatusCode);
            }
            catch (JsonException e) { throw new WorkerApiException("The HDB Resale API returned data this app cannot read.", response.StatusCode, e); }
        }
    }

    public void Dispose() => http.Dispose();
}

internal static class ApiValidation
{
    internal static YearMonth Month(string? value, string field) =>
        value is not null && YearMonth.TryParse(value, out var month) ? month! : throw Invalid(field);

    private static WorkerApiException Invalid(string field) =>
        new($"The HDB Resale API returned an invalid {field}.");

    private static void Text(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Invalid(field);
    }

    private static void Pair<T>(IReadOnlyList<T>? value, string field) where T : IComparable<T>
    {
        if (value is not { Count: 2 } || value[0].CompareTo(value[1]) > 0) throw Invalid(field);
    }

    internal static DatasetManifest Manifest(DatasetManifest manifest)
    {
        if (manifest.DataWindow is null || manifest.FilterOptions is null || manifest.Counts is null) throw Invalid("manifest");
        var min = Month(manifest.DataWindow.MinMonth, "dataWindow.minMonth");
        var max = Month(manifest.DataWindow.MaxMonth, "dataWindow.maxMonth");
        if ((min.Year, min.Month).CompareTo((max.Year, max.Month)) > 0) throw Invalid("dataWindow");
        if (manifest.FilterOptions.Towns is null || manifest.FilterOptions.FlatTypes is null || manifest.FilterOptions.FlatModels is null
            || manifest.FilterOptions.Towns.Any(string.IsNullOrWhiteSpace) || manifest.FilterOptions.FlatTypes.Any(string.IsNullOrWhiteSpace))
            throw Invalid("filterOptions");
        return manifest;
    }

    internal static IReadOnlyList<AddressSummary> Addresses(IReadOnlyList<AddressSummary> addresses)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var address in addresses)
        {
            if (address is null) throw Invalid("address");
            Summary(address, "address");
            if (!keys.Add(address.AddressKey)) throw Invalid("address key (duplicate)");
        }
        return addresses;
    }

    private static void Summary(AddressSummary address, string field)
    {
        Text(address.AddressKey, field + " key");
        Text(address.Town, field + " town");
        Text(address.Block, field + " block");
        Text(address.StreetName, field + " street");
        if (address.Coordinates is not { } point || !double.IsFinite(point.Lat) || point.Lat is < -90 or > 90
            || !double.IsFinite(point.Lng) || point.Lng is < -180 or > 180)
            throw Invalid(field + " coordinates");
        if (address.TransactionCount < 1 || address.MedianPrice < 0 || address.PricePerSqmMedian < 0)
            throw Invalid(field + " statistics");
        Pair(address.FloorAreaRange, field + " floorAreaRange");
        Pair(address.LeaseCommenceRange, field + " leaseCommenceRange");
        Month(address.LatestMonth, field + " latestMonth");
        if (address.AvailableDateRange is not { Count: 2 }) throw Invalid(field + " availableDateRange");
        Month(address.AvailableDateRange[0], field + " availableDateRange");
        Month(address.AvailableDateRange[1], field + " availableDateRange");
        if (address.FlatTypes is null || address.FlatTypes.Count == 0 || address.FlatModels is null) throw Invalid(field + " flat types");
        foreach (var (type, cohort) in address.FlatTypeCohorts ?? new Dictionary<string, FlatTypeCohort>())
        {
            if (cohort is null || cohort.TransactionCount < 1 || cohort.FlatModels is null) throw Invalid(field + " cohort " + type);
            Month(cohort.LatestMonth, field + " cohort latestMonth");
            Pair(cohort.FloorAreaRange, field + " cohort floorAreaRange");
        }
    }

    internal static AddressDetail Detail(AddressDetail detail, string addressKey)
    {
        if (detail.Summary is null || detail.RecentTransactions is null || detail.MonthlyTrend is null) throw Invalid("address detail");
        Summary(detail.Summary, "address detail");
        if (detail.Summary.AddressKey != addressKey) throw Invalid("address detail key");
        Pair(detail.Summary.PriceIqr, "address detail priceIqr");
        foreach (var row in detail.RecentTransactions)
        {
            if (row is null) throw Invalid("transaction");
            Text(row.Id, "transaction id");
            Month(row.Month, "transaction month");
            Text(row.FlatType, "transaction flat type");
            if (row.ResalePrice <= 0 || row.FloorAreaSqm <= 0) throw Invalid("transaction price or area");
        }
        string? previous = null;
        foreach (var point in detail.MonthlyTrend)
        {
            if (point is null) throw Invalid("trend point");
            Month(point.Month, "trend month");
            if (previous is not null && string.CompareOrdinal(previous, point.Month) >= 0) throw Invalid("trend order");
            if (point.TransactionCount < 1 || point.MedianPrice <= 0) throw Invalid("trend point");
            previous = point.Month;
        }
        return detail;
    }
}
