using System.Globalization;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
namespace HdbResale.Domain;

public sealed record ImportDiagnostic(string File, long Row, string Message);
public sealed record RejectedRecord(string File, long Row, IReadOnlyList<string> Fields, string Reason);
public sealed record ImportResult(IReadOnlyList<ResaleTransaction> Accepted,
    IReadOnlyList<RejectedRecord> Rejected, IReadOnlyList<ImportDiagnostic> Diagnostics)
{
    public int MatchedCount => Accepted.Count(t => t.Match.IsMatched);
    public int AmbiguousCount => Accepted.Count(t => t.Match.Quality == MatchQuality.Ambiguous);
    public int UnmatchedCount => Accepted.Count(t => t.Match.Quality == MatchQuality.Unmatched);
}

public static class CsvImport
{
    private sealed record CsvRow(long Number, Dictionary<string, string> Fields, string[] Raw);
    public static ImportResult LoadDirectory(string directory, IReadOnlyDictionary<string, OneMapSearch>? oneMapSearches = null, string? buildingEvidencePath = null, IReadOnlyDictionary<string, HistoricalPostalAssertion>? historicalAssertions = null, Action<string>? stage = null, bool indexed = true)
    {
        var rejected = new List<RejectedRecord>();
        var diagnostics = new List<ImportDiagnostic>();
        var properties = new List<PropertyAddress>();
        foreach (var row in Read(Path.Combine(directory, "address-evidence.csv"),
            ["source_row", "blk_no", "street"], rejected, diagnostics))
        {
            var f = row.Fields;
            if (!long.TryParse(f["source_row"], out var sourceRow) || sourceRow < 2 ||
                string.IsNullOrWhiteSpace(f["blk_no"]) || string.IsNullOrWhiteSpace(f["street"]))
                Reject("address-evidence.csv", row, "Property source row, block and street are required.", rejected, diagnostics);
            else properties.Add(new(sourceRow, f["blk_no"], f["street"]));
        }
        stage?.Invoke("property-evidence");
        var postalAddresses = new List<PostalAddress>();
        foreach (var row in Read(Path.Combine(directory, "postal-address-evidence.csv"),
            ["source_row", "block", "street_name", "postal_code"], rejected, diagnostics))
        {
            var f = row.Fields;
            if (!long.TryParse(f["source_row"], out var sourceRow) || sourceRow < 2 ||
                string.IsNullOrWhiteSpace(f["block"]) || string.IsNullOrWhiteSpace(f["street_name"]) || !PostalCode(f["postal_code"]))
                Reject("postal-address-evidence.csv", row, "Postal source row, block, street and six-digit postal code are required.", rejected, diagnostics);
            else postalAddresses.Add(new(sourceRow, f["block"], f["street_name"], f["postal_code"]));
        }
        stage?.Invoke("postal-evidence");
        var footprints = ReadFootprints(buildingEvidencePath ?? Path.Combine(directory, "building-evidence.geojson"), rejected, diagnostics, stage);
        stage?.Invoke("footprints");
        var propertyIndex = properties.ToLookup(p => (AddressNormalizer.Block(p.Block), AddressNormalizer.Street(p.Street)));
        var postalIndex = postalAddresses.ToLookup(p => (AddressNormalizer.Block(p.Block), AddressNormalizer.Street(p.Street)));
        var footprintIndex = footprints.ToLookup(p => AddressNormalizer.Block(p.Identity.Block));
        var matches = new Dictionary<(string Town, string Block, string Street), AddressMatch>();
        stage?.Invoke("evidence-index");
        var accepted = new List<ResaleTransaction>();
        var ids = new HashSet<int>();
        var transactionRows = Read(Path.Combine(directory, "transactions.csv"),
            ["source_row", "month", "town", "flat_type", "block", "street_name", "resale_price"], rejected, diagnostics);
        stage?.Invoke("transaction-csv");
        var parsed = new List<(int SourceRow, TransactionFacts Facts)>();
        foreach (var row in transactionRows)
        {
            var f = row.Fields;
            string? error = null;
            if (!YearMonth.TryParse(f["month"], out var month)) error = "Month must be a valid yyyy-MM.";
            else if (!decimal.TryParse(f["resale_price"], NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var price) || price <= 0) error = "Price must be a positive decimal.";
            else if (new[] { "town", "flat_type", "block", "street_name" }.Any(k => string.IsNullOrWhiteSpace(f[k])))
                error = "Town, flat type, block and street are required.";
            else if (!int.TryParse(f["source_row"], out var sourceRow) || sourceRow < 2 || !ids.Add(sourceRow))
                error = "Source row must be unique and at least 2.";
            else
            {
                var facts = new TransactionFacts(month!, f["town"], f["block"], f["street_name"], f["flat_type"], price);
                parsed.Add((sourceRow, facts));
            }
            if (error is not null) Reject("transactions.csv", row, error, rejected, diagnostics);
        }
        stage?.Invoke("validation-facts");
        var resolved = new List<(int SourceRow, TransactionFacts Facts, DerivedLocation Location, AddressMatch Match)>();
        foreach (var (sourceRow, facts) in parsed)
        {
            OneMapSearch? search = null;
            oneMapSearches?.TryGetValue(OneMapEvidence.AddressKey(facts.Block,facts.Street), out search);
            HistoricalPostalAssertion? historical = null;
            historicalAssertions?.TryGetValue(HistoricalOneMap.Key(facts),out historical);
            var key = (facts.Town, facts.Block, facts.Street);
            if (!indexed || !matches.TryGetValue(key, out var match))
            {
                var addressKey = (AddressNormalizer.Block(facts.Block), AddressNormalizer.Street(facts.Street));
                match = AddressMatcher.Match(facts,
                    indexed ? propertyIndex[addressKey].ToArray() : properties,
                    indexed ? postalIndex[addressKey].ToArray() : postalAddresses,
                    indexed ? footprintIndex[addressKey.Item1].ToArray() : footprints, search, historical);
                if (indexed) matches.Add(key, match);
            }
            var location = match.MatchedFootprint?.Location ?? new DerivedLocation(null, CoordinateQuality.Missing, match.Reason);
            resolved.Add((sourceRow, facts, location, match));
        }
        stage?.Invoke("matching-resolution");
        foreach (var row in resolved) accepted.Add(new("HDB-" + row.SourceRow, row.Facts, row.Location, row.Match));
        stage?.Invoke("transaction-domain");
        return new(accepted.AsReadOnly(), rejected.AsReadOnly(), diagnostics.AsReadOnly());
    }
    private static bool PostalCode(string value) => value.Length == 6 && value.All(char.IsAsciiDigit);
    private static IReadOnlyList<FootprintRecord> ReadFootprints(string path,
        List<RejectedRecord> rejected, List<ImportDiagnostic> diagnostics, Action<string>? stage)
    {
        var result = new List<FootprintRecord>();
        var file = Path.GetFileName(path);
        try
        {
            var text = File.ReadAllText(path);
            stage?.Invoke("footprint-file-read");
            using var document = JsonDocument.Parse(text);
            stage?.Invoke("footprint-json");
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String || type.GetString() != "FeatureCollection" ||
                !root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(new(file, 0, "Expected GeoJSON FeatureCollection with features array."));
                return result;
            }
            long featureNumber = 0;
            foreach (var feature in features.EnumerateArray())
            {
                featureNumber++;
                string? error = null;
                if (feature.ValueKind != JsonValueKind.Object || !feature.TryGetProperty("properties", out var p) ||
                    p.ValueKind != JsonValueKind.Object || !Integer(p, "OBJECTID", out var objectId) || objectId <= 0 ||
                    !Integer(p, "ENTITYID", out var entityId) || entityId <= 0 ||
                    !Text(p, "BLK_NO", out var block) || string.IsNullOrWhiteSpace(block) ||
                    !Text(p, "POSTAL_COD", out var postal) || !PostalCode(postal))
                    error = "Footprint OBJECTID, ENTITYID, block and six-digit postal code are required.";
                else if (!feature.TryGetProperty("geometry", out var geometry)) error = "Geometry member is required (null explicitly means missing).";
                else
                {
                    GeoPoint? point = null;
                    if (geometry.ValueKind != JsonValueKind.Null) error = Midpoint(geometry, out point);
                    if (error is null)
                    {
                        var identity = new FootprintIdentity(objectId, entityId, block, postal);
                        var source = $"HDB Existing Building OBJECTID {objectId}; ENTITYID {entityId}; postal {postal}; " +
                            (point is null ? "no geometry in local evidence." : "exterior-ring bounding-box midpoint (not exact/interior guaranteed).");
                        result.Add(new(identity, new(point, point is null ? CoordinateQuality.Missing : CoordinateQuality.BlockApproximation, source)));
                    }
                }
                if (error is not null)
                {
                    rejected.Add(new(file, featureNumber, Array.AsReadOnly(new[] { feature.GetRawText() }), error));
                    diagnostics.Add(new(file, featureNumber, error));
                }
            }
        }
        catch (IOException e) { diagnostics.Add(new(file, 0, e.Message)); }
        catch (UnauthorizedAccessException e) { diagnostics.Add(new(file, 0, e.Message)); }
        catch (JsonException e) { diagnostics.Add(new(file, 0, $"Malformed GeoJSON: {e.Message}")); }
        return result.AsReadOnly();
    }
    private static bool Integer(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out value);
    }
    private static bool Text(JsonElement element, string name, out string value)
    {
        value = "";
        if (!element.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String) return false;
        value = p.GetString()!;
        return true;
    }
    private static string? Midpoint(JsonElement geometry, out GeoPoint? point)
    {
        point = null;
        if (geometry.ValueKind != JsonValueKind.Object || !Text(geometry, "type", out var type) || type != "Polygon" ||
            !geometry.TryGetProperty("coordinates", out var rings) || rings.ValueKind != JsonValueKind.Array ||
            rings.GetArrayLength() == 0 || rings[0].ValueKind != JsonValueKind.Array || rings[0].GetArrayLength() < 4)
            return "Expected Polygon with an exterior ring of at least four positions.";
        var positions = new List<(double Longitude, double Latitude)>();
        foreach (var position in rings[0].EnumerateArray())
        {
            if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() < 2 ||
                position[0].ValueKind != JsonValueKind.Number || position[1].ValueKind != JsonValueKind.Number ||
                !position[0].TryGetDouble(out var lon) || !position[1].TryGetDouble(out var lat) ||
                !double.IsFinite(lon) || !double.IsFinite(lat) || lon is < -180 or > 180 || lat is < -90 or > 90)
                return "Polygon positions must contain finite longitude [-180,180] / latitude [-90,90].";
            positions.Add((lon, lat));
        }
        if (positions[0] != positions[^1]) return "Polygon exterior ring must be closed.";
        point = new(Math.Round((positions.Min(p => p.Latitude) + positions.Max(p => p.Latitude)) / 2, 10),
            Math.Round((positions.Min(p => p.Longitude) + positions.Max(p => p.Longitude)) / 2, 10));
        return null;
    }
    private static void Reject(string file, CsvRow row, string error, List<RejectedRecord> rejected,
        List<ImportDiagnostic> diagnostics)
    {
        rejected.Add(new(file, row.Number, Array.AsReadOnly(row.Raw), error));
        diagnostics.Add(new(file, row.Number, error));
    }
    private static IReadOnlyList<CsvRow> Read(string path, string[] required,
        List<RejectedRecord> rejected, List<ImportDiagnostic> diagnostics)
    {
        var rows = new List<CsvRow>();
        var file = Path.GetFileName(path);
        try
        {
            using var parser = new TextFieldParser(path) { TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            parser.SetDelimiters(",");
            var header = parser.ReadFields();
            if (header is null || header.Distinct(StringComparer.Ordinal).Count() != header.Length ||
                required.Any(k => !header.Contains(k, StringComparer.Ordinal)))
            {
                diagnostics.Add(new(file, 1, "Missing or duplicate required CSV header."));
                return rows;
            }
            while (!parser.EndOfData)
            {
                var number = parser.LineNumber;
                try
                {
                    var values = parser.ReadFields();
                    if (values is null) break;
                    if (values.Length != header.Length)
                    {
                        Reject(file, new(number, [], values), "Field count differs from header.", rejected, diagnostics);
                        continue;
                    }
                    rows.Add(new(number, header.Zip(values).ToDictionary(x => x.First, x => x.Second,
                        StringComparer.Ordinal), values));
                }
                catch (MalformedLineException e)
                {
                    rejected.Add(new(file, e.LineNumber, Array.AsReadOnly(new[] { parser.ErrorLine }), "Malformed CSV quoting."));
                    diagnostics.Add(new(file, e.LineNumber, "Malformed CSV quoting."));
                }
            }
        }
        catch (IOException e) { diagnostics.Add(new(file, 0, e.Message)); }
        catch (UnauthorizedAccessException e) { diagnostics.Add(new(file, 0, e.Message)); }
        catch (MalformedLineException e) { diagnostics.Add(new(file, e.LineNumber, "Malformed CSV header.")); }
        return rows;
    }
}
