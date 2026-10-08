using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

// Filters, order and selection over the recorded API addresses (tests/fixtures/worker-api). Expected keys
// are spelled out from the recorded data, not recomputed with the code under test.
public sealed class AddressExplorerTests
{
    internal static AddressExplorer Recorded()
    {
        using var client = WorkerApiClientTests.Client(new WorkerApiClientTests.RecordedApi());
        return new(client.GetManifestAsync().GetAwaiter().GetResult(), client.GetAddressesAsync().GetAwaiter().GetResult());
    }

    private static string[] Keys(AddressExplorer explorer) => explorer.Addresses.Select(a => a.AddressKey).ToArray();
    private static AddressFilters Filters(string town = AddressFilters.AllTowns, string type = AddressFilters.AllFlatTypes,
        decimal minimum = 0, decimal maximum = 2_000_000, int months = 0) => new(town, type, minimum, maximum, months);

    [Fact]
    public void StartsWithEveryAddressUpToTheDefaultMaximumLowestMedianFirst()
    {
        var explorer = Recorded();
        Assert.Equal(11, explorer.TotalAddressCount);
        Assert.Equal("2026-10", explorer.LatestDatasetMonth.ToString());
        Assert.Equal(AddressFilters.Default, explorer.Filters);
        Assert.Equal(["ang-mo-kio-727-ang-mo-kio-ave-6", "bedok-39-bedok-sth-rd", "bedok-115-bedok-nth-rd",
            "bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres"], Keys(explorer));
        Assert.Null(explorer.Selected);
    }

    [Fact]
    public void ATownKeepsOnlyItsAddresses()
    {
        var explorer = Recorded();
        explorer.Filter(Filters(town: "KALLANG/WHAMPOA"));
        Assert.Equal(["kallang-whampoa-46-bendemeer-rd", "kallang-whampoa-58-jln-ma-mor"], Keys(explorer));
    }

    [Fact]
    public void AFlatTypeBoundsAndOrdersByThatTypesMedian()
    {
        var explorer = Recorded();
        // Ang Mo Kio 588B/588C have address medians above S$1.1M but 4-room medians below it.
        explorer.Filter(Filters(type: "4 room", maximum: 1_100_000));
        Assert.Equal(["bedok-39-bedok-sth-rd", "bedok-115-bedok-nth-rd", "bedok-748a-bedok-reservoir-cres",
            "bedok-748b-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres", "ang-mo-kio-588b-ang-mo-kio-st-52",
            "ang-mo-kio-588c-ang-mo-kio-st-52"], Keys(explorer));
        Assert.Equal(1002888m, explorer.EffectiveMedianPrice(explorer.Addresses[5]));
        explorer.Filter(Filters(type: "4 ROOM", minimum: 850_000, maximum: 1_002_888));
        Assert.Equal(["bedok-748b-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres", "ang-mo-kio-588b-ang-mo-kio-st-52"],
            Keys(explorer));
    }

    [Fact]
    public void AWindowKeepsAddressesWithARegistrationInIt()
    {
        var explorer = Recorded();
        explorer.Filter(Filters(months: 12));
        Assert.Equal("2025-11", explorer.WindowStart);
        Assert.Equal(["bedok-39-bedok-sth-rd", "bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres",
            "bedok-747a-bedok-reservoir-cres", "ang-mo-kio-588c-ang-mo-kio-st-52", "bedok-10d-bedok-sth-ave-2",
            "ang-mo-kio-588b-ang-mo-kio-st-52", "kallang-whampoa-46-bendemeer-rd"], Keys(explorer));
        explorer.Filter(Filters(months: 24));
        Assert.Equal("2024-11", explorer.WindowStart);
        Assert.Equal(10, explorer.Addresses.Count);
        Assert.DoesNotContain("ang-mo-kio-727-ang-mo-kio-ave-6", Keys(explorer));
    }

    [Fact]
    public void AWindowWithAFlatTypeUsesThatTypesLatestRegistration()
    {
        var explorer = Recorded();
        // Bedok 747A sold a flat in 2026-02, but its latest 4-room sale was 2025-09.
        explorer.Filter(Filters(type: "4 ROOM", months: 12));
        Assert.Equal(["bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres", "ang-mo-kio-588b-ang-mo-kio-st-52"],
            Keys(explorer));
    }

