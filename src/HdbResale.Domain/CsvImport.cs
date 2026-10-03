using System.Globalization;
using Microsoft.VisualBasic.FileIO;
namespace HdbResale.Domain;

public sealed record ImportDiagnostic(string File, long Row, string Message);
public sealed record RejectedRecord(string File, long Row, IReadOnlyList<string> Fields, string Reason);
public sealed record ImportResult(IReadOnlyList<ResaleTransaction> Accepted,
    IReadOnlyList<RejectedRecord> Rejected, IReadOnlyList<ImportDiagnostic> Diagnostics);

public static class CsvImport
{
    private sealed record CsvRow(long Number, Dictionary<string, string> Fields, string[] Raw);
    public static ImportResult LoadDirectory(string directory)
    {
        var rejected = new List<RejectedRecord>();
        var diagnostics = new List<ImportDiagnostic>();
        var locations = new Dictionary<string, DerivedLocation>(StringComparer.Ordinal);
        foreach (var row in Read(Path.Combine(directory, "locations.csv"),
            ["town", "block", "street_name", "latitude", "longitude", "quality", "source"], rejected, diagnostics))
        {
            var f = row.Fields;
            var key = Key(f);
            string? error = null;
            DerivedLocation? location = null;
            if (string.IsNullOrWhiteSpace(f["town"]) || string.IsNullOrWhiteSpace(f["block"]) ||
                string.IsNullOrWhiteSpace(f["street_name"])) error = "Location address is required.";
            else if (!Enum.TryParse<LocationQuality>(f["quality"], out var quality) || !Enum.IsDefined(quality) || f["quality"] != quality.ToString())
                error = "Unknown location quality.";
            else if (string.IsNullOrWhiteSpace(f["source"])) error = "Location source is required.";
            else if (quality == LocationQuality.Missing)
            {
                if (f["latitude"] != "" || f["longitude"] != "") error = "Missing quality cannot carry coordinates.";
                else location = new(null, quality, f["source"]);
            }
            else if (!double.TryParse(f["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(f["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                !double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
                error = "Coordinates must be finite latitude [-90,90] / longitude [-180,180].";
            else location = new(new(lat, lon), quality, f["source"]);
            if (error is null && !locations.TryAdd(key, location!)) error = "Duplicate location address.";
            if (error is not null) Reject("locations.csv", row, error, rejected, diagnostics);
        }
        var accepted = new List<ResaleTransaction>();
        var ids = new HashSet<int>();
        foreach (var row in Read(Path.Combine(directory, "transactions.csv"),
            ["source_row", "month", "town", "flat_type", "block", "street_name", "resale_price"], rejected, diagnostics))
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
                var location = locations.GetValueOrDefault(Key(f)) ??
                    new DerivedLocation(null, LocationQuality.Missing, "No accepted entry in local location table.");
                accepted.Add(new("HDB-" + sourceRow, new(month!, f["town"], f["block"],
                    f["street_name"], f["flat_type"], price), location));
            }
            if (error is not null) Reject("transactions.csv", row, error, rejected, diagnostics);
        }
        return new(accepted.AsReadOnly(), rejected.AsReadOnly(), diagnostics.AsReadOnly());
    }
    private static string Key(Dictionary<string, string> f) => $"{f["town"]}|{f["block"]}|{f["street_name"]}";
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
