using System.Text;
using System.Text.Json;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

public sealed class StartupAllocationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-allocation-contract-" + Guid.NewGuid());
    public StartupAllocationTests()
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "data")))
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
    }
    public void Dispose() => Directory.Delete(directory, true);

    [Fact]
    public void ExactStringsAreSharedOnlyWithinOneImportAndNeverNormalized()
    {
        File.WriteAllText(Path.Combine(directory, "transactions.csv"),
            "source_row,month,town,flat_type,block,street_name,resale_price\n" +
            "2,2024-01,Town,3 ROOM,1,Street,100\n" +
            "3,2024-02,Town,3 ROOM,1,Street,200\n" +
            "4,2024-02,TOWN,3 room, 1 ,STREET,300\n");
        var first = CsvImport.LoadDirectory(directory).Accepted;
        var second = CsvImport.LoadDirectory(directory).Accepted;
        Assert.Same(first[0].Town, first[1].Town);
        Assert.Same(first[0].FlatType, first[1].FlatType);
        Assert.Same(first[0].Facts.Block, first[1].Facts.Block);
        Assert.Same(first[0].Facts.Street, first[1].Facts.Street);
        Assert.NotSame(first[0].Town, second[0].Town);
        Assert.Equal("TOWN", first[2].Town);
        Assert.Equal("3 room", first[2].FlatType);
        Assert.Equal(" 1 ", first[2].Facts.Block);
        Assert.Equal("STREET", first[2].Facts.Street);
    }

    [Fact]
    public void SharingIsBoundedByEntryCountAndStringLengthWithoutLosingFacts()
    {
        using (var writer = new StreamWriter(Path.Combine(directory, "transactions.csv")))
        {
            writer.WriteLine("source_row,month,town,flat_type,block,street_name,resale_price");
            for (var i = 0; i < 8300; i++) writer.WriteLine($"{i + 2},2024-01,Town-{i},3 ROOM,1,Street,100");
            writer.WriteLine("8302,2024-01,Town-0,3 ROOM,1,Street,100");
            writer.WriteLine("8303,2024-01,Town-8299,3 ROOM,1,Street,100");
            writer.WriteLine($"8304,2024-01,{new string('X', 129)},3 ROOM,1,Street,100");
            writer.WriteLine($"8305,2024-01,{new string('X', 129)},3 ROOM,1,Street,100");
        }
        var result = CsvImport.LoadDirectory(directory);
        Assert.Equal(8304, result.Accepted.Count);
        Assert.Empty(result.Rejected);
        var rows = result.Accepted;
        Assert.Same(rows[0].Town, rows[8300].Town);
        Assert.Equal(rows[8299].Town, rows[8301].Town);
        Assert.NotSame(rows[8299].Town, rows[8301].Town);
        Assert.Equal(rows[8302].Town, rows[8303].Town);
        Assert.NotSame(rows[8302].Town, rows[8303].Town);
    }

    [Fact]
    public void FootprintBomEncodingsPreserveEveryImportedField()
    {
        var path = Path.Combine(directory, "building-evidence.geojson");
        var text = File.ReadAllText(path);
        var expected = JsonSerializer.Serialize(CsvImport.LoadDirectory(directory));
        foreach (var encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode,
            Encoding.BigEndianUnicode, Encoding.UTF32, new UTF32Encoding(true, true) })
        {
            File.WriteAllText(path, text, encoding);
            Assert.Equal(expected, JsonSerializer.Serialize(CsvImport.LoadDirectory(directory)));
        }
    }

    [Fact]
    public void InvalidUtf8FootprintTextKeepsStreamReaderReplacementBehavior()
    {
        // An ignored property still must not make otherwise readable features fail.
        var path = Path.Combine(directory, "building-evidence.geojson");
        var text = File.ReadAllText(path).Replace("\"FeatureCollection\"", "\"FeatureCollection\",\"ignored\":\"INVALID-BYTE\"");
        var expected = JsonSerializer.Serialize(CsvImport.LoadDirectory(directory));
        var bytes = Encoding.UTF8.GetBytes(text);
        var start = text.IndexOf("INVALID-BYTE", StringComparison.Ordinal);
        bytes[start] = 0xff;
        File.WriteAllBytes(path, bytes);
        Assert.Equal(expected, JsonSerializer.Serialize(CsvImport.LoadDirectory(directory)));
    }
}