    [Fact]
    public void AWindowWithAFlatTypeNeverGuessesFromAnAddressWithoutThatTypesFigures()
    {
        var address = ProductCoreParityTests.Block(flatTypes: ["4 ROOM"]) with { LatestMonth = "2026-09" };
        Assert.True(AddressSemantics.Matches(address, null, "4 ROOM", null, null, startMonth: null));
        Assert.True(AddressSemantics.Matches(address, null, null, null, null, startMonth: "2026-01"));
        Assert.False(AddressSemantics.Matches(address, null, "4 ROOM", null, null, startMonth: "2026-01"));
    }

    [Fact]
    public void EqualMediansKeepTheApiOrder()
    {
        var first = ProductCoreParityTests.Block() with { AddressKey = "first" };
        var second = ProductCoreParityTests.Block() with { AddressKey = "second" };
        var cheaper = ProductCoreParityTests.Block(medianPrice: 400_000) with { AddressKey = "cheaper" };
        var explorer = new AddressExplorer(Recorded().Manifest, [first, second, cheaper]);
        Assert.Equal(["cheaper", "first", "second"], Keys(explorer));
    }

    [Theory]
    [InlineData("2026-10", 12, "2025-11")]
    [InlineData("2026-10", 24, "2024-11")]
    [InlineData("2026-01", 24, "2024-02")]
    [InlineData("2026-12", 12, "2026-01")]
    [InlineData("2026-10", 0, null)]
    public void WindowsEndAtTheDatasetsLatestMonth(string latest, int months, string? start)
    {
        YearMonth.TryParse(latest, out var month);
        Assert.Equal(start, AddressSemantics.WindowStart(month!, months));
    }

    [Fact]
    public void TheSelectionSurvivesOnlyWhileTheAddressStillMatches()
    {
        var explorer = Recorded();
        explorer.Select("ang-mo-kio-727-ang-mo-kio-ave-6");
        Assert.Equal("ang-mo-kio-727-ang-mo-kio-ave-6", explorer.Selected?.AddressKey);
        explorer.Filter(Filters(maximum: 400_000));
        Assert.Equal("ang-mo-kio-727-ang-mo-kio-ave-6", explorer.Selected?.AddressKey);
        Assert.Equal(0, explorer.IndexOf("ang-mo-kio-727-ang-mo-kio-ave-6"));
        explorer.Filter(Filters(town: "BEDOK"));
        Assert.Null(explorer.Selected);
        explorer.Select("bedok-10d-bedok-sth-ave-2");
        Assert.Equal("bedok-10d-bedok-sth-ave-2", explorer.Selected?.AddressKey);
        explorer.Select("not-listed");
        Assert.Null(explorer.Selected);
        Assert.Equal(-1, explorer.IndexOf("not-listed"));
    }

    [Fact]
    public void CrossedBoundsAreAnEmptyResultAndInvalidFiltersAreRefused()
    {
        var explorer = Recorded();
        explorer.Filter(Filters(minimum: 900_000, maximum: 800_000));
        Assert.Empty(explorer.Addresses);
        Assert.Throws<ArgumentOutOfRangeException>(() => explorer.Filter(Filters(months: 6)));
        Assert.Throws<ArgumentOutOfRangeException>(() => explorer.Filter(Filters(minimum: -1)));
        Assert.Throws<ArgumentException>(() => explorer.Filter(Filters(town: " ")));
        explorer.Reset();
        Assert.Equal(6, explorer.Addresses.Count);
    }

    [Fact]
    public void RemainingLeaseFollowsTheWebAppsNinetyNineYearEstimate()
    {
        Assert.Equal((68, 68), AddressSemantics.RemainingLeaseYears([1995, 1995], 2026));
        Assert.Equal((45, 53), AddressSemantics.RemainingLeaseYears([1972, 1980], 2026));
        Assert.Equal("MULTI-GENERATION", AddressSemantics.CanonicalFlatType(" multi generation "));
    }
}
