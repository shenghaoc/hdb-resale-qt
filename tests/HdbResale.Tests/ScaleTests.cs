using System.Text.Json;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class ScaleTests
{
    [Theory]
    [InlineData("data")]
    [InlineData("coverage")]
    public void IndexAndReusePreserveEveryEvidenceField(string directory)
    {
        var path = Path.Combine(AppContext.BaseDirectory, directory);
        var indexed = CsvImport.LoadDirectory(path);
        var reference = CsvImport.LoadDirectory(path, indexed: false);
        Assert.Equal(JsonSerializer.Serialize(reference), JsonSerializer.Serialize(indexed));
    }
    [Fact]
    public void AddressProjectionIsOrderIndependentAndConservesLocatedRows()
    {
        var original = ExplorerStateTests.Fixture().Accepted;
        var rows = original.SelectMany((t, i) => Enumerable.Range(0, 1000).Select(n =>
            t with { Id = $"large-{i}-{n}", Facts = t.Facts with { Price = 100_000 + n } })).ToArray();
        var grouped = BlockSummaries.Located(rows);
        Assert.Equal(6, grouped.Count);
        Assert.Equal(6000, grouped.Sum(g => g.Count));
        Assert.All(grouped, g => Assert.Equal(100_499.5m, g.MedianPrice));
        Assert.Equal(JsonSerializer.Serialize(grouped), JsonSerializer.Serialize(BlockSummaries.Located(rows.Reverse().ToArray())));
        var state = new ExplorerState(rows);
        state.Select(rows[^1].Id);
        Assert.Equal(rows[^1].Id, state.Selected!.Id);
        state.Filter("All towns", 100_100);
        Assert.Equal(state.Visible.Count, BlockSummaries.Located(state.Visible).Sum(g => g.Count));
        Assert.Null(state.Selected);
        state.Filter("All towns", 0);
        Assert.Empty(BlockSummaries.Located(state.Visible));
        state.Reset(); Assert.Equal(6000, state.Visible.Count);
    }
    [Fact]
    public void UnlocatedAndAmbiguousRowsRemainSelectableOutsideMapProjection()
    {
        var original = ExplorerStateTests.Fixture().Accepted[0];
        var missing = original with { Id = "missing", Location = new(null, CoordinateQuality.Missing, "explicit missing") };
        var ambiguous = missing with { Id = "ambiguous", Match = new(MatchQuality.Ambiguous, "conflict", [], [], []) };
        var rows = new[] { original, missing, ambiguous };
        Assert.Equal(1, Assert.Single(BlockSummaries.Located(rows)).Count);
        var state = new ExplorerState(rows);
        Assert.Equal(3, state.Visible.Count);
        state.Select(ambiguous.Id); Assert.Equal(ambiguous, state.Selected);
        state.Select(missing.Id); Assert.Equal(missing, state.Selected);
    }
    [Fact]
    public void InconsistentPointsAtOneAddressAreNeverSilentlyCombined()
    {
        var row = ExplorerStateTests.Fixture().Accepted[0];
        var different = row with { Id = "different", Location = new(new(1.2,103.8),CoordinateQuality.BlockApproximation,"other footprint") };
        Assert.Throws<InvalidDataException>(()=>BlockSummaries.Located(new[] {row,different}));
    }
    [Fact]
    public void FullSizedCsvKeepsEveryUniqueSourceRow()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hdb-scale-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var fixture = Path.Combine(AppContext.BaseDirectory,"data");
            foreach (var name in new[] { "address-evidence.csv", "postal-address-evidence.csv", "building-evidence.geojson" })
                File.Copy(Path.Combine(fixture,name),Path.Combine(directory,name));
            using (var writer = new StreamWriter(Path.Combine(directory,"transactions.csv")))
            {
                writer.WriteLine("source_row,month,town,flat_type,block,street_name,resale_price");
                for (var i=2;i<241_922;i++) writer.WriteLine($"{i},2024-01,ANG MO KIO,3 ROOM,509,ANG MO KIO AVE 8,300000");
            }
            var result = CsvImport.LoadDirectory(directory);
            Assert.Empty(result.Rejected); Assert.Empty(result.Diagnostics);
            Assert.Equal(241_920,result.Accepted.Count);
            Assert.Equal(241_920,result.Accepted.Select(t=>t.Id).Distinct().Count());
            Assert.Equal(241_920,Assert.Single(BlockSummaries.Located(result.Accepted)).Count);
            var state = new ExplorerState(result.Accepted);
            state.Select("HDB-241921"); Assert.NotNull(state.Selected);
            state.Filter("All towns",0); Assert.Null(state.Selected);
        }
        finally { Directory.Delete(directory,true); }
    }
}
