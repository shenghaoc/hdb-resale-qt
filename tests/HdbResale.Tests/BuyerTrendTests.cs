using HdbResale.App;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class BuyerTrendTests
{
    private static ResaleTransaction Row(string id, string month, decimal price, string town = "T")
    {
        YearMonth.TryParse(month, out var parsed);
        var source=ExplorerStateTests.Fixture().Accepted[0];
        return source with {Id=id,Facts=source.Facts with {Month=parsed!,Town=town,Price=price}};
    }
    [Fact]
    public void MonthlyMediansUseAllMatchingRowsAndPreserveMissingMonths()
    {
        var rows=Enumerable.Range(1,20).Select(i=>Row("R"+i,"2026-01",i*10000)).ToList();
        rows.Add(Row("end","2026-10",300000,"OTHER"));
        var state=new ExplorerState(rows);state.Select("R1");
        var trend=BuyerTrend.Build(state);
        Assert.Equal("2024-11",trend.Start);Assert.Equal("2026-10",trend.End);
        Assert.Equal(24,trend.Points.Count);Assert.Equal(1,trend.ObservedMonths);Assert.Equal(20,trend.Sales);
        var observed=Assert.Single(trend.Points,p=>p.Count>0);
        Assert.Equal("2026-01",observed.Month);Assert.Equal(105000,observed.MedianPrice);Assert.Equal(105,observed.PriceThousands);
        Assert.Equal(23,trend.Points.Count(p=>p.MedianPrice is null));
        state.Filter("T","All flat types",100000,200000,0);
        var filtered=BuyerTrend.Build(state);Assert.Equal(11,filtered.Sales);
        Assert.Equal(150000,Assert.Single(filtered.Points,p=>p.Count>0).MedianPrice);
    }
    [Fact]
    public void FixedWindowDoesNotChangeWithSelectedAddressAndNeverAddsZeroSales()
    {
        var state=new ExplorerState([Row("old","2024-10",300000),Row("start","2024-11",500000),Row("end","2026-10",700000,"OTHER")]);
        state.Select("old");var trend=BuyerTrend.Build(state);
        Assert.Equal(1,trend.Sales);Assert.Equal(500000,trend.Points[0].MedianPrice);
        Assert.True(trend.MaximumY>trend.MinimumY);
        state.Filter("T","All flat types",0,1000000,12);
        Assert.Null(state.SelectedAddress);Assert.Empty(BuyerTrend.Build(state).Points);
        state.Reset();state.Select("end");trend=BuyerTrend.Build(state);
        Assert.Equal("2024-11",trend.Start);Assert.Equal(700000,trend.Points[^1].MedianPrice);
    }
    [Fact]
    public void EarliestAcceptedYearUsesOnlyRepresentableMonthsWithoutSelectionCrash()
    {
        var state=new ExplorerState([Row("first","0001-01",300000)]);state.Select("first");
        var trend=BuyerTrend.Build(state);
        Assert.Equal("0001-01",trend.Start);Assert.Equal("0001-01",trend.End);
        Assert.Equal(300000,Assert.Single(trend.Points).MedianPrice);Assert.Equal(1,trend.Sales);
        Assert.True(trend.MaximumY>trend.MinimumY);
    }
    [Fact]
    public void NoRecentSalesIsAnExplicitEmptySeriesWithFiniteAxisBounds()
    {
        var state=new ExplorerState([Row("old","2017-01",300000),Row("latest","2026-10",700000,"OTHER")]);
        state.Select("old");var trend=BuyerTrend.Build(state);
        Assert.Equal(24,trend.Points.Count);Assert.Equal(0,trend.Sales);Assert.Equal(0,trend.ObservedMonths);
        Assert.All(trend.Points,p=>Assert.Null(p.MedianPrice));Assert.True(trend.MaximumY>trend.MinimumY);
    }
}
