using HdbResale.Domain;
using System.Text.Json;
using Xunit;
namespace HdbResale.Tests;
public sealed class ExplorerStateTests
{
    internal static ImportResult Fixture() => CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
    [Fact]
    public void FixturePointsPinOfficialFootprintDerivationAndInferredStreetJoin()
    {
        var data = Path.Combine(AppContext.BaseDirectory, "data");
        using var shapes = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "building-evidence.geojson")));
        using var codes = JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "street-code-evidence.json")));
        foreach (var code in codes.RootElement.EnumerateArray())
        {
            var street = code.GetProperty("street_name").GetString();
            var addressBlocks = File.ReadAllLines(Path.Combine(data, "address-evidence.csv")).Skip(1)
                .Select(line => line.Split(','))
                .Where(fields => fields[1] == street).Select(fields => fields[0]).Order().ToArray();
            var codeBlocks = code.GetProperty("matching_blocks").EnumerateArray()
                .Select(b => b.GetString()).Order().ToArray();
            Assert.Equal(addressBlocks, codeBlocks);
        }
        var result = Fixture();
        var expected = new Dictionary<string, int> { ["HDB-34"] = 937499, ["HDB-371"] = 942992,
            ["HDB-382"] = 945486, ["HDB-7555"] = 936585, ["HDB-24303"] = 937147 };
        foreach (var (id, objectId) in expected)
        {
            var row = Assert.Single(result.Accepted, t => t.Id == id);
            Assert.Equal(LocationQuality.BlockApproximation, row.Location.Quality);
            var shape = shapes.RootElement.GetProperty("features").EnumerateArray().Single(f =>
                f.GetProperty("properties").GetProperty("OBJECTID").GetInt32() == objectId);
            var properties = shape.GetProperty("properties");
            Assert.Equal(row.Facts.Block, properties.GetProperty("BLK_NO").GetString());
            Assert.Contains($"OBJECTID {objectId}", row.Location.Source);
            var code = codes.RootElement.EnumerateArray().Single(c =>
                c.GetProperty("street_name").GetString() == row.Facts.Street);
            Assert.Equal(properties.GetProperty("ST_COD").GetString(), code.GetProperty("street_code").GetString());
            Assert.Contains(code.GetProperty("matching_blocks").EnumerateArray(), b => b.GetString() == row.Facts.Block);
            var vertices = shape.GetProperty("geometry").GetProperty("coordinates")[0].EnumerateArray().ToArray();
            var latitude = (vertices.Min(v => v[1].GetDouble()) + vertices.Max(v => v[1].GetDouble())) / 2;
            var longitude = (vertices.Min(v => v[0].GetDouble()) + vertices.Max(v => v[0].GetDouble())) / 2;
            Assert.Equal(latitude, row.Location.Point!.Latitude, 9);
            Assert.Equal(longitude, row.Location.Point.Longitude, 9);
        }
        var missing = Assert.Single(result.Accepted, t => t.Id == "HDB-1188");
        Assert.Equal(LocationQuality.Missing, missing.Location.Quality);
        Assert.Null(missing.Location.Point);
    }
    [Fact]
    public void ImportedFilterClearsHiddenSelectionAndRetainsUnlocatedRows()
    {
        var result = Fixture();
        Assert.Empty(result.Diagnostics);
        Assert.Equal(6, result.Accepted.Count);
        Assert.Equal(5, result.Accepted.Count(t => t.Location.Point is not null));
        var state = new ExplorerState(result.Accepted);
        state.Select("HDB-34");
        state.Filter("ANG MO KIO", 238000m);
        var unlocated = Assert.Single(state.Visible);
        Assert.Equal("HDB-1188", unlocated.Id);
        Assert.Null(unlocated.Location.Point);
        Assert.Null(state.Selected);
        state.Select("HDB-34");
        Assert.Null(state.Selected);
        state.Select(unlocated.Id);
        Assert.Equal(unlocated, state.Selected);
        state.Reset();
        Assert.Equal(unlocated, state.Selected);
        Assert.Equal(6, state.Visible.Count);
        state.Filter("CLEMENTI", 0);
        Assert.Empty(state.Visible);
        Assert.Null(state.Selected);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Filter("All towns", -1));
        Assert.Equal("CLEMENTI", state.Town);
    }
}
