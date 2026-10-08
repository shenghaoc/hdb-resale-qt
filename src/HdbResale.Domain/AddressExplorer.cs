namespace HdbResale.Domain;

// The filters this app offers. Prices bound the address's median, not individual sales; the window keeps
// addresses with a registration in the latest 12 or 24 source months.
public sealed record AddressFilters(string Town, string FlatType, decimal MinimumPrice, decimal MaximumPrice, int RecencyMonths)
{
    public const string AllTowns = "All towns";
    public const string AllFlatTypes = "All flat types";
    public static AddressFilters Default { get; } = new(AllTowns, AllFlatTypes, 0, 1_000_000, 0);
}

// The address figures that apply to a selected flat type, falling back to the whole address exactly as the
// web app does when the API has no figure for that type.
public sealed record EffectiveCohort(int TransactionCount, string LatestMonth, IReadOnlyList<decimal> FloorAreaRange,
    IReadOnlyList<string> FlatModels, bool IsTypeSpecific);

// The web app's semantics for these filters, mirrored rather than shared (hdb-resale-visualizer
// shared/product/filtering.ts: `matchesFilter`, `resolveEffectiveMedianPrice`, `resolveEffectiveBlockCohort`;
// shared/product/lease.ts). tests/HdbResale.Tests/ProductCoreParityTests.cs runs the web's golden fixtures.
public static class AddressSemantics
{
    public const int LeaseYears = 99;

    public static string CanonicalFlatType(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized == "MULTI GENERATION" ? "MULTI-GENERATION" : normalized;
    }

    private static string? Selected(string? flatType) =>
        string.IsNullOrWhiteSpace(flatType) || flatType == AddressFilters.AllFlatTypes ? null : CanonicalFlatType(flatType);

    public static decimal EffectiveMedianPrice(AddressSummary address, string? flatType) =>
        Selected(flatType) is { } type && address.MedianPriceByFlatType?.TryGetValue(type, out var median) == true
            ? median : address.MedianPrice;

    public static decimal EffectivePricePerSqm(AddressSummary address, string? flatType) =>
        Selected(flatType) is { } type && address.MedianPricePerSqmByFlatType?.TryGetValue(type, out var median) == true
            ? median : address.PricePerSqmMedian;

    public static EffectiveCohort Cohort(AddressSummary address, string? flatType) =>
        Selected(flatType) is { } type && address.FlatTypeCohorts?.TryGetValue(type, out var cohort) == true
            ? new(cohort.TransactionCount, cohort.LatestMonth, cohort.FloorAreaRange, cohort.FlatModels, true)
            : new(address.TransactionCount, address.LatestMonth, address.FloorAreaRange, address.FlatModels, false);

    // The first month a "latest N months" window keeps, ending at the dataset's latest source month.
    public static string? WindowStart(YearMonth latest, int recencyMonths)
    {
        if (recencyMonths <= 0) return null;
        var index = latest.Year * 12 + latest.Month - 1 - (recencyMonths - 1);
        return $"{index / 12:D4}-{index % 12 + 1:D2}";
    }

    public static bool Matches(AddressSummary address, string? town, string? flatType, decimal? budgetMin, decimal? budgetMax,
        string? startMonth)
    {
        if (!string.IsNullOrEmpty(town) && town != AddressFilters.AllTowns && address.Town != town) return false;
        var type = Selected(flatType);
        if (type is not null && !address.FlatTypes.Any(t => CanonicalFlatType(t) == type)) return false;
        // A refinement of a selected type needs that type's own figures; the web app never guesses them.
        var typeCohort = type is null ? null : address.FlatTypeCohorts?.GetValueOrDefault(type);
        if (type is not null && startMonth is not null && typeCohort is null) return false;
        var price = EffectiveMedianPrice(address, type);
        if (budgetMin is { } min && price < min) return false;
        if (budgetMax is { } max && price > max) return false;
        var latestMonth = typeCohort?.LatestMonth ?? address.LatestMonth;
        if (startMonth is not null && string.CompareOrdinal(latestMonth, startMonth) < 0) return false;
        return true;
    }

    // The web app's estimate: a 99-year lease from the commencement year, at the given calendar year.
    public static (int Minimum, int Maximum) RemainingLeaseYears(IReadOnlyList<int> leaseCommenceRange, int currentYear) =>
        (LeaseYears - (currentYear - leaseCommenceRange[0]), LeaseYears - (currentYear - leaseCommenceRange[1]));
}

// The dataset as the API publishes it, filtered and ordered the way the web app lists it: by the median that
// applies to the selected flat type, lowest first, keeping the API's order among equal medians.
public sealed class AddressExplorer
{
    private readonly IReadOnlyList<AddressSummary> all;

    public AddressExplorer(DatasetManifest manifest, IReadOnlyList<AddressSummary> addresses)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(addresses);
        Manifest = manifest;
        all = Array.AsReadOnly(addresses.ToArray());
        LatestDatasetMonth = manifest.LatestMonth;
        Rebuild();
    }

    public DatasetManifest Manifest { get; }
    public YearMonth LatestDatasetMonth { get; }
    public int TotalAddressCount => all.Count;
    public AddressFilters Filters { get; private set; } = AddressFilters.Default;
    public IReadOnlyList<AddressSummary> Addresses { get; private set; } = [];
    public AddressSummary? Selected { get; private set; }
    public string? WindowStart => AddressSemantics.WindowStart(LatestDatasetMonth, Filters.RecencyMonths);

    public void Filter(AddressFilters filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentException.ThrowIfNullOrWhiteSpace(filters.Town);
        ArgumentException.ThrowIfNullOrWhiteSpace(filters.FlatType);
        ArgumentOutOfRangeException.ThrowIfNegative(filters.MinimumPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(filters.MaximumPrice);
        // Independently edited bounds may cross; that is an empty result, not a repaired filter.
        if (filters.RecencyMonths is not (0 or 12 or 24)) throw new ArgumentOutOfRangeException(nameof(filters));
        Filters = filters;
        Rebuild();
    }

    public void Reset() => Filter(AddressFilters.Default);

    public decimal EffectiveMedianPrice(AddressSummary address) => AddressSemantics.EffectiveMedianPrice(address, Filters.FlatType);

    private void Rebuild()
    {
        var selectedKey = Selected?.AddressKey;
        var start = WindowStart;
        var type = Filters.FlatType;
        Addresses = Array.AsReadOnly(all
            .Where(a => AddressSemantics.Matches(a, Filters.Town, type, Filters.MinimumPrice, Filters.MaximumPrice, start))
            .OrderBy(a => AddressSemantics.EffectiveMedianPrice(a, type))
            .ToArray());
        // The selection survives a filter change while the address still matches, and clears otherwise.
        Selected = selectedKey is null ? null : Addresses.FirstOrDefault(a => a.AddressKey == selectedKey);
    }

    public void Select(string addressKey) =>
        Selected = Addresses.FirstOrDefault(a => a.AddressKey == addressKey);

    public int IndexOf(string addressKey)
    {
        for (var i = 0; i < Addresses.Count; i++) if (Addresses[i].AddressKey == addressKey) return i;
        return -1;
    }
}
