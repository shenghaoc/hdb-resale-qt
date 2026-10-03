using HdbResale.Domain;
using System.Text.Json.Nodes;
using Xunit;
namespace HdbResale.Tests;
public sealed class CsvImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-test-" + Guid.NewGuid());
    private const string Header = "source_row,month,town,flat_type,block,street_name,resale_price\n";
    private const string PropertyHeader = "source_row,blk_no,street\n";
    private const string PostalHeader = "source_row,block,street_name,postal_code\n";
    public CsvImportTests() { Directory.CreateDirectory(directory); }
    public void Dispose() => Directory.Delete(directory, true);
    private static JsonObject Feature(int id, JsonNode? geometry) => new()
    {
        ["type"] = "Feature",
        ["properties"] = new JsonObject { ["OBJECTID"] = id, ["ENTITYID"] = 77, ["BLK_NO"] = "1", ["POSTAL_COD"] = "123456" },
        ["geometry"] = geometry
    };
    private static JsonNode Polygon() => JsonNode.Parse("""
        {"type":"Polygon","coordinates":[[[103.7,1.2],[103.9,1.2],[103.9,1.4],[103.7,1.4],[103.7,1.2]]]}
        """)!;
    private ImportResult Load(string transactions, string properties = PropertyHeader,
        string postals = PostalHeader, params JsonObject[] features)
    {
        File.WriteAllText(Path.Combine(directory, "transactions.csv"), transactions);
        File.WriteAllText(Path.Combine(directory, "address-evidence.csv"), properties);
        File.WriteAllText(Path.Combine(directory, "postal-address-evidence.csv"), postals);
        File.WriteAllText(Path.Combine(directory, "building-evidence.geojson"),
            new JsonObject { ["type"] = "FeatureCollection", ["features"] = new JsonArray(features.Select(f => (JsonNode)f).ToArray()) }.ToJsonString());
        return CsvImport.LoadDirectory(directory);
    }
    [Fact]
    public void QuotedFieldsAndDecimalPricesKeepFactsSeparateFromDerivedCoordinates()
    {
        var result = Load(Header + "2,2024-02,T,3 ROOM,1,\"STREET, ONE\",123.45\n",
            PropertyHeader + "2,1,\"STREET, ONE\"\n", PostalHeader + "2,1,\"STREET, ONE\",123456\n", Feature(42, Polygon()));
        var row = Assert.Single(result.Accepted);
        Assert.Equal(123.45m, row.Price);
        Assert.Equal("2024-02", row.Facts.Month.ToString());
        Assert.Equal("STREET, ONE", row.Facts.Street);
        Assert.Equal(MatchQuality.ExactAddress, row.Match.Quality);
        Assert.Equal(1.3, row.Location.Point!.Latitude);
        Assert.Empty(result.Rejected);
    }
    [Theory]
    [InlineData("2,2024-13,T,3 ROOM,1,ST,100", "Month")]
    [InlineData("2,2024-1,T,3 ROOM,1,ST,100", "Month")]
    [InlineData("2,2024-01,T,3 ROOM,1,ST,abc", "Price")]
    [InlineData("2,2024-01,T,3 ROOM,1,ST,0", "Price")]
    [InlineData("2,2024-01,T,3 ROOM,1,ST,-1", "Price")]
    [InlineData("2,2024-01,,3 ROOM,1,ST,100", "required")]
    [InlineData("2,2024-01,T,,1,ST,100", "required")]
    [InlineData("2,2024-01,T,3 ROOM,,ST,100", "required")]
    [InlineData("2,2024-01,T,3 ROOM,1,,100", "required")]
    [InlineData("2,2024-01,T,3 ROOM,1", "Field count")]
    public void InvalidRowDoesNotDiscardReadableValidRow(string bad, string reason)
    {
        var result = Load(Header + bad + "\n3,2024-02,T,3 ROOM,2,ST,100\n");
        Assert.Equal("HDB-3", Assert.Single(result.Accepted).Id);
        var rejection = Assert.Single(result.Rejected);
        Assert.Equal(2, rejection.Row);
        Assert.Contains(reason, rejection.Reason);
        Assert.NotEmpty(rejection.Fields);
    }
    [Fact]
    public void InvalidGeometryDoesNotDiscardOtherReadableFootprintsOrTransaction()
    {
        var bad = Polygon(); bad["coordinates"]![0]![0]![1] = 91;
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n",
            PropertyHeader + "2,1,ST\n", PostalHeader + "2,1,ST,123456\n", Feature(99, bad), Feature(42, Polygon()));
        var row = Assert.Single(result.Accepted);
        Assert.Equal(42, row.Match.MatchedFootprint!.Identity.ObjectId);
        Assert.Equal(CoordinateQuality.BlockApproximation, row.Location.Quality);
        Assert.Equal("building-evidence.geojson", Assert.Single(result.Rejected).File);
        Assert.Contains("finite", Assert.Single(result.Diagnostics).Message);
    }
    [Fact]
    public void MatchedIdentityCanHaveMissingCoordinatesWithoutLosingItsProvenance()
    {
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n",
            PropertyHeader + "2,1,ST\n", PostalHeader + "2,1,ST,123456\n", Feature(42, null));
        var row = Assert.Single(result.Accepted);
        Assert.True(row.Match.IsMatched);
        Assert.Equal(CoordinateQuality.Missing, row.Location.Quality);
        Assert.Null(row.Location.Point);
        Assert.Contains("OBJECTID 42", row.Location.Source);
        Assert.Empty(result.Diagnostics);
    }
    [Fact]
    public void AmbiguousIdentityIsRetainedInImportCountsWithoutCoordinates()
    {
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n",
            PropertyHeader + "2,1,ST\n", PostalHeader + "2,1,ST,123456\n",
            Feature(42, Polygon()), Feature(99, Polygon()));
        var row = Assert.Single(result.Accepted);
        Assert.Equal(MatchQuality.Ambiguous, row.Match.Quality);
        Assert.Equal(1, result.AmbiguousCount);
        Assert.Equal(0, result.MatchedCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Null(row.Location.Point);
        Assert.Empty(result.Rejected);
    }
    [Fact]
    public void StructuralAndIoFailuresAreVisibleDiagnostics()
    {
        var result = Load("month,town\n2024-01,T\n");
        Assert.Empty(result.Accepted);
        Assert.Contains(result.Diagnostics, d => d.Row == 1 && d.Message.Contains("header"));
        File.Delete(Path.Combine(directory, "transactions.csv"));
        result = CsvImport.LoadDirectory(directory);
        Assert.Contains(result.Diagnostics, d => d.File == "transactions.csv" && d.Row == 0);
        Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n");
        File.WriteAllText(Path.Combine(directory, "building-evidence.geojson"), "{bad");
        result = CsvImport.LoadDirectory(directory);
        Assert.Single(result.Accepted);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Malformed GeoJSON"));
    }
    [Fact]
    public void MalformedCsvAndCanonicalDuplicateIdAreDiagnosed()
    {
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100,extra\n" +
            "3,2024-01,T,3 ROOM,1,ST,100\n003,2024-01,T,3 ROOM,1,ST,100\n");
        Assert.Single(result.Accepted);
        Assert.Equal(2, result.Rejected.Count);
        result = Load(Header + "2,2024-01,T,3 ROOM,1,\"bad\"text,100\n3,2024-01,T,3 ROOM,2,ST,200\n");
        Assert.Single(result.Accepted);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("quoting"));
    }
    [Fact]
    public void BadPostalRecordIsRejectedWhileValidTransactionRemainsUnmatched()
    {
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n", PropertyHeader + "2,1,ST\n",
            PostalHeader + "2,1,ST,12345\n");
        Assert.Equal(MatchQuality.Unmatched, Assert.Single(result.Accepted).Match.Quality);
        Assert.Equal("postal-address-evidence.csv", Assert.Single(result.Rejected).File);
        Assert.Equal(1, result.UnmatchedCount);
    }
    [Fact]
    public void InvalidCoordinatesAndContradictoryQualityCannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(double.PositiveInfinity, 103));
        Assert.Throws<ArgumentException>(() => new DerivedLocation(new(1, 103), CoordinateQuality.Missing, "source"));
        Assert.Throws<ArgumentException>(() => new DerivedLocation(null, CoordinateQuality.BlockApproximation, "source"));
        Assert.Throws<ArgumentException>(() => new DerivedLocation(new(1, 103), CoordinateQuality.BlockApproximation, ""));
    }
}
