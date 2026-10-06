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
    // All rows from one file share its ordinal header map.
    private sealed record CsvRow(long Number, Dictionary<string, int> Header, string[] Raw)
    {
        public string this[string name] => Raw[Header[name]];
        public string? Optional(string name) => Header.TryGetValue(name, out var column) ? Raw[column] : null;
    }
    public static ImportResult LoadDirectory(string directory, IReadOnlyDictionary<string, OneMapSearch>? oneMapSearches = null, string? buildingEvidencePath = null, IReadOnlyDictionary<string, HistoricalPostalAssertion>? historicalAssertions = null, Action<string>? stage = null, bool indexed = true, bool referenceCsv = false, bool supportMultiPolygon = true)
    {
        var rejected = new List<RejectedRecord>();
        var diagnostics = new List<ImportDiagnostic>();
        var expandRoadAliases = false;
        var normalizationPath = Path.Combine(directory, "address-normalization.txt");
        if (File.Exists(normalizationPath))
        {
            try
            {
                if (File.ReadAllText(normalizationPath) == "terminal-road-types-v1\n") expandRoadAliases = true;
                else diagnostics.Add(new(Path.GetFileName(normalizationPath), 0, "Unknown address normalization profile; no expanded aliases used."));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { diagnostics.Add(new(Path.GetFileName(normalizationPath), 0, e.Message)); }
        }
        var historicalPath = Path.Combine(directory, "historical-postal-evidence.json");
        if (historicalAssertions is null && File.Exists(historicalPath))
        {
            try { historicalAssertions = HistoricalOneMap.ReadApprovedProjection(historicalPath); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            { diagnostics.Add(new(Path.GetFileName(historicalPath), 0, e.Message)); }
        }
        var properties = new List<PropertyAddress>();
        foreach (var row in Read(Path.Combine(directory, "address-evidence.csv"),
            ["source_row", "blk_no", "street"], rejected, diagnostics, referenceCsv))
        {
            var f = row;
            if (!long.TryParse(f["source_row"], out var sourceRow) || sourceRow < 2 ||
                string.IsNullOrWhiteSpace(f["blk_no"]) || string.IsNullOrWhiteSpace(f["street"]))
                Reject("address-evidence.csv", row, "Property source row, block and street are required.", rejected, diagnostics);
            else properties.Add(new(sourceRow, f["blk_no"], f["street"]));
        }
        stage?.Invoke("property-evidence");
        var postalAddresses = new List<PostalAddress>();
        foreach (var row in Read(Path.Combine(directory, "postal-address-evidence.csv"),
            ["source_row", "block", "street_name", "postal_code"], rejected, diagnostics, referenceCsv))
        {
            var f = row;
            if (!long.TryParse(f["source_row"], out var sourceRow) || sourceRow < 2 ||
                string.IsNullOrWhiteSpace(f["block"]) || string.IsNullOrWhiteSpace(f["street_name"]) ||
                (!f.Header.ContainsKey("source_dataset") && !PostalCode(f["postal_code"])))
                Reject("postal-address-evidence.csv", row, "Postal source row, block, street and six-digit postal code are required.", rejected, diagnostics);
            else if (f.Header.TryGetValue("source_dataset", out var datasetColumn) &&
                !ValidDataset(f.Raw[datasetColumn]))
                Reject("postal-address-evidence.csv", row, "Source dataset must be a data.gov.sg dataset ID.", rejected, diagnostics);
            else
            {
                postalAddresses.Add(new(sourceRow, f["block"], f["street_name"], f["postal_code"])
                { SourceDataset = f.Header.TryGetValue("source_dataset", out var column) ? f.Raw[column] : null });
                if (!PostalCode(f["postal_code"])) diagnostics.Add(new("postal-address-evidence.csv", row.Number,
                    "Malformed public postal assertion retained as unresolved evidence; not repaired or silently discarded."));
            }
        }
        stage?.Invoke("postal-evidence");
        var footprints = ReadFootprints(buildingEvidencePath ?? Path.Combine(directory, "building-evidence.geojson"), rejected, diagnostics, stage, supportMultiPolygon);
        stage?.Invoke("footprints");
        var propertyIndex = properties.ToLookup(p => (AddressNormalizer.Block(p.Block), AddressNormalizer.Street(p.Street, expandRoadAliases)));
        var postalIndex = postalAddresses.ToLookup(p => (AddressNormalizer.Block(p.Block), AddressNormalizer.Street(p.Street, expandRoadAliases)));
        var footprintIndex = footprints.ToLookup(p => AddressNormalizer.Block(p.Identity.Block));
        var matches = new Dictionary<(string Town, string Block, string Street), AddressMatch>();
        stage?.Invoke("evidence-index");
        var accepted = new List<ResaleTransaction>();
        var strings = new ExactStrings();
        var ids = new HashSet<(string? Source, int Row)>();
        var transactionRows = TransactionRows(directory, rejected, diagnostics, referenceCsv);
        stage?.Invoke("transaction-csv");
        var parsed = new List<(string Id, TransactionProvenance? Provenance, TransactionFacts Facts)>();
        foreach (var (file, source, row) in transactionRows)
        {
            var f = row;
            string? error = null;
            if (!YearMonth.TryParse(f["month"], out var month)) error = "Month must be a valid yyyy-MM.";
            else if (!decimal.TryParse(f["resale_price"], NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var price) || price <= 0) error = "Price must be a positive decimal.";
            else if (string.IsNullOrWhiteSpace(f["town"]) || string.IsNullOrWhiteSpace(f["flat_type"]) ||
                string.IsNullOrWhiteSpace(f["block"]) || string.IsNullOrWhiteSpace(f["street_name"]))
                error = "Town, flat type, block and street are required.";
            else if (!int.TryParse(f["source_row"], out var sourceRow) || sourceRow < 2 || !ids.Add((source?.SourceIdentity, sourceRow)))
                error = "Source row must be unique and at least 2.";
            else
            {
                var facts = new TransactionFacts(month!, strings.Share(f["town"]), strings.Share(f["block"]),
                    strings.Share(f["street_name"]), strings.Share(f["flat_type"]), price)
                {
                    StoreyRange = strings.ShareOptional(f.Optional("storey_range")),
                    FloorAreaSqmSource = strings.ShareOptional(f.Optional("floor_area_sqm")),
                    FloorAreaSqm = PositiveDecimal(f.Optional("floor_area_sqm")),
                    FlatModel = strings.ShareOptional(f.Optional("flat_model")),
                    LeaseCommenceDateSource = strings.ShareOptional(f.Optional("lease_commence_date")),
                    LeaseCommenceYear = ValidYear(f.Optional("lease_commence_date")),
                    RemainingLeaseSource = strings.ShareOptional(f.Optional("remaining_lease")),
                    RemainingLeaseMonths = LeaseMonths(f.Optional("remaining_lease"))
                };
                parsed.Add((source is null ? "HDB-" + sourceRow : $"HDB-{source.SourceIdentity}-{sourceRow}",
                    source is null ? null : new(source.SourceIdentity, source.RawSha256, sourceRow), facts));
            }
            if (error is not null) Reject(file, row, error, rejected, diagnostics);
        }
        stage?.Invoke("validation-facts");
        var resolved = new List<(string Id, TransactionProvenance? Provenance, TransactionFacts Facts, DerivedLocation Location, AddressMatch Match)>();
        foreach (var (id, provenance, facts) in parsed)
        {
            var key = (facts.Town, facts.Block, facts.Street);
            if (!indexed || !matches.TryGetValue(key, out var match))
            {
                OneMapSearch? search = null;
                oneMapSearches?.TryGetValue(OneMapEvidence.AddressKey(facts.Block,facts.Street), out search);
                HistoricalPostalAssertion? historical = null;
                historicalAssertions?.TryGetValue(HistoricalOneMap.Key(facts),out historical);
                var addressKey = (AddressNormalizer.Block(facts.Block), AddressNormalizer.Street(facts.Street, expandRoadAliases));
                match = AddressMatcher.Match(facts,
                    indexed ? propertyIndex[addressKey].ToArray() : properties,
                    indexed ? postalIndex[addressKey].ToArray() : postalAddresses,
                    indexed ? footprintIndex[addressKey.Item1].ToArray() : footprints, search, historical, expandRoadAliases);
                if (indexed) matches.Add(key, match);
            }
            var location = match.MatchedFootprint?.Location ?? new DerivedLocation(null, CoordinateQuality.Missing, match.Reason);
            resolved.Add((id, provenance, facts, location, match));
        }
        stage?.Invoke("matching-resolution");
        foreach (var row in resolved) accepted.Add(new(row.Id, row.Facts, row.Location, row.Match) { Provenance = row.Provenance });
        stage?.Invoke("transaction-domain");
        return new(accepted.AsReadOnly(), rejected.AsReadOnly(), diagnostics.AsReadOnly());
    }
    // Per-import, ordinal and bounded: facts keep the exact source text, with
    // no normalized spellings and no process-global string.Intern lifetime.
    private sealed class ExactStrings
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        public string? ShareOptional(string? value) => value is null ? null : Share(value);
        public string Share(string value)
        {
            if (value.Length > 128) return value;
            if (values.TryGetValue(value, out var existing)) return existing;
            if (values.Count < 8192) values.Add(value, value);
            return value;
        }
    }
    private static decimal? PositiveDecimal(string? value) => decimal.TryParse(value,
        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null;
    private static int? ValidYear(string? value) => int.TryParse(value,
        NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1 and <= 9999 ? year : null;
    private static int? LeaseMonths(string? value)
    {
        if (value is null) return null;
        // HDB's 2015–2016 source reports whole years at the resale application.
        // Conversion supplies a reported value, not extra month precision.
        if (value.Length is >= 1 and <= 3 && value.All(char.IsAsciiDigit) &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var wholeYears))
            return wholeYears * 12;
        // Source lease is independent evidence; commencement year is not a
        // substitute. Retain malformed text without rejecting its transaction.
        var match = System.Text.RegularExpressions.Regex.Match(value,
            @"\A([0-9]{1,3}) years?(?: ([0-9]{1,2}) months?)?\z",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var years = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var months = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return months <= 11 ? years * 12 + months : null;
    }
    private static bool ValidDataset(string value) => value.Length == 34 && value.StartsWith("d_", StringComparison.Ordinal) &&
        value.AsSpan(2).IndexOfAnyExcept("0123456789abcdef") < 0;
    private static bool PostalCode(string value) => value.Length == 6 && value.All(char.IsAsciiDigit);
    private static IReadOnlyList<FootprintRecord> ReadFootprints(string path,
        List<RejectedRecord> rejected, List<ImportDiagnostic> diagnostics, Action<string>? stage, bool supportMultiPolygon)
    {
        var result = new List<FootprintRecord>();
        var file = Path.GetFileName(path);
        try
        {
            var bytes = File.ReadAllBytes(path);
            // JsonDocument can borrow UTF-8 bytes without the former pair of
            // full-size UTF-16 strings or a pooled UTF-8 copy. Retain the original
            // StreamReader decoding semantics for other BOMs/invalid UTF-8.
            var textFallback = bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe }) ||
                bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff }) ||
                bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff });
            if (!textFallback)
            {
                try { _ = new System.Text.UTF8Encoding(false, true).GetCharCount(bytes); }
                catch (System.Text.DecoderFallbackException) { textFallback = true; }
            }
            string? text = null;
            if (textFallback)
            {
                using var reader = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                text = reader.ReadToEnd();
            }
            stage?.Invoke("footprint-file-read");
            var utf8 = bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0);
            using var document = text is null ? JsonDocument.Parse(utf8) : JsonDocument.Parse(text);
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
                    if (geometry.ValueKind != JsonValueKind.Null) error = Midpoint(geometry, out point, supportMultiPolygon);
                    if (error is null)
                    {
                        var identity = new FootprintIdentity(objectId, entityId, block, postal);
                        var source = $"HDB Existing Building OBJECTID {objectId}; ENTITYID {entityId}; postal {postal}; " +
                            (point is null ? "no geometry in local evidence." :
                                geometry.GetProperty("type").GetString() == "MultiPolygon"
                                ? "all component exterior-ring union bounding-box midpoint (not exact/interior guaranteed)."
                                : "exterior-ring bounding-box midpoint (not exact/interior guaranteed).");
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
    private static string? Midpoint(JsonElement geometry, out GeoPoint? point, bool supportMultiPolygon)
    {
        point = null;
        if (supportMultiPolygon && geometry.ValueKind == JsonValueKind.Object &&
            Text(geometry, "type", out var geometryType) && geometryType == "MultiPolygon")
        {
            if (!geometry.TryGetProperty("coordinates", out var polygons) || polygons.ValueKind != JsonValueKind.Array || polygons.GetArrayLength() == 0)
                return "Expected MultiPolygon with at least one polygon.";
            Bounds? union = null;
            foreach (var polygon in polygons.EnumerateArray())
            {
                var error = ExteriorBounds(polygon, out var bounds);
                if (error is not null) return "MultiPolygon component: " + error;
                union = union is null ? bounds : new Bounds(Math.Min(union.Value.MinLongitude, bounds.MinLongitude),
                    Math.Max(union.Value.MaxLongitude, bounds.MaxLongitude), Math.Min(union.Value.MinLatitude, bounds.MinLatitude),
                    Math.Max(union.Value.MaxLatitude, bounds.MaxLatitude));
            }
            point = Point(union!.Value);
            return null;
        }
        if (geometry.ValueKind != JsonValueKind.Object || !Text(geometry, "type", out var type) || type != "Polygon" ||
            !geometry.TryGetProperty("coordinates", out var rings) || rings.ValueKind != JsonValueKind.Array ||
            rings.GetArrayLength() == 0 || rings[0].ValueKind != JsonValueKind.Array || rings[0].GetArrayLength() < 4)
            return "Expected Polygon with an exterior ring of at least four positions.";
        var polygonError = ExteriorBounds(rings, out var polygonBounds);
        if (polygonError is null) point = Point(polygonBounds);
        return polygonError;
    }
    private readonly record struct Bounds(double MinLongitude, double MaxLongitude, double MinLatitude, double MaxLatitude);
    private static GeoPoint Point(Bounds b) => new(Math.Round((b.MinLatitude + b.MaxLatitude) / 2, 10),
        Math.Round((b.MinLongitude + b.MaxLongitude) / 2, 10));
    private static string? ExteriorBounds(JsonElement rings, out Bounds bounds)
    {
        bounds = default;
        if (rings.ValueKind != JsonValueKind.Array || rings.GetArrayLength() == 0 ||
            rings[0].ValueKind != JsonValueKind.Array || rings[0].GetArrayLength() < 4)
            return "Expected Polygon with an exterior ring of at least four positions.";
        (double Longitude, double Latitude) first = default, last = default;
        double minLongitude = 0, maxLongitude = 0, minLatitude = 0, maxLatitude = 0;
        var index = 0;
        foreach (var position in rings[0].EnumerateArray())
        {
            if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() < 2 ||
                position[0].ValueKind != JsonValueKind.Number || position[1].ValueKind != JsonValueKind.Number ||
                !position[0].TryGetDouble(out var lon) || !position[1].TryGetDouble(out var lat) ||
                !double.IsFinite(lon) || !double.IsFinite(lat) || lon is < -180 or > 180 || lat is < -90 or > 90)
                return "Polygon positions must contain finite longitude [-180,180] / latitude [-90,90].";
            if (index++ == 0)
            {
                first = (lon, lat);
                minLongitude = maxLongitude = lon;
                minLatitude = maxLatitude = lat;
            }
            else
            {
                if (lon < minLongitude) minLongitude = lon;
                if (lon > maxLongitude) maxLongitude = lon;
                if (lat < minLatitude) minLatitude = lat;
                if (lat > maxLatitude) maxLatitude = lat;
            }
            last = (lon, lat);
        }
        if (first != last) return "Polygon exterior ring must be closed.";
        bounds = new(minLongitude, maxLongitude, minLatitude, maxLatitude);
        return null;
    }
    private static void Reject(string file, CsvRow row, string error, List<RejectedRecord> rejected,
        List<ImportDiagnostic> diagnostics)
    {
        rejected.Add(new(file, row.Number, Array.AsReadOnly(row.Raw), error));
        diagnostics.Add(new(file, row.Number, error));
    }
    private static IReadOnlyList<(string File, TransactionSource? Source, CsvRow Row)> TransactionRows(string directory,
        List<RejectedRecord> rejected, List<ImportDiagnostic> diagnostics, bool referenceCsv)
    {
        string[] required = ["source_row", "month", "town", "flat_type", "block", "street_name", "resale_price"];
        var manifestPath = Path.Combine(directory, TransactionSources.ManifestName);
        if (!Path.Exists(manifestPath) && new FileInfo(manifestPath).LinkTarget is null)
            return Read(Path.Combine(directory, "transactions.csv"), required, rejected, diagnostics, referenceCsv)
                .Select(row => ("transactions.csv", (TransactionSource?)null, row)).ToArray();
        var rows = new List<(string, TransactionSource?, CsvRow)>();
        try
        {
            foreach (var source in TransactionSources.Read(directory))
            {
                using var input = new FileStream(Path.Combine(directory, source.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
                TransactionSources.Verify(input, source);
                var rejectedStart = rejected.Count;
                var diagnosticStart = diagnostics.Count;
                var parsed = Read(source.Path, required, rejected, diagnostics, referenceCsv, input, source.Path);
                if (diagnostics.Count != diagnosticStart || rejected.Count != rejectedStart || parsed.Count != source.Records ||
                    (parsed.Count > 0 && !parsed[0].Header.OrderBy(column => column.Value).Select(column => column.Key).SequenceEqual(source.Columns)))
                    throw new InvalidDataException($"Transaction source schema/count failed: {source.Path}.");
                rows.AddRange(parsed.Select(row => (source.Path, (TransactionSource?)source, row)));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            diagnostics.Add(new(TransactionSources.ManifestName, 0, e.Message));
            rows.Clear(); // An invalid pack never exposes a partial transaction cohort.
        }
        return rows;
    }
    private static IReadOnlyList<CsvRow> Read(string path, string[] required,
        List<RejectedRecord> rejected, List<ImportDiagnostic> diagnostics, bool referenceCsv,
        FileStream? verifiedInput = null, string? displayName = null)
    {
        var rows = new List<CsvRow>();
        var file = displayName ?? Path.GetFileName(path);
        try
        {
            // Hold one stream throughout detection and fallback. Every line is
            // checked before fast-path acceptance, so a quote can never slip
            // through a stale pre-scan decision. Fallback discards provisional
            // parse results; an I/O failure still preserves preceding readable rows.
            using var ownedInput = verifiedInput is null ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
            var input = verifiedInput ?? ownedInput!;
            var rejectedStart = rejected.Count;
            var diagnosticsStart = diagnostics.Count;
            if (!referenceCsv && TryReadPlain(input, file, required, rejected, diagnostics, out rows)) return rows;
            rows.Clear();
            rejected.RemoveRange(rejectedStart, rejected.Count - rejectedStart);
            diagnostics.RemoveRange(diagnosticsStart, diagnostics.Count - diagnosticsStart);
            input.Position = 0;
            using var parser = new TextFieldParser(input, System.Text.Encoding.UTF8, detectEncoding: true, leaveOpen: true)
                { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
            parser.SetDelimiters(",");
            var header = parser.ReadFields();
            if (!ValidHeader(header, required))
            {
                diagnostics.Add(new(file, 1, "Missing or duplicate required CSV header."));
                return rows;
            }
            var columns = Columns(header!);
            while (!parser.EndOfData)
            {
                var number = parser.LineNumber;
                try
                {
                    var values = parser.ReadFields();
                    if (values is null) break;
                    if (values.Length != header!.Length)
                    {
                        Reject(file, new(number, columns, values), "Field count differs from header.", rejected, diagnostics);
                        continue;
                    }
                    rows.Add(new(number, columns, values));
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
    private static bool ValidHeader(string[]? header, string[] required) => header is not null &&
        header.Distinct(StringComparer.Ordinal).Count() == header.Length && required.All(k => header.Contains(k, StringComparer.Ordinal));
    private static Dictionary<string, int> Columns(string[] header) => header.Select((name, index) => (name, index))
        .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);
    private static bool TryReadPlain(Stream input, string file, string[] required,
        List<RejectedRecord> rejected, List<ImportDiagnostic> diagnostics, out List<CsvRow> rows)
    {
        rows = [];
        using var reader = new StreamReader(input, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096, leaveOpen: true);
        Dictionary<string, int>? columns = null;
        var width = 0;
        long nextLine = 1, number = 1;
        while (reader.ReadLine() is { } line)
        {
            nextLine++;
            // Quoted fields, multiline quoting, malformed recovery and other
            // BOM encodings retain the existing TextFieldParser implementation.
            if (reader.CurrentEncoding.CodePage != System.Text.Encoding.UTF8.CodePage || line.Length > 65_536 || line.Contains('"')) return false;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = line.Split(',');
            if (columns is null)
            {
                if (!ValidHeader(values, required))
                {
                    diagnostics.Add(new(file, 1, "Missing or duplicate required CSV header."));
                    return true;
                }
                columns = Columns(values);
                width = values.Length;
            }
            else if (values.Length != width)
                Reject(file, new(number, columns, values), "Field count differs from header.", rejected, diagnostics);
            else rows.Add(new(number, columns, values));
            // Like TextFieldParser, capture the next record's diagnostic line
            // before skipping blanks. Source identity remains source_row.
            number = nextLine;
        }
        if (columns is null) diagnostics.Add(new(file, 1, "Missing or duplicate required CSV header."));
        return true;
    }
}
