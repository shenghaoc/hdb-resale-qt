using System.Text.Json;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

public sealed class AddressCoverageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-m9-test-" + Guid.NewGuid());
    private const string A = "d_8575e84912df3c28995b8e6e0e05205a";
    private const string B = "d_3a3807c023c61ddfba947dc069eb53f2";
    public AddressCoverageTests()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"transactions.csv"),"source_row,month,town,flat_type,block,street_name,resale_price\n2,2024-01,T,3 ROOM,1,ST,100\n3,2024-02,T,3 ROOM,1,ST,200\n");
        File.WriteAllText(Path.Combine(directory,"address-evidence.csv"),"source_row,blk_no,street\n2,1,ST\n");
        File.WriteAllText(Path.Combine(directory,"building-evidence.geojson"),"""
            {"type":"FeatureCollection","features":[{"properties":{"OBJECTID":42,"ENTITYID":77,"BLK_NO":"1","POSTAL_COD":"123456"},"geometry":null}]}
            """);
    }
    public void Dispose() => Directory.Delete(directory, true);
    private void Postals(string rows) => File.WriteAllText(Path.Combine(directory,"postal-address-evidence.csv"),
        "source_row,block,street_name,postal_code,source_dataset\n" + rows);
    [Fact]
    public void DatasetQualifiedRowsPreserveAllAssertionsIncludingSameOrdinalAcrossDatasets()
    {
        Postals($"2,1,ST,123456,{A}\n2,1,ST,123456,{B}\n");
        var result=CsvImport.LoadDirectory(directory);
        Assert.All(result.Accepted,t=>{
            Assert.Equal(MatchQuality.ExactAddress,t.Match.Quality);
            Assert.Equal(new[]{A,B},t.Match.PostalAssertions.Select(p=>p.SourceDataset));
            Assert.Equal(new long[]{2,2},t.Match.PostalAssertions.Select(p=>p.SourceRow));
        });
        Assert.Empty(result.Diagnostics);
        Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(CsvImport.LoadDirectory(directory,indexed:false)));
        Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(CsvImport.LoadDirectory(directory,referenceCsv:true)));
        var report=AddressCoverageStudy.Run(directory);
        var row=Assert.Single(report.Rows);
        Assert.Equal(2,report.Transactions);Assert.Equal(2,row.Transactions);
        Assert.Equal(new[]{"HDB-2","HDB-3"},row.Ids);
        Assert.Equal(CoverageReason.MatchedWithoutGeometry,row.Reason);
    }
    [Fact]
    public void NewSourceConflictingWithPreviouslyMatchedEvidenceAlwaysMakesAmbiguous()
    {
        Postals($"2,1,ST,123456,{B}\n");
        Assert.All(CsvImport.LoadDirectory(directory).Accepted,t=>Assert.True(t.Match.IsMatched));
        Postals($"2,1,ST,123456,{B}\n2,1,ST,654321,{A}\n3,1,ST,123456,{B}\n");
        Assert.All(CsvImport.LoadDirectory(directory).Accepted,t=>{
            Assert.Equal(MatchQuality.Ambiguous,t.Match.Quality);Assert.Null(t.Location.Point);
            Assert.Equal(3,t.Match.PostalAssertions.Count);
            Assert.Equal(CoverageReason.ConflictingPostals,CoverageStudy.Reason(t));
        });
    }
    [Theory]
    [InlineData("")]
    [InlineData("ACRA A")]
    [InlineData("https://example.org")]
    [InlineData("d_8575e84912df3c28995b8e6e0e05205z")]
    public void InvalidDatasetIdentityRejectsAssertionInsteadOfInventingProvenance(string source)
    {
        Postals($"2,1,ST,123456,{source}\n");var result=CsvImport.LoadDirectory(directory);
        Assert.Single(result.Rejected);Assert.Single(result.Diagnostics);
        Assert.All(result.Accepted,t=>Assert.Empty(t.Match.PostalAssertions));
    }
    [Theory]
    [InlineData("1234")]
    [InlineData("")]
    [InlineData("ABCDEF")]
    [InlineData("１２３４５６")]
    public void MalformedPublicAssertionBlocksOtherwiseUniqueAddressWithoutRepair(string malformed)
    {
        Postals($"2,1,ST,123456,{B}\n3,1,ST,{malformed},{A}\n");
        var result=CsvImport.LoadDirectory(directory);
        Assert.Empty(result.Rejected);Assert.Single(result.Diagnostics);
        Assert.All(result.Accepted,t=>{
            Assert.Equal(MatchQuality.Ambiguous,t.Match.Quality);Assert.Null(t.Location.Point);
            Assert.Equal(CoverageReason.InvalidPostalAssertion,CoverageStudy.Reason(t));
            Assert.Contains(t.Match.PostalAssertions,p=>p.PostalCode==malformed);
            Assert.Equal(2,t.Match.PostalAssertions.Count);
        });
    }
    [Fact]
    public void UnapprovedHistoricalSidecarCannotSilentlyInfluenceCoordinates()
    {
        Postals("");File.WriteAllText(Path.Combine(directory,"historical-postal-evidence.json"),"{}");
        var result=CsvImport.LoadDirectory(directory);
        Assert.All(result.Accepted,t=>{Assert.Null(t.Match.HistoricalOneMap);Assert.Equal(MatchQuality.Unmatched,t.Match.Quality);});
        Assert.Contains("projection hash mismatch",Assert.Single(result.Diagnostics).Message);
    }
    [Fact]
    public void LegacyCanonicalAndFrozenCoverageOutputsOmitOptionalDatasetField()
    {
        foreach(var name in new[]{"data","coverage"})
        {
            var result=CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory,name));
            Assert.DoesNotContain("SourceDataset",JsonSerializer.Serialize(result));
            var report=AddressCoverageStudy.Run(Path.Combine(AppContext.BaseDirectory,name));
            Assert.Equal(result.Accepted.Count,report.Rows.Sum(r=>r.Transactions));
            Assert.Equal(report.Transactions,report.Reasons.Values.Sum(r=>r.Transactions));
        }
    }
}
