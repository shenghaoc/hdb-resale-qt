using System.Globalization;
using System.Text;
using System.Text.Json;
using HdbResale.Domain;
using Xunit;

namespace HdbResale.Tests;

public sealed class StartupImportContractTests : IDisposable
{
    private const string Header = "source_row,month,town,flat_type,block,street_name,resale_price\n";
    private const string PropertyHeader = "source_row,blk_no,street\n";
    private const string PostalHeader = "source_row,block,street_name,postal_code\n";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "hdb-startup-contract-" + Guid.NewGuid());

    public StartupImportContractTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);

    private ImportResult Load(string transactions, string properties = PropertyHeader,
        string postals = PostalHeader, string features = "[]")
    {
        File.WriteAllText(Path.Combine(directory, "transactions.csv"), transactions);
        File.WriteAllText(Path.Combine(directory, "address-evidence.csv"), properties);
        File.WriteAllText(Path.Combine(directory, "postal-address-evidence.csv"), postals);
        File.WriteAllText(Path.Combine(directory, "building-evidence.geojson"),
            "{\"type\":\"FeatureCollection\",\"features\":" + features + "}");
        return CsvImport.LoadDirectory(directory);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void QuotedMultilineAndEscapedFieldsPreserveTextAndFollowingDiagnosticLine(string newline)
    {
        var result = Load(Header.Replace("\n", newline) +
            "200,2024-02, T ,3 room, 1 ,\"STREET, \"\"ONE\"\"" + newline + "TWO\",123.45" + newline +
            "901,2024-02,T,3 ROOM,1,ST,0" + newline);

        var transaction = Assert.Single(result.Accepted);
        Assert.Equal("HDB-200", transaction.Id);
        Assert.Equal(" T ", transaction.Town);
        Assert.Equal("3 room", transaction.FlatType);
        Assert.Equal(" 1 ", transaction.Facts.Block);
        Assert.Equal("STREET, \"ONE\"" + newline + "TWO", transaction.Facts.Street);
        Assert.Equal(123.45m, transaction.Price);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(4, rejected.Row);
        Assert.Equal("901", rejected.Fields[0]);
        Assert.Equal(new ImportDiagnostic("transactions.csv", 4, "Price must be a positive decimal."),
            Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void BlankLinesDoNotBecomeTransactionsOrChangeSourceIds()
    {
        var result = Load("\n" + Header + "\n \t\n" +
            "400,2024-01,T,3 ROOM,1,ST,100\n\n" +
            "900,2024-01,T,3 ROOM,1,ST,0\n\n" +
            "700,2024-01,T,3 ROOM,1,ST,200\n\n");
        Assert.Equal(new[] { "HDB-400", "HDB-700" }, result.Accepted.Select(t => t.Id));
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal("900", rejected.Fields[0]);
        Assert.Equal("Price must be a positive decimal.", rejected.Reason);
        Assert.Equal(rejected.Row, Assert.Single(result.Diagnostics).Row);
        // M7 captures parser.LineNumber before ReadFields skips blank lines. Do not
        // freeze that incidental offset as a physical-line-number guarantee.
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PlainRecordsPreserveUnicodeAndTrailingEmptyFieldsWithOrWithoutFinalNewline(string newline)
    {
        const string street = "ST\u0085REET\u2028ONE\u2029TWO";
        var result = Load(Header.TrimEnd('\n') + ",unused" + newline +
            "\u00a0\u1680\u2007\u202f\u3000" + newline +
            "200,2024-01,T,3 ROOM,1," + street + ",100," + newline +
            "201,2024-01,T,3 ROOM,1,ST,100" + newline +
            "202,2024-01,T,3 ROOM,1,ST,200,");
        Assert.Equal(new[] { "HDB-200", "HDB-202" }, result.Accepted.Select(t => t.Id));
        Assert.Equal(street, result.Accepted[0].Facts.Street);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal("201", rejected.Fields[0]);
        Assert.Equal("Field count differs from header.", rejected.Reason);
        Assert.Equal(7, rejected.Fields.Count);
        Assert.Single(result.Diagnostics);
    }

    [Theory]
    [InlineData("\u200b")]
    [InlineData("\ufeff")]
    [InlineData("\u001a")]
    public void NonWhitespaceControlOrFormatOnlyLinesAreNotSilentlySkipped(string line)
    {
        var result = Load(Header + line + "\n200,2024-01,T,3 ROOM,1,ST,100\n");
        Assert.Equal("HDB-200", Assert.Single(result.Accepted).Id);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(2, rejected.Row);
        Assert.Equal(new[] { line }, rejected.Fields);
        Assert.Equal("Field count differs from header.", rejected.Reason);
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("unicodeFFFE")]
    [InlineData("utf-32")]
    [InlineData("utf-32BE")]
    public void BomEncodedCsvPreservesHeaderAndNonAsciiValues(string encodingName)
    {
        Load(Header);
        var encoding = encodingName == "utf-32BE"
            ? new UTF32Encoding(bigEndian: true, byteOrderMark: true)
            : Encoding.GetEncoding(encodingName);
        File.WriteAllText(Path.Combine(directory, "transactions.csv"),
            Header + "200,2024-02,TÖWN,3 ROOM,1,RUE ÉCOLE,123.45\n", encoding);
        var result = CsvImport.LoadDirectory(directory);
        var transaction = Assert.Single(result.Accepted);
        Assert.Equal("TÖWN", transaction.Town);
        Assert.Equal("RUE ÉCOLE", transaction.Facts.Street);
        Assert.Equal(123.45m, transaction.Price);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void LongPlainFieldAndEmbeddedNulRemainSourceText()
    {
        var street = new string('S', 20_000) + "\0END";
        var result = Load(Header + "200,2024-01,T,3 ROOM,1," + street + ",100");
        Assert.Equal(street, Assert.Single(result.Accepted).Facts.Street);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void MalformedRecordRecoveryKeepsReadableRowsAndRawRejectionEvidence()
    {
        const string malformed = "300,2024-01,T,3 ROOM,1,\"bad\"text,100";
        var result = Load(Header + "200,2024-01,T,3 ROOM,1,\"FIRST\nSECOND\",100\n" +
            malformed + "\n900,2024-01,T,3 ROOM,1,ST,200\n");
        Assert.Equal(new[] { "HDB-200", "HDB-900" }, result.Accepted.Select(t => t.Id));
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(4, rejected.Row);
        Assert.Equal(new[] { malformed }, rejected.Fields);
        Assert.Equal("Malformed CSV quoting.", rejected.Reason);
        Assert.Equal(new ImportDiagnostic("transactions.csv", 4, rejected.Reason), Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void UnterminatedQuotedRecordRetainsTheConsumedTailAsMalformedEvidence()
    {
        const string tail = "200,2024-01,T,3 ROOM,1,\"bad,100\n900,2024-01,T,3 ROOM,1,ST,200";
        var result = Load(Header + tail + "\n");
        Assert.Empty(result.Accepted);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(2, rejected.Row);
        Assert.Equal(new[] { tail }, rejected.Fields);
        Assert.Equal("Malformed CSV quoting.", rejected.Reason);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void MixedFailuresRetainEachReasonAndSourceReference()
    {
        var result = Load(Header + "200,2024-01,T,3 ROOM,1,ST,0\n" +
            "300,2024-01,T,3 ROOM,1,\"bad\"text,100\n" +
            "400,2024-01,T,3 ROOM,1,ST,100,extra\n" +
            "900,2024-01,T,3 ROOM,1,ST,200\n");
        Assert.Equal("HDB-900", Assert.Single(result.Accepted).Id);
        Assert.Equal(3, result.Rejected.Count);
        Assert.Equal(3, result.Diagnostics.Count);
        // M7 reports parser failures before semantic failures because it reads the
        // complete CSV first. Presence, reason, and source evidence matter here;
        // no public ordering contract requires retaining that staging artifact.
        Assert.Contains(result.Rejected, r => r.Row == 2 && r.Reason == "Price must be a positive decimal." && r.Fields[0] == "200");
        Assert.Contains(result.Rejected, r => r.Row == 3 && r.Reason == "Malformed CSV quoting." && r.Fields.Count == 1);
        Assert.Contains(result.Rejected, r => r.Row == 4 && r.Reason == "Field count differs from header." && r.Fields[^1] == "extra");
        foreach (var rejection in result.Rejected)
            Assert.Contains(new ImportDiagnostic(rejection.File, rejection.Row, rejection.Reason), result.Diagnostics);
    }

    [Fact]
    public void ReorderedHeadersAndExtraFieldsPreserveExactFactsAndEvidence()
    {
        var result = Load("ignored,street_name,resale_price,block,town,month,source_row,flat_type\n" +
            "\"unused, value\",Test AVE 1,123.45, 1 , T ,2024-02,200,3 room\n",
            "street,extra,source_row,blk_no\nTEST AVENUE 1,ignored,2147483648,1\n",
            "postal_code,street_name,block,source_row,extra\n012345,TEST AVENUE 1,1,2147483649,ignored\n",
            """
            [{"properties":{"OBJECTID":42,"ENTITYID":77,"BLK_NO":"1","POSTAL_COD":"012345"},"geometry":null}]
            """);
        var transaction = Assert.Single(result.Accepted);
        Assert.Equal(" T ", transaction.Town);
        Assert.Equal(" 1 ", transaction.Facts.Block);
        Assert.Equal("Test AVE 1", transaction.Facts.Street);
        Assert.Equal("3 room", transaction.FlatType);
        Assert.Equal(123.45m, transaction.Price);
        Assert.Equal(MatchQuality.NormalizedAddress, transaction.Match.Quality);
        Assert.Equal(2147483648L, Assert.Single(transaction.Match.PropertyCandidates).SourceRow);
        var assertion = Assert.Single(transaction.Match.PostalAssertions);
        Assert.Equal(2147483649L, assertion.SourceRow);
        Assert.Equal("012345", assertion.PostalCode);
        Assert.Null(transaction.Location.Point);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(CsvImport.LoadDirectory(directory, indexed: false)));
    }

    [Theory]
    [InlineData("source_row,month,Town,flat_type,block,street_name,resale_price")]
    [InlineData("source_row,month,town,flat_type,block,street_name")]
    [InlineData("source_row,month,town,flat_type,block,street_name,resale_price,town")]
    [InlineData("source_row,month,town,flat_type,block,street_name,resale_price,extra,extra")]
    public void MissingCaseChangedOrDuplicateHeadersAreDiagnosticOnly(string header)
    {
        var result = Load(header + "\n");
        Assert.Empty(result.Accepted);
        Assert.Empty(result.Rejected);
        Assert.Equal(new ImportDiagnostic("transactions.csv", 1, "Missing or duplicate required CSV header."),
            Assert.Single(result.Diagnostics));
    }

    [Fact]
    public void OnlyValidRowsReserveCanonicalSourceIdsAndValidationPrecedenceIsStable()
    {
        var result = Load(Header + "200,2024-01,T,3 ROOM,1,ST,0\n" +
            "00200,2024-01,T,3 ROOM,1,ST,100\n" +
            "200,2024-13,T,3 ROOM,1,ST,0\n" +
            "200,2024-01, ,3 ROOM,1,ST,100\n" +
            "200,2024-01,T,3 ROOM,1,ST,100\n" +
            "2147483648,2024-01,T,3 ROOM,1,ST,100\n");
        Assert.Equal("HDB-200", Assert.Single(result.Accepted).Id);
        Assert.Equal(5, result.Rejected.Count);
        Assert.Contains(result.Rejected, r => r.Row == 2 && r.Reason.StartsWith("Price", StringComparison.Ordinal));
        Assert.Contains(result.Rejected, r => r.Row == 4 && r.Reason.StartsWith("Month", StringComparison.Ordinal));
        Assert.Contains(result.Rejected, r => r.Row == 5 && r.Reason == "Town, flat type, block and street are required.");
        Assert.Contains(result.Rejected, r => r.Row == 6 && r.Reason == "Source row must be unique and at least 2.");
        Assert.Contains(result.Rejected, r => r.Row == 7 && r.Reason == "Source row must be unique and at least 2.");
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void DecimalAndMonthValidationDoNotDependOnCurrentCulture(string cultureName)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var result = Load(Header + "200,2024-02,T,3 ROOM,1,ST,123.45\n" +
                "201,2024-02,T,3 ROOM,1,ST,\"123,45\"\n" +
                "202,2024-02,T,3 ROOM,1,ST,+123.45\n" +
                "203,2024-02,T,3 ROOM,1,ST, 123.45\n" +
                "204,2024-02,T,3 ROOM,1,ST,123.45 \n" +
                "205,2024-02,T,3 ROOM,1,ST,1e2\n" +
                "206,2024-02,T,3 ROOM,1,ST,.5\n" +
                "207,2024-02,T,3 ROOM,1,ST,1.\n");
            Assert.Equal(new[] { 123.45m, 0.5m, 1m }, result.Accepted.Select(t => t.Price));
            Assert.All(result.Accepted, t => Assert.Equal("2024-02", t.Facts.Month.ToString()));
            Assert.Equal(5, result.Rejected.Count);
            Assert.All(result.Rejected, r => Assert.Equal("Price must be a positive decimal.", r.Reason));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("1,1,ST", "1,1,ST,012345")]
    [InlineData("2, ,ST", "2, ,ST,012345")]
    [InlineData("2,1, \t", "2,1, \t,012345")]
    public void EvidenceRequiredFieldsRemainValidatedIndependentlyOfTransactions(string property, string postal)
    {
        var result = Load(Header + "200,2024-01,T,3 ROOM,1,ST,100\n", PropertyHeader + property + "\n", PostalHeader + postal + "\n");
        Assert.Equal(MatchQuality.Unmatched, Assert.Single(result.Accepted).Match.Quality);
        Assert.Equal(2, result.Rejected.Count);
        Assert.Contains(result.Rejected, r => r.File == "address-evidence.csv" && r.Row == 2 && r.Reason == "Property source row, block and street are required.");
        Assert.Contains(result.Rejected, r => r.File == "postal-address-evidence.csv" && r.Row == 2 && r.Reason == "Postal source row, block, street and six-digit postal code are required.");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData(" 12345")]
    [InlineData("１２３４５６")]
    public void PostalCodeRequiresExactlySixAsciiDigits(string postal)
    {
        var result = Load(Header + "200,2024-01,T,3 ROOM,1,ST,100\n", PropertyHeader + "2,1,ST\n", PostalHeader + $"2,1,ST,{postal}\n");
        Assert.Equal(MatchQuality.Unmatched, Assert.Single(result.Accepted).Match.Quality);
        Assert.Equal("postal-address-evidence.csv", Assert.Single(result.Rejected).File);
        Assert.Single(result.Diagnostics);
    }
}
