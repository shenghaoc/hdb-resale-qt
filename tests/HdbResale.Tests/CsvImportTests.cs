using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class CsvImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-test-" + Guid.NewGuid());
    private const string Header = "source_row,month,town,flat_type,block,street_name,resale_price\n";
    private const string LocationHeader = "town,block,street_name,latitude,longitude,quality,source\n";
    public CsvImportTests() { Directory.CreateDirectory(directory); }
    public void Dispose() => Directory.Delete(directory, true);
    private ImportResult Load(string transactions, string locations = LocationHeader)
    {
        File.WriteAllText(Path.Combine(directory, "transactions.csv"), transactions);
        File.WriteAllText(Path.Combine(directory, "locations.csv"), locations);
        return CsvImport.LoadDirectory(directory);
    }
    [Fact]
    public void QuotedFieldsAndDecimalPricesKeepFactsSeparateFromDerivedCoordinates()
    {
        var result = Load(Header + "2,2024-02,T,3 ROOM,1,\"STREET, ONE\",123.45\n",
            LocationHeader + "T,1,\"STREET, ONE\",1.3,103.8,StreetApproximation,official anchor\n");
        var row = Assert.Single(result.Accepted);
        Assert.Equal(123.45m, row.Price);
        Assert.Equal("2024-02", row.Facts.Month.ToString());
        Assert.Equal("STREET, ONE", row.Facts.Street);
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
    [Theory]
    [InlineData("91,103,StreetApproximation,source")]
    [InlineData("1,181,BlockApproximation,source")]
    [InlineData("NaN,103,StreetApproximation,source")]
    [InlineData("1,103,Missing,source")]
    [InlineData(",,Authoritative,source")]
    [InlineData("1,103,StreetApproximation,")]
    [InlineData("1,103,Exact,source")]
    public void BadLocationIsRejectedButValidTransactionRemainsUnlocated(string fields)
    {
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n", LocationHeader + "T,1,ST," + fields + "\n");
        var row = Assert.Single(result.Accepted);
        Assert.Equal(LocationQuality.Missing, row.Location.Quality);
        Assert.Null(row.Location.Point);
        Assert.Equal("locations.csv", Assert.Single(result.Rejected).File);
    }
    [Fact]
    public void MissingLocationWithReasonIsAccepted()
    {
        var result = Load(Header + "2,2024-01,T,3 ROOM,1,ST,100\n", LocationHeader + "T,1,ST,,,Missing,no coverage\n");
        Assert.Empty(result.Diagnostics);
        Assert.Equal("no coverage", Assert.Single(result.Accepted).Location.Source);
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
    }
    [Fact]
    public void MalformedQuotedRowAndDuplicateSourceIdAreDiagnosed()
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
    public void InvalidGeoPointsAndContradictoryQualityCannotBeConstructed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPoint(double.PositiveInfinity, 103));
        Assert.Throws<ArgumentException>(() => new DerivedLocation(new(1, 103), LocationQuality.Missing, "source"));
        Assert.Throws<ArgumentException>(() => new DerivedLocation(null, LocationQuality.Authoritative, "source"));
    }
}
