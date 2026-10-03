namespace HdbResale.Domain;

public sealed record AddressCoverageRow(string Town, string Block, string Street, int Transactions,
    IReadOnlyList<string> Ids, MatchQuality Quality, CoordinateQuality Coordinates, CoverageReason Reason,
    DerivedLocation Location, AddressMatch Evidence);
public sealed record AddressCoverageCount(int Addresses, int Transactions);
public sealed record AddressCoverageReport(int Transactions, int Addresses, int Rejected,
    IReadOnlyList<ImportDiagnostic> Diagnostics, IReadOnlyDictionary<string, AddressCoverageCount> Reasons,
    IReadOnlyList<AddressCoverageRow> Rows);

/// <summary>Complete deterministic address inventory, using actual importer decisions.</summary>
public static class AddressCoverageStudy
{
    public static AddressCoverageReport Run(string directory, bool supportMultiPolygon = true)
    {
        var imported = CsvImport.LoadDirectory(directory, supportMultiPolygon: supportMultiPolygon);
        var rows = imported.Accepted.GroupBy(t => (t.Town, t.Facts.Block, t.Facts.Street))
            .OrderBy(g => g.Key.Town, StringComparer.Ordinal).ThenBy(g => g.Key.Block, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Street, StringComparer.Ordinal).Select(g =>
            {
                var first = g.First();
                if (g.Any(t => t.Match != first.Match || t.Location != first.Location))
                    throw new InvalidDataException("Raw address has inconsistent outcomes.");
                return new AddressCoverageRow(g.Key.Town, g.Key.Block, g.Key.Street, g.Count(),
                    g.Select(t => t.Id).ToArray(), first.Match.Quality, first.Location.Quality,
                    CoverageStudy.Reason(first), first.Location, first.Match);
            }).ToArray();
        return new(imported.Accepted.Count, rows.Length, imported.Rejected.Count, imported.Diagnostics,
            rows.GroupBy(r => r.Reason).ToDictionary(g => g.Key.ToString(),
                g => new AddressCoverageCount(g.Count(), g.Sum(r => r.Transactions))), rows);
    }
}
