using HdbResale.Domain;
using System.Text.Json;
using Xunit;
namespace HdbResale.Tests;
public sealed class ExplorerStateTests
{
    internal static ImportResult Fixture() => CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    [Fact]
    public void CanonicalFixturePinsExplicitAddressesPostalLinkAndOriginalMidpoints()
    {
        var result = Fixture();
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Rejected);
        Assert.Equal(6, result.Accepted.Count);
        Assert.Equal(6, result.MatchedCount);
        Assert.Equal(0, result.AmbiguousCount);
        Assert.Equal(0, result.UnmatchedCount);
        var expected = new Dictionary<string, (int ObjectId, int EntityId, string Postal, int Assertions)>
        {
            ["HDB-34"] = (937499, 7861, "560509", 5), ["HDB-1188"] = (946126, 4194, "560510", 1),
            ["HDB-371"] = (942992, 2806, "120449", 9), ["HDB-382"] = (945486, 2850, "120461", 6),
            ["HDB-7555"] = (936585, 9869, "520503", 3), ["HDB-24303"] = (937147, 9870, "520505", 6)
        };
        using var shapes = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "building-evidence.geojson")));
        foreach (var (id, e) in expected)
        {
            var row = Assert.Single(result.Accepted, t => t.Id == id);
            Assert.Equal(MatchQuality.NormalizedAddress, row.Match.Quality);
            // Actual HDB resale/property fields match directly, without normalization.
            var property = Assert.Single(row.Match.PropertyCandidates);
            Assert.Equal(row.Facts.Block, property.Block);
            Assert.Equal(row.Facts.Street, property.Street);
            Assert.Equal(e.Assertions, row.Match.PostalAssertions.Count);
            Assert.All(row.Match.PostalAssertions, p => Assert.Equal(e.Postal, p.PostalCode));
            var footprint = Assert.Single(row.Match.FootprintCandidates);
            Assert.Equal(e.ObjectId, footprint.Identity.ObjectId);
            Assert.Equal(e.EntityId, footprint.Identity.EntityId);
            Assert.Equal(e.Postal, footprint.Identity.PostalCode);
            Assert.Equal(row.Facts.Block, footprint.Identity.Block);
            Assert.Equal(CoordinateQuality.BlockApproximation, row.Location.Quality);
            Assert.Contains($"OBJECTID {e.ObjectId}", row.Location.Source);
            var shape = shapes.RootElement.GetProperty("features").EnumerateArray().Single(f =>
                f.GetProperty("properties").GetProperty("OBJECTID").GetInt32() == e.ObjectId);
            var vertices = shape.GetProperty("geometry").GetProperty("coordinates")[0].EnumerateArray().ToArray();
            Assert.Equal((vertices.Min(v => v[1].GetDouble()) + vertices.Max(v => v[1].GetDouble())) / 2,
                row.Location.Point!.Latitude, 9);
            Assert.Equal((vertices.Min(v => v[0].GetDouble()) + vertices.Max(v => v[0].GetDouble())) / 2,
                row.Location.Point.Longitude, 9);
        }
        // Pin an original M2 point as well as the derivation, not merely self-consistency.
        Assert.Equal(1.3739973579, result.Accepted.Single(t => t.Id == "HDB-34").Location.Point!.Latitude);
        Assert.Equal(103.8501378907, result.Accepted.Single(t => t.Id == "HDB-34").Location.Point!.Longitude);
    }
    [Fact]
    public void ImportedFilterClearsHiddenSelectionAndPreservesVisibleSelection()
    {
        var result = Fixture();
        Assert.Empty(result.Diagnostics);
        Assert.Equal(6, result.Accepted.Count);
        Assert.Equal(6, result.Accepted.Count(t => t.Location.Point is not null));
        var state = new ExplorerState(result.Accepted);
        state.Select("HDB-34");
        state.Filter("ANG MO KIO", 238000m);
        var visible = Assert.Single(state.Visible);
        Assert.Equal("HDB-1188", visible.Id);
        Assert.NotNull(visible.Location.Point);
        Assert.Null(state.Selected);
        state.Select("HDB-34");
        Assert.Null(state.Selected);
        state.Select(visible.Id);
        Assert.Equal(visible, state.Selected);
        state.Reset();
        Assert.Equal(visible, state.Selected);
        Assert.Equal(6, state.Visible.Count);
        state.Filter("CLEMENTI", 0);
        Assert.Empty(state.Visible);
        Assert.Null(state.Selected);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Filter("All towns", -1));
        Assert.Equal("CLEMENTI", state.Town);
    }
}
