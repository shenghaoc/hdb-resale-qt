using System.Text.Json;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

public sealed class BuyerDomainTests
{
    private static YearMonth Month(string value)
    {
        Assert.True(YearMonth.TryParse(value, out var month));
        return month!;
    }
    private static ResaleTransaction Row(string id, decimal price = 400_000, string month = "2025-06",
        string block = "1", string town = "T", string flatType = "3 ROOM", decimal? area = 80,
        int? commenceYear = 1980, string? storey = "01 TO 03", string? model = "Model A",
        string? lease = "60 years", int? leaseMonths = 720, bool mapped = true) => new(id,
            new(Month(month), town, block, "ST", flatType, price)
            {
                StoreyRange = storey, FloorAreaSqm = area, FlatModel = model,
                LeaseCommenceYear = commenceYear, RemainingLeaseSource = lease, RemainingLeaseMonths = leaseMonths
            }, mapped ? new(new(1.3, 103.8), CoordinateQuality.BlockApproximation, "evidence")
                : new(null, CoordinateQuality.Missing, "missing evidence"),
            new(mapped ? MatchQuality.ExactAddress : MatchQuality.Unmatched, "test evidence", [], [], []));

    [Fact]
    public void CompleteSummariesIncludeUnlocatedRowsAndMapReusesTheExactSameSummaries()
    {
        var a = Row("a", 200_000);
        var b = Row("b", 400_000, flatType: "4 ROOM", area: 100);
        var unlocated = Row("c", 300_000, block: "2", mapped: false);
        var state = new ExplorerState([a, b, unlocated]);
        Assert.Equal(3, state.Visible.Count);
        Assert.Equal(2, state.Addresses.Count);
        Assert.Equal(3, state.Addresses.Sum(s => s.Count));
        var mapped = Assert.Single(state.MappedAddresses);
        Assert.Same(state.Addresses.Single(s => s.Key == BlockSummaries.Key(a)), mapped);
        Assert.Equal(2, mapped.Count);
        Assert.Equal(new[] { "3 ROOM", "4 ROOM" }, mapped.FlatTypes);
        Assert.Equal(200_000, mapped.MinimumPrice);
        Assert.Equal(400_000, mapped.MaximumPrice);
        Assert.Equal(300_000, mapped.MedianPrice);
        Assert.Equal(80, mapped.MinimumAreaSqm);
        Assert.Equal(100, mapped.MaximumAreaSqm);
        Assert.Equal(3250, mapped.MedianPricePerSqm); // median(200000/80,400000/100), not median(price)/median(area)
        Assert.Equal(new[] { MatchQuality.ExactAddress }, mapped.MatchQualities);
        Assert.Equal(new[] { CoordinateQuality.BlockApproximation }, mapped.CoordinateQualities);
        state.SelectAddress(BlockSummaries.Key(unlocated));
        Assert.Equal(unlocated, state.Selected);
        Assert.False(state.SelectedAddress!.IsMapped);
    }

    [Fact]
    public void MixedMissingAmbiguousOrConflictingCoordinatesStayInTableWithoutInventingMapTruth()
    {
        var located = Row("located");
        var missing = Row("missing", mapped: false);
        var ambiguous = missing with { Id = "ambiguous", Match = new(MatchQuality.Ambiguous, "conflict", [], [], []) };
        var differentPoint = located with { Id = "other", Location = new(new(1.4, 103.9), CoordinateQuality.BlockApproximation, "other") };
        foreach (var rows in new[] { new[] { located, missing, ambiguous }, new[] { located, differentPoint } })
        {
            var state = new ExplorerState(rows);
            var summary = Assert.Single(state.Addresses);
            Assert.Equal(rows.Length, summary.Count);
            Assert.False(summary.IsMapped);
            Assert.Empty(state.MappedAddresses);
            Assert.Equal(rows.Select(t => t.Match.Quality).Distinct().Order(), summary.MatchQualities);
            Assert.Equal(rows.Select(t => t.Location.Quality).Distinct().Order(), summary.CoordinateQualities);
        }
        // The historical projection intentionally keeps its located-row contract.
        Assert.Equal(1, Assert.Single(BlockSummaries.Located([located, missing, ambiguous])).Count);
        Assert.Throws<InvalidDataException>(() => BlockSummaries.Located([located, differentPoint]));
    }

