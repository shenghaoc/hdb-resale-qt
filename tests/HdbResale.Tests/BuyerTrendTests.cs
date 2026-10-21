using HdbResale.App;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class BuyerTrendTests
{
    private static YearMonth Month(string value) { YearMonth.TryParse(value, out var month); return month!; }
    internal static AddressDetail Recorded(string key)
    {
        using var client = WorkerApiClientTests.Client(new WorkerApiClientTests.RecordedApi());
        return client.GetAddressDetailAsync(key).GetAwaiter().GetResult()!;
    }
    private static AddressDetail WithTrend(params (string Month, decimal Median, int Count)[] points) =>
        Recorded("bedok-10d-bedok-sth-ave-2") with
        { MonthlyTrend = points.Select(p => new AddressTrendPoint(p.Month, p.Median, p.Count, 5000)).ToArray() };

    [Fact]
    public void TheWindowIsTheLatest24SourceMonthsAndMonthsWithoutASaleStayEmpty()
    {
        var trend = BuyerTrend.Build(WithTrend(("2024-10", 400000, 9), ("2024-11", 500000, 2), ("2026-01", 600000, 1),
            ("2026-10", 700000, 3)), Month("2026-10"));
        Assert.Equal("2024-11", trend.Start); Assert.Equal("2026-10", trend.End);
        Assert.Equal(24, trend.Points.Count); Assert.Equal(3, trend.ObservedMonths); Assert.Equal(6, trend.Sales);
        Assert.Equal(21, trend.Points.Count(p => p.MedianPrice is null));
        Assert.Equal(500, trend.Points[0].PriceThousands); Assert.Equal(2, trend.Points[0].Count);
        Assert.Equal(700000, trend.Points[^1].MedianPrice);
        Assert.Equal(450, trend.MinimumY); Assert.Equal(750, trend.MaximumY);
    }

    [Fact]
    public void ARecordedAddressShowsTheApisMonthlyMedians()
    {
        var trend = BuyerTrend.Build(Recorded("bedok-10d-bedok-sth-ave-2"), Month("2026-10"));
        var september = Assert.Single(trend.Points, p => p.Month == "2026-09");
        Assert.Equal(1168888, september.MedianPrice); Assert.Equal(1, september.Count);
        Assert.Null(trend.Points[^1].MedianPrice); // no registration in 2026-10
        Assert.True(trend.ObservedMonths > 0);
    }

    [Fact]
    public void NoSaleInTheWindowIsAnExplicitEmptySeriesWithFiniteAxisBounds()
    {
        var trend = BuyerTrend.Build(Recorded("ang-mo-kio-727-ang-mo-kio-ave-6"), Month("2026-10"));
        Assert.Equal(24, trend.Points.Count); Assert.Equal(0, trend.Sales); Assert.Equal(0, trend.ObservedMonths);
        Assert.All(trend.Points, p => Assert.Null(p.MedianPrice)); Assert.True(trend.MaximumY > trend.MinimumY);
    }

    [Fact]
    public void TheSummaryNamesTheHighestAndLowestMonthsOfTheRecordedChart()
    {
        // 748B's recorded medians in 2024-11..2026-10: ten months, highest 1,060,000 (2026-05), lowest 653,000 (2026-06).
        var trend = BuyerTrend.Build(Recorded("bedok-748b-bedok-reservoir-cres"), Month("2026-10"));
        Assert.Equal("Highest S$1,060,000 in 2026-05, lowest S$653,000 in 2026-06, 10 months with sales of the 24 shown.",
            trend.Summary);
    }

    [Fact]
    public void TheSummaryNamesEveryMonthThatSharesAnExtreme()
    {
        var trend = BuyerTrend.Build(WithTrend(("2025-01", 500000, 1), ("2025-06", 500000, 2), ("2025-08", 450000, 1),
            ("2026-01", 400000, 2), ("2026-03", 400000, 1)), Month("2026-10"));
        Assert.Equal("Highest S$500,000 in 2025-01 and 2025-06, lowest S$400,000 in 2026-01 and 2026-03, 5 months with sales of the 24 shown.",
            trend.Summary);
        var level = BuyerTrend.Build(WithTrend(("2025-01", 500000, 1), ("2025-06", 500000, 2), ("2026-01", 500000, 1)), Month("2026-10"));
        Assert.Equal("S$500,000 in 2025-01, 2025-06 and 2026-01, the only 3 months with sales of the 24 shown.", level.Summary);
    }

    [Fact]
    public void TheSummaryOfOneMonthOrNoneIsPlain()
    {
        Assert.Equal("S$600,000 in 2026-01, the only month with sales of the 24 shown.",
            BuyerTrend.Build(WithTrend(("2026-01", 600000, 1)), Month("2026-10")).Summary);
        Assert.Equal("", BuyerTrend.Build(Recorded("ang-mo-kio-727-ang-mo-kio-ave-6"), Month("2026-10")).Summary);
        Assert.Equal("", BuyerTrendData.Empty.Summary);
        // The earliest data has fewer than 24 months to show.
        Assert.Equal("S$300,000 in 0001-01, the only month with sales of the 1 shown.",
            BuyerTrend.Build(WithTrend(("0001-01", 300000, 1)), Month("0001-01")).Summary);
    }

    [Fact]
    public void NoDetailIsTheEmptySeriesAndTheEarliestYearStillWorks()
    {
        Assert.Same(BuyerTrendData.Empty, BuyerTrend.Build(null, Month("2026-10")));
        var first = BuyerTrend.Build(WithTrend(("0001-01", 300000, 1)), Month("0001-01"));
        Assert.Equal("0001-01", first.Start); Assert.Equal("0001-01", first.End);
        Assert.Equal(300000, Assert.Single(first.Points).MedianPrice); Assert.True(first.MaximumY > first.MinimumY);
    }
}
