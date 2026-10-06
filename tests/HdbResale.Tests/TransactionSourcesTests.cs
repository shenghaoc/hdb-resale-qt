using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HdbResale.App;
using HdbResale.Domain;
using Xunit;

namespace HdbResale.Tests;

public sealed class TransactionSourcesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-sources-" + Guid.NewGuid());
    private const string Header = "source_row,month,town,flat_type,block,street_name,resale_price";
    public TransactionSourcesTests()
    {
        Directory.CreateDirectory(Path.Combine(directory, "transactions"));
        File.WriteAllText(Path.Combine(directory, "address-evidence.csv"), "source_row,blk_no,street\n2,1,ST\n");
        File.WriteAllText(Path.Combine(directory, "postal-address-evidence.csv"), "source_row,block,street_name,postal_code\n2,1,ST,123456\n");
        File.WriteAllText(Path.Combine(directory, "building-evidence.geojson"), """
            {"type":"FeatureCollection","features":[{"properties":{"OBJECTID":42,"ENTITYID":77,"BLK_NO":"1","POSTAL_COD":"123456"},"geometry":{"type":"Polygon","coordinates":[[[103.7,1.2],[103.9,1.2],[103.9,1.4],[103.7,1.2]]]}}]}
            """);
    }
    public void Dispose() => Directory.Delete(directory, true);
    private JsonObject Source(string identity, string csv, int records = 1)
    {
        var bytes = Encoding.UTF8.GetBytes(csv);
        File.WriteAllBytes(Path.Combine(directory, "transactions", identity + ".csv"), bytes);
        return new JsonObject { ["sourceIdentity"] = identity, ["path"] = "transactions/" + identity + ".csv",
            ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes)), ["bytes"] = bytes.Length,
            ["rawSha256"] = new string('a', 64), ["rawBytes"] = bytes.Length, ["records"] = records,
            ["columns"] = new JsonArray(csv.Split('\n')[0].Split(',').Select(c => (JsonNode?)JsonValue.Create(c)).ToArray()) };
    }
    private void Manifest(params JsonObject[] sources) => File.WriteAllText(Path.Combine(directory, "transaction-sources.json"),
        new JsonObject { ["schemaVersion"] = "hdb-transaction-sources-v1", ["sources"] = new JsonArray(sources.Cast<JsonNode?>().ToArray()) }.ToJsonString());
    private ImportResult Load()
    {
        var result = CsvImport.LoadDirectory(directory);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(CsvImport.LoadDirectory(directory, referenceCsv: true)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(CsvImport.LoadDirectory(directory, indexed: false)));
        return result;
    }

    [Fact]
    public void OrderedSourcesPreservePresenceRawFactsDuplicateOccurrencesAndLocationEvidence()
    {
        Manifest(Source("older", Header + "\n2,2014-01,T,3 ROOM,1,ST,400000\n3,2014-01,T,3 ROOM,1,ST,400000\n", 2),
            Source("integer", Header + ",remaining_lease\n2,2015-01,T,3 ROOM,1,ST,400000,070\n"),
            Source("blank", Header + ",remaining_lease\n2,2016-01,T,3 ROOM,1,ST,400000,\n"),
            Source("modern", Header + ",remaining_lease\n2,2017-01,T,3 ROOM,1,ST,400000,68 years 05 months\n"));
        var result = Load();
        Assert.Empty(result.Rejected); Assert.Empty(result.Diagnostics);
        Assert.Equal(new[] { "HDB-older-2", "HDB-older-3", "HDB-integer-2", "HDB-blank-2", "HDB-modern-2" }, result.Accepted.Select(t => t.Id));
        Assert.Equal(new string?[] { null, null, "070", "", "68 years 05 months" }, result.Accepted.Select(t => t.Facts.RemainingLeaseSource));
        Assert.Equal(new int?[] { null, null, 840, null, 821 }, result.Accepted.Select(t => t.Facts.RemainingLeaseMonths));
        Assert.Equal(result.Accepted[0].Facts, result.Accepted[1].Facts);
        Assert.All(result.Accepted, t => { Assert.Equal(MatchQuality.ExactAddress, t.Match.Quality); Assert.Equal(CoordinateQuality.BlockApproximation, t.Location.Quality); });
        Assert.All(result.Accepted, t => Assert.Equal(new string('a', 64), t.Provenance!.RawSha256));
        var state = new ExplorerState(result.Accepted);
        state.SelectAddress("T|1|ST");
        Assert.Equal(5, state.SelectedAddress!.Count);
        Assert.Single(state.MappedAddresses);
        Assert.Equal(2, state.SelectedAddress.LeaseEstimateCount);
        Assert.Contains("070 years (reported in whole years)", BuyerPresentation.RecentJson(state));
        Assert.Contains("Source remaining lease at resale application", BuyerPresentation.RecentJson(state));
        Assert.Contains("Source integer, row 2; SHA-256", BuyerPresentation.RecentJson(state));
        Assert.Contains("approximately 68y 0m–68y 5m", BuyerPresentation.Lease(state));
        state.Filter("T", "3 ROOM", 0, 1_000_000, 12);
        Assert.Equal("HDB-modern-2", Assert.Single(state.Visible).Id);
    }

    [Fact]
    public void QuotedMultilineAndWhitespaceFactsStillUseTheExistingParser()
    {
        Manifest(Source("quoted", Header + ",flat_model\n2,2024-01,\" T \",3 ROOM,1,ST,400000,\"Model,\nA\"\n"));
        var row = Assert.Single(Load().Accepted);
        Assert.Equal(" T ", row.Facts.Town);
        Assert.Equal("Model,\nA", row.Facts.FlatModel);
    }

    [Fact]
    public void DuplicateSourceLocalRowIsRejectedButSameRowInAnotherSourceSurvives()
    {
        Manifest(Source("a", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n002,2024-01,T,3 ROOM,1,ST,400000\n", 2),
            Source("b", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n"));
        var result = Load();
        Assert.Equal(2, result.Accepted.Count);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal("transactions/a.csv", rejected.File);
        Assert.Equal("002", rejected.Fields[0]);
        Assert.Equal("Source row must be unique and at least 2.", rejected.Reason);
    }

    [Fact]
    public void DuplicateSourceIdentitiesAndAmbiguousLegacyInputFailClosed()
    {
        var source = Source("a", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n");
        Manifest(source, (JsonObject)source.DeepClone());
        Assert.Empty(Load().Accepted);
        Manifest((JsonObject)source.DeepClone());
        File.WriteAllText(Path.Combine(directory, "transactions.csv"), Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n");
        var result = Load();
        Assert.Empty(result.Accepted);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("both are present"));
    }

    [Theory]
    [InlineData("path", "../outside.csv")]
    [InlineData("path", "/tmp/outside.csv")]
    [InlineData("path", "transactions\\a.csv")]
    [InlineData("path", "transactions/%2e%2e/a.csv")]
    [InlineData("sha256", "bad")]
    [InlineData("sha256", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("sourceIdentity", "a/b")]
    [InlineData("rawSha256", "BAD")]
    public void InvalidDescriptorOrCorruptLaterSourceCannotExposeAnEarlierCohort(string property, string value)
    {
        var a = Source("first", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n");
        var b = Source("a", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n");
        b[property] = value;
        Manifest(a, b);
        var result = Load();
        Assert.Empty(result.Accepted);
        Assert.Contains(result.Diagnostics, d => d.File == "transaction-sources.json");
    }

    [Theory]
    [InlineData("bytes", 1)]
    [InlineData("bytes", 268435457)]
    [InlineData("records", 2)]
    [InlineData("records", 0)]
    public void LengthAndRecordCountAreVerified(string property, long value)
    {
        var source = Source("a", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n");
        source[property] = value;
        Manifest(source);
        Assert.Empty(Load().Accepted);
    }

    [Fact]
    public void UnknownSchemaColumnsMissingFilesAndSymlinksFailClosed()
    {
        var source = Source("a", Header + "\n2,2024-01,T,3 ROOM,1,ST,400000\n");
        source["columns"]![1] = "unexpected";
        Manifest(source);
        Assert.Empty(Load().Accepted);
        source["columns"]![1] = "month";
        Manifest((JsonObject)source.DeepClone());
        var manifestPath = Path.Combine(directory, "transaction-sources.json");
        var json = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        json["schemaVersion"] = "future";
        File.WriteAllText(manifestPath, json.ToJsonString());
        Assert.Empty(Load().Accepted);
        Manifest((JsonObject)source.DeepClone());
        var csv = Path.Combine(directory, "transactions/a.csv");
        File.Move(csv, csv + ".original");
        Assert.Empty(Load().Accepted);
        File.CreateSymbolicLink(csv, csv + ".original");
        Assert.Empty(Load().Accepted);
    }
}