    [Fact]
    public void MediansAreDecimalEvenOddAndExcludeInvalidAreaOnlyFromAreaDependentStatistics()
    {
        var rows = new[] { Row("a", 1.01m, area: 0.1m), Row("b", 3.03m, area: 0.1m),
            Row("c", 7.07m, area: null), Row("d", 9.09m, area: 0), Row("e", 11.11m, area: -1) };
        var odd = Assert.Single(BlockSummaries.All(rows));
        Assert.Equal(7.07m, odd.MedianPrice);
        Assert.Equal(20.2m, odd.MedianPricePerSqm);
        Assert.Equal(2, odd.PricePerSqmCount);
        Assert.Equal(.1m, odd.MinimumAreaSqm);
        Assert.Equal(.1m, odd.MaximumAreaSqm);
        Assert.Equal(5, odd.Count);
        Assert.Equal(5.05m, Assert.Single(BlockSummaries.All(rows.Take(4).ToArray())).MedianPrice);
        Assert.All(rows.Skip(2), t => Assert.Null(t.PricePerSqm));
        var missing = Assert.Single(BlockSummaries.All(rows.Skip(2).ToArray()));
        Assert.Null(missing.MinimumAreaSqm); Assert.Null(missing.MaximumAreaSqm); Assert.Null(missing.MedianPricePerSqm);
        Assert.Equal(decimal.MaxValue, Assert.Single(BlockSummaries.All([
            Row("max-a", decimal.MaxValue, area: 1), Row("max-b", decimal.MaxValue, area: 1)])).MedianPrice);
        Assert.Null(Row("ratio-overflow", decimal.MaxValue, area: .1m).PricePerSqm);
        Assert.Equal(0.03m, Assert.Single(BlockSummaries.All([
            Row("small-a", .02m, area: 1), Row("small-b", .04m, area: 1)])).MedianPricePerSqm);
        Assert.Equal(.0000000000000000000000000002m, Assert.Single(BlockSummaries.All([
            Row("tiny-a", .0000000000000000000000000001m, area: 1),
            Row("tiny-b", .0000000000000000000000000002m, area: 1)])).MedianPrice);
    }

