using System.Globalization;
using System.Text.Json;
using HdbResale.Domain;
namespace HdbResale.App;

// Formatting and serialization of C#-owned buyer state. QML never derives prices,
// cohort membership, lease figures or recent-row ordering.
internal static class BuyerPresentation
{
    private static string Number(decimal? value, int decimals = 0) => value?.ToString("N" + decimals, CultureInfo.InvariantCulture) ?? "unavailable";
    internal static string Money(decimal? value) => value is null ? "unavailable" : "S$" + Number(value, value == decimal.Truncate(value.Value) ? 0 : 2);
    internal static string Range(decimal? minimum, decimal? maximum, string suffix) => minimum is null ? "unavailable" :
        minimum == maximum ? Number(minimum, 1) + suffix : Number(minimum, 1) + "–" + Number(maximum, 1) + suffix;
    internal static string Metrics(ExplorerState state) => state.SelectedAddress is { } b
        ? $"{b.Latest.Town} · {string.Join(", ", b.FlatTypes)}\n{b.Count:N0} matching transactions · latest {b.Latest.Facts.Month}\nMedian {Money(b.MedianPrice)} · {Money(b.MedianPricePerSqm)}/m² ({b.PricePerSqmCount} of {b.Count} sales)\nPrice range {Money(b.MinimumPrice)}–{Money(b.MaximumPrice)}\nFloor area {Range(b.MinimumAreaSqm, b.MaximumAreaSqm, " m²")}\nLease commencement year{(b.LeaseCommenceYears.Count == 1 ? "" : "s")}: {(b.LeaseCommenceYears.Count == 0 ? "unavailable" : string.Join(", ", b.LeaseCommenceYears))}"
        : "The list and map summarize the same matching sales. Select an address to inspect its recent registrations.";
    internal static string Lease(ExplorerState state)
    {
        if (state.SelectedAddress is not { } b || state.LatestDatasetMonth is not { } reference) return "";
        var estimate = b.EstimateLeaseMonths(reference);
        if (estimate is null) return "Derived remaining lease unavailable; see source lease text in each transaction.";
        var minimum = estimate.IncludesWholeYearObservations ? Years(estimate.MinimumMonths) : Months(estimate.MinimumMonths);
        var maximum = estimate.IncludesWholeYearObservations ? Years(estimate.MaximumMonths) : Months(estimate.MaximumMonths);
        var precision = estimate.IncludesWholeYearObservations
            ? " Display rounded to whole years because source precision is whole years; exact expiry and rounding convention are unknown." : "";
        return $"Derived remaining lease at {reference}: approximately {minimum}{(minimum == maximum ? "" : "–" + maximum)}.\n{b.LeaseEstimateCount} of {b.Count} matching source lease observations, minus elapsed calendar months; rounded source facts may disagree.{precision} Not an eligibility assessment.";
    }
    private static string Years(int months) => months < 0 ? "expired / below zero" : $"{Math.Round(months / 12d, MidpointRounding.AwayFromZero):0} years";
    private static string Months(int months) => months < 0 ? "expired / below zero" : $"{months / 12}y {months % 12}m";
    internal static string RecentJson(ExplorerState state) => JsonSerializer.Serialize(state.RecentTransactions.Select(t => new {
        id=t.Id, heading=$"{t.Facts.Month} · {t.FlatType} · {Money(t.Price)}",
        details=$"{Number(t.Facts.FloorAreaSqm, 1)} m² · {Money(t.PricePerSqm)}/m² · storey {Empty(t.Facts.StoreyRange)}\n{Empty(t.Facts.FlatModel)} · lease start {Empty(t.Facts.LeaseCommenceDateSource)}\nSource remaining lease at resale application: {SourceLease(t.Facts)}" +
            (t.Provenance is not { } p ? "" : $"\nSource {p.SourceIdentity}, row {p.SourceRow}; SHA-256 {p.RawSha256}")
    }));
    private static string SourceLease(TransactionFacts facts) => facts.RemainingLeaseSource is { Length: >= 1 and <= 3 } raw &&
        raw.All(char.IsAsciiDigit) && facts.RemainingLeaseMonths.HasValue ? raw + " years (reported in whole years)" : Empty(facts.RemainingLeaseSource);
    private static string Empty(string? value) => string.IsNullOrEmpty(value) ? "unavailable" : value;
    internal static object? Summary(BlockSummary? b) => b is null ? null : new {
        key=b.Key, address=b.Latest.Address, town=b.Latest.Town, count=b.Count, latest=b.Latest.Facts.Month.ToString(),
        minimumPrice=b.MinimumPrice, maximumPrice=b.MaximumPrice, medianPrice=b.MedianPrice,
        medianPricePerSqm=b.MedianPricePerSqm, minimumArea=b.MinimumAreaSqm, maximumArea=b.MaximumAreaSqm,
        types=b.FlatTypes, leaseYears=b.LeaseCommenceYears,
        recent=b.RecentTransactions.Select(t => new {id=t.Id, month=t.Facts.Month.ToString(), type=t.FlatType,
            storey=t.Facts.StoreyRange, area=t.Facts.FloorAreaSqm, price=t.Price, pricePerSqm=t.PricePerSqm,
            model=t.Facts.FlatModel, lease=t.Facts.RemainingLeaseSource, leaseYear=t.Facts.LeaseCommenceYear})
    };
    internal static string StateJson(ExplorerState state) => JsonSerializer.Serialize(new {
        rows=state.Visible.Count, addresses=state.Addresses.Count, mapped=state.MappedAddresses.Count,
        selected=Summary(state.SelectedAddress), latest=state.LatestDatasetMonth?.ToString()
    });
}
