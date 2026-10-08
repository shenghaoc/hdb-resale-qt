using HdbResale.App;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

// The inspector's facts come from the API's figures, applied to the selected flat type exactly as the list does.
public sealed class BuyerPresentationTests
{
    private static YearMonth Month(string value) { YearMonth.TryParse(value, out var month); return month!; }
    private static AddressSummary Recorded(string key)
    {
        using var client = WorkerApiClientTests.Client(new WorkerApiClientTests.RecordedApi());
        return client.GetAddressesAsync().GetAwaiter().GetResult().Single(a => a.AddressKey == key);
    }
    private static IReadOnlyList<BuyerPresentation.InspectorSection> Sections(string flatType = AddressFilters.AllFlatTypes,
        AddressDetail? detail = null, AddressSummary? address = null) =>
        BuyerPresentation.Inspector(address ?? Recorded("bedok-748b-bedok-reservoir-cres"), flatType, detail, Month("2026-09"), 2026);
    private static string Value(IReadOnlyList<BuyerPresentation.InspectorSection> sections, string title, string label) =>
        sections.Single(s => s.Title == title).Facts.Single(f => f.Label == label).Value;

    [Fact]
    public void TheWholeAddressFiguresFillTheSections()
    {
        var sections = Sections();
        Assert.Equal(["Sales", "Address", "Lease", "Location"], sections.Select(s => s.Title));
        Assert.Equal("Sales of all flat types in the 24 source months to 2026-09.", sections[0].Note);
        Assert.Equal("10", Value(sections, "Sales", "Registrations"));
        Assert.Equal("2026-09", Value(sections, "Sales", "Latest"));
        Assert.Equal("S$837,500", Value(sections, "Sales", "Median price"));
        Assert.Equal("S$9,832.82/m²", Value(sections, "Sales", "Median per m²"));
        Assert.Equal("67.0–105.0 m²", Value(sections, "Sales", "Floor area"));
        Assert.DoesNotContain(sections[0].Facts, f => f.Label == "Middle half, all types");
        Assert.Equal("BEDOK", Value(sections, "Address", "Town"));
        Assert.Equal("3 ROOM, 4 ROOM, 5 ROOM", Value(sections, "Address", "Flat types"));
        Assert.Equal("DBSS", Value(sections, "Address", "Models"));
        Assert.Equal("472748", Value(sections, "Address", "Postal code"));
        Assert.Equal("BEDOK NORTH MRT STATION · 317 m, about 4 min walk", Value(sections, "Address", "Nearest MRT"));
        Assert.Equal("2014", Value(sections, "Lease", "Commenced"));
        Assert.Equal("about 87 years of a 99-year lease", Value(sections, "Lease", "Remaining in 2026"));
        Assert.Single(sections[3].Facts);
        Assert.Matches(@"^1\.\d{5}, 103\.\d{5}$", Value(sections, "Location", "Block point"));
    }

    [Fact]
    public void ASelectedFlatTypeUsesItsOwnCohortAndMedians()
    {
        var sections = Sections("4 ROOM");
        Assert.Equal("4 ROOM sales in the 24 source months to 2026-09.", sections[0].Note);
        Assert.Equal("6", Value(sections, "Sales", "Registrations"));
        Assert.Equal("2026-02", Value(sections, "Sales", "Latest"));
        Assert.Equal("S$850,000", Value(sections, "Sales", "Median price"));
        Assert.Equal("87.0 m²", Value(sections, "Sales", "Floor area"));
    }

    [Fact]
    public void TheDetailsAddTheMiddleHalfOfSales()
    {
        var sections = Sections(detail: BuyerTrendTests.Recorded("bedok-748b-bedok-reservoir-cres"));
        Assert.Equal("S$705,750–S$880,000", Value(sections, "Sales", "Middle half, all types"));
        using var json = System.Text.Json.JsonDocument.Parse(BuyerPresentation.InspectorJson(sections));
        var sales = json.RootElement[0];
        Assert.Equal("Sales", sales.GetProperty("title").GetString());
        var fact = sales.GetProperty("facts").EnumerateArray().Last();
        Assert.Equal("Middle half, all types", fact.GetProperty("label").GetString());
        Assert.Equal("S$705,750–S$880,000", fact.GetProperty("value").GetString());
    }

    [Fact]
    public void OptionalAddressFactsAreOmittedRatherThanBlank()
    {
        var address = Recorded("bedok-748b-bedok-reservoir-cres") with
            { NearestMrt = null, PostalCode = " ", FlatModels = [], LeaseCommenceRange = [1972, 1975] };
        var sections = Sections(address: address);
        Assert.Equal(["Town", "Flat types"], sections[1].Facts.Select(f => f.Label));
        Assert.Equal("1972–1975", Value(sections, "Lease", "Commenced"));
        Assert.Equal("about 45–48 years of a 99-year lease", Value(sections, "Lease", "Remaining in 2026"));
    }

    [Fact]
    public void AnAddressWithoutSalesInTheWindowSaysSo()
    {
        var sections = Sections(address: Recorded("bedok-748b-bedok-reservoir-cres") with { LatestMonth = "2023-01" });
        Assert.Equal("Sales of all flat types in all recorded months (none in the latest 24).", sections[0].Note);
    }
}