    [Fact]
    public void RecentFifteenAreDeterministicAcrossInputOrderAndEveryTieBreak()
    {
        var same = Row("id-b");
        var expected = new[]
        {
            same with { Id = "newer", Facts = same.Facts with { Month = Month("2025-07") } },
            same with { Id = "type", Facts = same.Facts with { FlatType = "2 ROOM" } },
            same with { Id = "storey", Facts = same.Facts with { StoreyRange = "00 TO 00" } },
            same with { Id = "area", Facts = same.Facts with { FloorAreaSqm = 70 } },
            same with { Id = "price", Facts = same.Facts with { Price = 390_000 } },
            same with { Id = "model", Facts = same.Facts with { FlatModel = "Improved" } },
            same with { Id = "lease", Facts = same.Facts with { RemainingLeaseSource = "59 years" } },
            same with { Id = "id-a" }, same
        }.Concat(Enumerable.Range(0, 20).Select(i => Row($"older-{i:D2}", month: "2024-12"))).ToArray();
        var first = Assert.Single(BlockSummaries.All(expected.Reverse().ToArray()));
        var second = Assert.Single(BlockSummaries.All(expected.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray()));
        Assert.Equal(expected.Take(15).Select(t => t.Id), first.RecentTransactions.Select(t => t.Id));
        Assert.Equal(first.RecentTransactions, second.RecentTransactions);
        Assert.Equal(29, first.Count);
        Assert.Equal("newer", first.Latest.Id);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void LeaseEstimatesUseAllSourceObservationsAtReferenceMonthAndNeverCommencementFallback()
    {
        var older = Row("old", month: "2020-01", commenceYear: 1977, lease: "80 years", leaseMonths: 960);
        var newer = Row("new", month: "2021-03", commenceYear: 1992, lease: "78 years 10 months", leaseMonths: 946);
        var reference = Month("2025-06");
        var summary = Assert.Single(BlockSummaries.All([older, newer]));
        Assert.Equal(new[] { 1977, 1992 }, summary.LeaseCommenceYears);
        Assert.Equal(2, summary.LeaseEstimateCount);
        Assert.Equal(new LeaseEstimate(reference, 895, 895) { IncludesWholeYearObservations = true }, summary.EstimateLeaseMonths(reference));
        Assert.Equal("80 years", older.Facts.RemainingLeaseSource);
        var differing = newer with { Facts = newer.Facts with { RemainingLeaseMonths = 950 } };
        Assert.Equal(new LeaseEstimate(reference, 895, 899) { IncludesWholeYearObservations = true }, Assert.Single(BlockSummaries.All([older, differing])).EstimateLeaseMonths(reference));
        Assert.Null(Assert.Single(BlockSummaries.All([Row("unknown", commenceYear: 1980, lease: "unknown", leaseMonths: null)]))
            .EstimateLeaseMonths(reference));
        Assert.Empty(Assert.Single(BlockSummaries.All([Row("missing-year", commenceYear: null)])).LeaseCommenceYears);
        // A 16th older row is excluded from recent display, but still participates in the source-lease range.
        var all = Enumerable.Range(0, 15).Select(i => newer with { Id = $"recent-{i}" }).Append(
            older with { Facts = older.Facts with { RemainingLeaseMonths = 900 } }).ToArray();
        Assert.Equal(new LeaseEstimate(reference, 835, 895) { IncludesWholeYearObservations = true }, Assert.Single(BlockSummaries.All(all)).EstimateLeaseMonths(reference));
        var expired = Row("expired", month: "2020-01", lease: "1 year", leaseMonths: 12);
        Assert.Equal(-53, Assert.Single(BlockSummaries.All([expired])).EstimateLeaseMonths(reference)!.MinimumMonths);
    }

    [Fact]
    public void AddressSelectionSurvivesHiddenTransactionAndClearsOnlyWhenAddressDisappears()
    {
        var old = Row("old", 300_000, month: "2024-07", flatType: "3 ROOM");
        var newest = Row("new", 600_000, month: "2025-06", flatType: "4 ROOM");
        var other = Row("other", 200_000, block: "2");
        var state = new ExplorerState([old, newest, other]);
        state.Select(newest.Id);
        state.Filter("T", "3 ROOM", 0, 500_000, 12);
        Assert.Equal(BlockSummaries.Key(old), state.SelectedAddress!.Key);
        Assert.Equal(old, state.Selected);
        Assert.Equal(new[] { old }, state.RecentTransactions);
        state.Reset();
        Assert.Equal(old, state.Selected); // retain exact selected row while it still matches
        Assert.Equal(2, state.SelectedAddress!.Count);
        state.Filter("T", "4 ROOM", 0, 1_000_000, 0);
        Assert.Equal(newest, state.Selected);
        state.Filter("T", "2 ROOM", 0, 1_000_000, 0);
        Assert.Null(state.Selected); Assert.Null(state.SelectedAddress); Assert.Empty(state.RecentTransactions);
        state.Reset();
        Assert.Null(state.Selected); Assert.Null(state.SelectedAddress); // no implicit selection on reentry
        state.SelectAddress(BlockSummaries.Key(old));
        Assert.Equal(newest, state.Selected);
        state.Select("not-visible"); Assert.Null(state.SelectedAddress);
        state.SelectAddress("missing-address"); Assert.Null(state.Selected);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(12, 2)]
    [InlineData(24, 4)]
    public void RecencyIncludesExactlyTwelveOrTwentyFourCalendarMonthsFromDatasetMaximum(int months, int count)
    {
        var rows = new[] { Row("latest", month: "2025-06"), Row("in12", month: "2024-07"),
            Row("out12", month: "2024-06"), Row("in24", month: "2023-07"),
            Row("out24", month: "2023-06"), Row("old", month: "2017-01") };
        var state = new ExplorerState(rows.Reverse().ToArray());
        state.Filter("T", ExplorerState.AllFlatTypes, 400_000, 400_000, months);
        Assert.Equal(count, state.Visible.Count);
        Assert.Equal("2025-06", state.LatestDatasetMonth!.ToString());
    }

    [Fact]
    public void LatestDatasetMonthIsIndependentOfTownTypePriceAndDefaultPriceCap()
    {
        var rows = new[] { Row("latest", 1_500_000, month: "2025-06", town: "OTHER", flatType: "EXECUTIVE"),
            Row("in", month: "2024-07"), Row("out", month: "2024-06") };
        var state = new ExplorerState(rows);
        Assert.Equal(2, state.Visible.Count);
        state.Filter("T", "3 ROOM", 400_000, 400_000, 12);
        Assert.Equal("in", Assert.Single(state.Visible).Id);
        Assert.Equal("2025-06", state.LatestDatasetMonth!.ToString());
        state.Filter("T", "3 ROOM", 400_001, 999_999, 12);
        Assert.Empty(state.Visible);
        Assert.Equal("2025-06", state.LatestDatasetMonth!.ToString());
    }

    [Fact]
    public void ResetAndLegacyFilterRestoreDefaultsAndInvalidFiltersDoNotPartiallyMutate()
    {
        var state = new ExplorerState([Row("a"), Row("b", 1_100_000)]);
        state.Filter("T", "3 ROOM", 300_000, 1_200_000, 24);
        state.Select("a");
        var before = state.Addresses;
        Assert.Throws<ArgumentException>(() => state.Filter(" ", "3 ROOM", 0, 1_000_000, 0));
        Assert.Throws<ArgumentException>(() => state.Filter("T", " ", 0, 1_000_000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Filter("T", "3 ROOM", -1, 1_000_000, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Filter("T", "3 ROOM", 0, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Filter("T", "3 ROOM", 0, 1_000_000, 6));
        Assert.Same(before, state.Addresses); Assert.Equal("a", state.Selected!.Id);
        state.Filter("T", "3 ROOM", 400_001, 400_000, 0);
        Assert.Empty(state.Visible); Assert.Empty(state.Addresses); Assert.Null(state.Selected);
        Assert.Equal(400_001, state.MinimumPrice); Assert.Equal(400_000, state.MaximumPrice);
        state.Filter("T", "3 ROOM", 400_000, 400_000, 0);
        Assert.Single(state.Visible);
        state.Select("a");
        state.Filter("T", 500_000);
        Assert.Equal(ExplorerState.AllFlatTypes, state.FlatType);
        Assert.Equal(0, state.MinimumPrice); Assert.Equal(0, state.RecencyMonths);
        state.Reset();
        Assert.Equal(ExplorerState.AllTowns, state.Town); Assert.Equal(ExplorerState.AllFlatTypes, state.FlatType);
        Assert.Equal(0, state.MinimumPrice); Assert.Equal(1_000_000, state.MaximumPrice); Assert.Equal(0, state.RecencyMonths);
        Assert.Single(state.Visible); Assert.Equal("a", state.Selected!.Id);
    }

    [Fact]
    public void EmptyDatasetAndLegacyConstructorsRemainValid()
    {
        var state = new ExplorerState([]);
        state.Filter(ExplorerState.AllTowns, ExplorerState.AllFlatTypes, 0, 0, 24);
        Assert.Empty(state.Visible); Assert.Empty(state.Addresses); Assert.Empty(state.MappedAddresses);
        Assert.Null(state.LatestDatasetMonth); Assert.Null(state.SelectedAddress);
        var facts = new TransactionFacts(Month("2024-01"), "T", "1", "ST", "3 ROOM", 100);
        Assert.Null(facts.FloorAreaSqm); Assert.Null(facts.LeaseCommenceYear); Assert.Null(facts.RemainingLeaseSource);
        var summary = new BlockSummary("legacy", Row("legacy"), 1, 100, 100, 100);
        Assert.Empty(summary.FlatTypes); Assert.Null(summary.EstimateLeaseMonths(Month("2024-01")));
    }
}
