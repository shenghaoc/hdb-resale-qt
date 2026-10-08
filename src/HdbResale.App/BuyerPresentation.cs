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

    // The inspector's structured facts. Each section is a titled group of label/value pairs with an optional note;
    // QML lays them out and never derives a figure. Sections that have nothing to say are omitted, never empty.
    internal sealed record Fact(string Label, string Value);
    internal sealed record InspectorSection(string Title, string Note, IReadOnlyList<Fact> Facts);

    internal static IReadOnlyList<InspectorSection> Inspector(AddressSummary address, string flatType, AddressDetail? detail,
        YearMonth latestDatasetMonth, int currentYear)
    {
        var cohort = AddressSemantics.Cohort(address, flatType);
        var figures = cohort.IsTypeSpecific ? AddressSemantics.CanonicalFlatType(flatType) + " sales" : "Sales of all flat types";
        var sales = new List<Fact>
        {
            new("Registrations", $"{cohort.TransactionCount:N0}"),
            new("Latest", cohort.LatestMonth),
            new("Median price", Money(AddressSemantics.EffectiveMedianPrice(address, flatType))),
            new("Median per m²", Money(AddressSemantics.EffectivePricePerSqm(address, flatType)) + "/m²"),
            new("Floor area", Range(cohort.FloorAreaRange[0], cohort.FloorAreaRange[1], " m²")),
        };
        // The details' interquartile range covers every flat type at the address, whatever type is selected; the
        // chart and the registrations below it do too. Only the facts above follow the selected type.
        if (detail is { Summary.PriceIqr: [var lower, var upper] })
            sales.Add(new("Middle half, all types", $"{Money(lower)}–{Money(upper)}"));

        var about = new List<Fact> { new("Town", address.Town), new("Flat types", string.Join(", ", address.FlatTypes)) };
        if (cohort.FlatModels.Count > 0) about.Add(new("Models", string.Join(", ", cohort.FlatModels)));
        if (!string.IsNullOrWhiteSpace(address.PostalCode)) about.Add(new("Postal code", address.PostalCode));
        if (address.NearestMrt is { } mrt)
            about.Add(new("Nearest MRT", $"{mrt.StationName} · {Number(mrt.DistanceMeters)} m, about {Math.Round(mrt.WalkingTimeSeconds / 60m):0} min walk"));

        var range = address.LeaseCommenceRange;
        var (minimum, maximum) = AddressSemantics.RemainingLeaseYears(range, currentYear);
        var point = string.Create(CultureInfo.InvariantCulture, $"{address.Coordinates.Lat:F5}, {address.Coordinates.Lng:F5}");
        return
        [
            new("Sales", $"{figures} {Scope(cohort.LatestMonth, latestDatasetMonth)}.", sales),
            new("Address", "", about),
            new("Lease", "Each registration below shows the remaining lease recorded at its resale application. Not an eligibility assessment.",
            [
                new("Commenced", range[0] == range[1] ? $"{range[0]}" : $"{range[0]}–{range[1]}"),
                new($"Remaining in {currentYear}", "about " + (minimum == maximum ? $"{maximum} years" : $"{minimum}–{maximum} years") + " of a 99-year lease"),
            ]),
            new("Location", "Locations are approximate block points, never individual flats.", [new("Block point", point)]),
        ];
    }
    internal static string InspectorJson(IReadOnlyList<InspectorSection> sections) => JsonSerializer.Serialize(sections.Select(s => new {
        title = s.Title, note = s.Note, facts = s.Facts.Select(f => new { label = f.Label, value = f.Value }) }));

    internal static string RecentJson(AddressDetail? detail) => JsonSerializer.Serialize((detail?.RecentTransactions ?? []).Select(t => new {
        id=t.Id, heading=$"{t.Month} · {t.FlatType} · {Money(t.ResalePrice)}",
        details=$"{Number(t.FloorAreaSqm, 1)} m² · {Money(t.PricePerSqm)}/m² · storey {Empty(t.StoreyRange)}\n{Empty(t.FlatModel)} · lease start {t.LeaseCommenceDate}\nSource remaining lease at resale application: {Empty(t.RemainingLease)}"
    }));
    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "unavailable" : value;
}
