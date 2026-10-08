using System.Globalization;
using System.Text.Json;
using HdbResale.Domain;
namespace HdbResale.App;

// Formatting of the API's figures for the selected address. QML never derives prices, membership, lease
// figures or row order; the API computes the statistics and AddressSemantics applies the selected flat type.
internal static class BuyerPresentation
{
    private static string Number(decimal? value, int decimals = 0) => value?.ToString("N" + decimals, CultureInfo.InvariantCulture) ?? "unavailable";
    internal static string Money(decimal? value) => value is null ? "unavailable" : "S$" + Number(value, value == decimal.Truncate(value.Value) ? 0 : 2);
    internal static string Range(decimal? minimum, decimal? maximum, string suffix) => minimum is null ? "unavailable" :
        minimum == maximum ? Number(minimum, 1) + suffix : Number(minimum, 1) + "–" + Number(maximum, 1) + suffix;

    // The API's statistics cover the dataset's latest 24 source months, or all recorded sales when there were none.
    internal static string Scope(string latestMonth, YearMonth latestDatasetMonth) =>
        string.CompareOrdinal(latestMonth, AddressSemantics.WindowStart(latestDatasetMonth, 24)) >= 0
            ? $"in the 24 source months to {latestDatasetMonth}"
            : "in all recorded months (none in the latest 24)";

    internal static string Metrics(AddressSummary address, string flatType, AddressDetail? detail, YearMonth latestDatasetMonth)
    {
        var cohort = AddressSemantics.Cohort(address, flatType);
        var median = AddressSemantics.EffectiveMedianPrice(address, flatType);
        var perSqm = AddressSemantics.EffectivePricePerSqm(address, flatType);
        var figures = cohort.IsTypeSpecific ? AddressSemantics.CanonicalFlatType(flatType) + " sales" : "Sales of all flat types";
        var lines = new List<string>
        {
            $"{address.Town} · {string.Join(", ", address.FlatTypes)}",
            $"{figures}: {cohort.TransactionCount:N0} {Scope(cohort.LatestMonth, latestDatasetMonth)} · latest {cohort.LatestMonth}",
            $"Median {Money(median)} · {Money(perSqm)}/m²",
            $"Floor area {Range(cohort.FloorAreaRange[0], cohort.FloorAreaRange[1], " m²")}",
        };
        if (detail is { Summary.PriceIqr: [var lower, var upper] })
            lines.Add($"Middle half of all sales {Money(lower)}–{Money(upper)}");
        if (address.NearestMrt is { } mrt)
            lines.Add($"Nearest MRT: {mrt.StationName} · {Number(mrt.DistanceMeters)} m, about {Math.Round(mrt.WalkingTimeSeconds / 60m):0} min walk");
        return string.Join("\n", lines);
    }

    // The web app's estimate: what remains of a 99-year lease in the current calendar year.
    internal static string Lease(AddressSummary address, int currentYear)
    {
        var range = address.LeaseCommenceRange;
        var (minimum, maximum) = AddressSemantics.RemainingLeaseYears(range, currentYear);
        var commenced = range[0] == range[1] ? $"{range[0]}" : $"{range[0]}–{range[1]}";
        var remaining = minimum == maximum ? $"{maximum} years" : $"{minimum}–{maximum} years";
        return $"Lease commenced {commenced}: about {remaining} of a 99-year lease remain in {currentYear}. " +
            "Each registration below shows the remaining lease recorded at its resale application. Not an eligibility assessment.";
    }

    internal static string Location(AddressSummary address)
    {
        var point = string.Create(CultureInfo.InvariantCulture, $"{address.Coordinates.Lat:F5}, {address.Coordinates.Lng:F5}");
        var postal = string.IsNullOrWhiteSpace(address.PostalCode) ? "" : $" · postal code {address.PostalCode}";
        return $"Approximate block location {point}{postal}. Locations are block points, never individual flats.";
    }

    internal static string RecentJson(AddressDetail? detail) => JsonSerializer.Serialize((detail?.RecentTransactions ?? []).Select(t => new {
        id=t.Id, heading=$"{t.Month} · {t.FlatType} · {Money(t.ResalePrice)}",
        details=$"{Number(t.FloorAreaSqm, 1)} m² · {Money(t.PricePerSqm)}/m² · storey {Empty(t.StoreyRange)}\n{Empty(t.FlatModel)} · lease start {t.LeaseCommenceDate}\nSource remaining lease at resale application: {Empty(t.RemainingLease)}"
    }));
    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
}
