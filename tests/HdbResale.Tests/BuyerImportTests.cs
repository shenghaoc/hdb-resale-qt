using System.Globalization;
using System.Text.Json;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

public sealed class BuyerImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-buyer-" + Guid.NewGuid());
    private const string Required = "source_row,month,town,flat_type,block,street_name,resale_price";
    private const string Optional = ",storey_range,floor_area_sqm,flat_model,lease_commence_date,remaining_lease";
    public BuyerImportTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "address-evidence.csv"), "source_row,blk_no,street\n2,1,ST\n");
        File.WriteAllText(Path.Combine(directory, "postal-address-evidence.csv"), "source_row,block,street_name,postal_code\n2,1,ST,123456\n");
        File.WriteAllText(Path.Combine(directory, "building-evidence.geojson"), """
            {"type":"FeatureCollection","features":[{"properties":{"OBJECTID":42,"ENTITYID":77,"BLK_NO":"1","POSTAL_COD":"123456"},"geometry":{"type":"Polygon","coordinates":[[[103.7,1.2],[103.9,1.2],[103.9,1.4],[103.7,1.2]]]}}]}
            """);
    }
    public void Dispose() => Directory.Delete(directory, true);
    private ImportResult Load(string csv)
    {
        File.WriteAllText(Path.Combine(directory, "transactions.csv"), csv);
        var result = CsvImport.LoadDirectory(directory);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(CsvImport.LoadDirectory(directory, referenceCsv: true)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(CsvImport.LoadDirectory(directory, indexed: false)));
        return result;
    }
    private static string LegacyProjection(ImportResult result) => JsonSerializer.Serialize(new
    {
        Accepted = result.Accepted.Select(t => new
        {
            t.Id, Facts = new { t.Facts.Month, t.Facts.Town, t.Facts.Block, t.Facts.Street, t.Facts.FlatType, t.Facts.Price },
            t.Location, t.Match
        }), result.Rejected, result.Diagnostics, result.MatchedCount, result.AmbiguousCount, result.UnmatchedCount
    });

    [Fact]
    public void OptionalFactsPreserveRawTextAndDoNotChangeExistingSourceIdentityOrEvidence()
    {
        var old = Load(Required + "\n00200,2024-02,T,3 ROOM,1,ST,400000\n");
        var extended = Load(Required + Optional + "\n00200,2024-02,T,3 ROOM,1,ST,400000,01 TO 03,080.50,New Generation,01980,62 years 05 months\n");
        Assert.Equal(LegacyProjection(old), LegacyProjection(extended));
        var row = Assert.Single(extended.Accepted);
        Assert.Equal("HDB-200", row.Id);
        Assert.Equal("01 TO 03", row.Facts.StoreyRange);
        Assert.Equal("080.50", row.Facts.FloorAreaSqmSource);
        Assert.Equal(80.5m, row.Facts.FloorAreaSqm);
        Assert.Equal("New Generation", row.Facts.FlatModel);
        Assert.Equal("01980", row.Facts.LeaseCommenceDateSource);
        Assert.Equal(1980, row.Facts.LeaseCommenceYear);
        Assert.Equal("62 years 05 months", row.Facts.RemainingLeaseSource);
        Assert.Equal(749, row.Facts.RemainingLeaseMonths);
        Assert.Equal(400000m / 80.5m, row.PricePerSqm);
        Assert.Empty(extended.Diagnostics);
        var legacy = Assert.Single(old.Accepted);
        Assert.Null(legacy.Facts.StoreyRange); Assert.Null(legacy.Facts.FloorAreaSqmSource);
        Assert.Null(legacy.Facts.FloorAreaSqm); Assert.Null(legacy.Facts.FlatModel);
        Assert.Null(legacy.Facts.LeaseCommenceDateSource); Assert.Null(legacy.Facts.LeaseCommenceYear);
        Assert.Null(legacy.Facts.RemainingLeaseSource); Assert.Null(legacy.Facts.RemainingLeaseMonths);
        Assert.Null(legacy.PricePerSqm);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("unknown", "unknown")]
    [InlineData("0", "0")]
    [InlineData("-10", "-1")]
    [InlineData("NaN", "10000")]
    [InlineData("1e2", "1980.0")]
    [InlineData(" 80 ", " 1980 ")]
    [InlineData("79228162514264337593543950336", "2147483648")]
    public void MissingOrMalformedNumericOptionalFieldsAreNullWithoutRejectingTransaction(string area, string year)
    {
        var result = Load(Required + Optional + $"\n200,2024-02,T,3 ROOM,1,ST,400000,unknown,{area},unknown,{year},not a lease\n");
        var row = Assert.Single(result.Accepted);
        Assert.Empty(result.Rejected); Assert.Empty(result.Diagnostics);
        Assert.Equal(area, row.Facts.FloorAreaSqmSource); Assert.Null(row.Facts.FloorAreaSqm);
        Assert.Equal(year, row.Facts.LeaseCommenceDateSource); Assert.Null(row.Facts.LeaseCommenceYear);
        Assert.Equal("not a lease", row.Facts.RemainingLeaseSource); Assert.Null(row.Facts.RemainingLeaseMonths);
        Assert.Null(row.PricePerSqm);
    }

    [Theory]
    [InlineData("62 years 05 months", 749)]
    [InlineData("62 years 5 months", 749)]
    [InlineData("1 year 1 month", 13)]
    [InlineData("99 years", 1188)]
    [InlineData("1 year", 12)]
    [InlineData("0 years 00 months", 0)]
    [InlineData("0 years 11 months", 11)]
    [InlineData("60 years 12 months", null)]
    [InlineData("60 years 99 months", null)]
    [InlineData("60 years 005 months", null)]
    [InlineData("60 years -1 months", null)]
    [InlineData("60.5 years", null)]
    [InlineData("60", 720)]
    [InlineData("070", 840)]
    [InlineData("0", 0)]
    [InlineData("1000", null)]
    [InlineData("-1", null)]
    [InlineData("", null)]
    [InlineData("60 years 05 months extra", null)]
    public void SourceLeaseHasSafeOptionalMonthParsingAndKeepsExactText(string source, int? expected)
    {
        var result = Load(Required + ",remaining_lease\n200,2024-02,T,3 ROOM,1,ST,400000," + source + "\n");
        var row = Assert.Single(result.Accepted);
        Assert.Equal(source, row.Facts.RemainingLeaseSource);
        Assert.Equal(expected, row.Facts.RemainingLeaseMonths);
        Assert.Empty(result.Diagnostics); Assert.Empty(result.Rejected);
    }

    [Fact]
    public void ReorderedQuotedOptionalTextAndRequiredRejectionsKeepAllOriginalEvidence()
    {
        var result = Load("remaining_lease,resale_price,source_row,flat_model,street_name,block,month,town,flat_type,storey_range,floor_area_sqm,lease_commence_date\n" +
            "62 years 05 months,400000,00200,\"Model, \"\"A\"\"\",ST,1,2024-02,T,3 ROOM,\" 01 TO 03 \" ,\"80,5\",1980\n" +
            "unknown,0,201,unknown,ST,1,2024-02,T,3 ROOM,unknown,unknown,unknown\n");
        var row = Assert.Single(result.Accepted);
        Assert.Equal("Model, \"A\"", row.Facts.FlatModel);
        Assert.Equal(" 01 TO 03 ", row.Facts.StoreyRange);
        Assert.Equal("80,5", row.Facts.FloorAreaSqmSource); Assert.Null(row.Facts.FloorAreaSqm);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal("Price must be a positive decimal.", rejected.Reason);
        Assert.Equal("201", rejected.Fields[2]); Assert.Equal("unknown", rejected.Fields[^1]);
        Assert.Single(result.Diagnostics);
    }

    [Theory]
    [InlineData("floor_area_sqm,floor_area_sqm")]
    [InlineData("remaining_lease,remaining_lease")]
    [InlineData("extra,extra")]
    public void DuplicateOptionalOrUnknownHeadersStillFailTheExistingHeaderContract(string columns)
    {
        var result = Load(Required + "," + columns + "\n");
        Assert.Empty(result.Accepted); Assert.Empty(result.Rejected);
        Assert.Equal(new ImportDiagnostic("transactions.csv", 1, "Missing or duplicate required CSV header."), Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void OptionalFieldsDoNotRelaxRequiredValidationOrCanonicalSourceRows()
    {
        var result = Load(Required + Optional + "\n" +
            "200,2024-13,T,3 ROOM,1,ST,100,01 TO 03,80,Model A,1980,62 years\n" +
            "200,2024-02,T,3 ROOM,1,ST,100,unknown,unknown,unknown,unknown,unknown\n" +
            "00200,2024-02,T,3 ROOM,1,ST,200,01 TO 03,80,Model A,1980,62 years\n");
        Assert.Equal("HDB-200", Assert.Single(result.Accepted).Id);
        Assert.Equal(2, result.Rejected.Count);
        Assert.Contains(result.Rejected, r => r.Reason == "Month must be a valid yyyy-MM.");
        Assert.Contains(result.Rejected, r => r.Reason == "Source row must be unique and at least 2.");
    }

    [Fact]
    public void NumericOptionalParsingIsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var result = Load(Required + Optional + "\n200,2024-02,T,3 ROOM,1,ST,100,01 TO 03,80.5,Model A,1980,62 years 05 months\n");
            Assert.Equal(80.5m, Assert.Single(result.Accepted).Facts.FloorAreaSqm);
            Assert.Equal(749, result.Accepted[0].Facts.RemainingLeaseMonths);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
