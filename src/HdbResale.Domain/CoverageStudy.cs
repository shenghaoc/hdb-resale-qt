namespace HdbResale.Domain;

public enum CoverageReason
{
    Matched, MissingProperty, MultipleProperties, MissingPostalCorroboration,
    ConflictingPostals, MissingFootprint, MultipleFootprints, MatchedWithoutGeometry, OneMapCandidateConflict, CrossSourcePostalConflict, InvalidPostalAssertion
}
public sealed record CoverageCell(string Town, string Period, string Match, string Coordinates, int Count);
public sealed record CoverageFailure(string Id, string Town, string Month, string Block, string Street,
    CoverageReason Reason, AddressMatch Evidence);
public sealed record CoverageReport(int Total, int Rejected, int Diagnostics,
    IReadOnlyDictionary<string, int> MatchQuality, IReadOnlyDictionary<string, int> CoordinateQuality,
    int MatchedWithoutGeometry, IReadOnlyDictionary<string, int> Reasons,
    IReadOnlyList<CoverageCell> CrossTabs, IReadOnlyList<CoverageFailure> Failures);

public static class CoverageStudy
{
    public static string Period(YearMonth month) => month.Year switch
    {
        >= 2017 and <= 2019 => "2017-2019", >= 2020 and <= 2022 => "2020-2022",
        >= 2023 and <= 2025 => "2023-2025", 2026 => "2026",
        _ => throw new ArgumentOutOfRangeException(nameof(month), "Outside pinned study periods.")
    };
    public static CoverageReason Reason(ResaleTransaction row)
    {
        var m = row.Match;
        if (m.PropertyCandidates.Count == 0) return CoverageReason.MissingProperty;
        if (m.PropertyCandidates.Count > 1) return CoverageReason.MultipleProperties;
        if (m.PostalAssertions.Any(p => p.PostalCode.Length != 6 || !p.PostalCode.All(char.IsAsciiDigit)))
            return CoverageReason.InvalidPostalAssertion;
        if (m.PostalAssertions.Select(p => p.PostalCode).Distinct(StringComparer.Ordinal).Count() > 1)
            return CoverageReason.ConflictingPostals;
        if (m.OneMap?.Outcome is OneMapOutcome.PostalConflict or OneMapOutcome.MultipleCandidates)
            return CoverageReason.OneMapCandidateConflict;
        if (m.OneMap?.Postal is { } postal && m.PostalAssertions.Any(p => p.PostalCode != postal))
            return CoverageReason.CrossSourcePostalConflict;
        if (m.HistoricalOneMap is { } h && HistoricalOneMap.Usable(row.Facts,h) && m.PostalAssertions.Any(p=>p.PostalCode!=h.Postal)) return CoverageReason.CrossSourcePostalConflict;
        if (m.PostalAssertions.Count == 0 && m.OneMap?.Postal is null && !(m.HistoricalOneMap is { } historical && HistoricalOneMap.Usable(row.Facts,historical))) return CoverageReason.MissingPostalCorroboration;
        if (m.FootprintCandidates.Count == 0) return CoverageReason.MissingFootprint;
        if (m.FootprintCandidates.Count > 1) return CoverageReason.MultipleFootprints;
        return row.Location.Point is null ? CoverageReason.MatchedWithoutGeometry : CoverageReason.Matched;
    }
    public static CoverageReport Summarize(ImportResult import)
    {
        var rows = import.Accepted;
        var matches = Enum.GetValues<MatchQuality>().ToDictionary(q => q.ToString(), q => rows.Count(r => r.Match.Quality == q));
        var coordinates = Enum.GetValues<CoordinateQuality>().ToDictionary(q => q.ToString(), q => rows.Count(r => r.Location.Quality == q));
        var reasons = Enum.GetValues<CoverageReason>().Where(q => rows.Any(r => r.Match.OneMap is not null || r.Match.HistoricalOneMap is not null) || q <= CoverageReason.MatchedWithoutGeometry || rows.Any(r => Reason(r) == q)).ToDictionary(q => q.ToString(), q => rows.Count(r => Reason(r) == q));
        var cells = rows.GroupBy(r => (r.Town, Period: Period(r.Facts.Month), Match: r.Match.Quality.ToString(), Coordinates: r.Location.Quality.ToString()))
            .OrderBy(g => g.Key.Town, StringComparer.Ordinal).ThenBy(g => g.Key.Period, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Match, StringComparer.Ordinal).ThenBy(g => g.Key.Coordinates, StringComparer.Ordinal)
            .Select(g => new CoverageCell(g.Key.Town, g.Key.Period, g.Key.Match, g.Key.Coordinates, g.Count())).ToArray();
        var failures = rows.Where(r => Reason(r) != CoverageReason.Matched)
            .Select(r => new CoverageFailure(r.Id, r.Town, r.Facts.Month.ToString(), r.Facts.Block, r.Facts.Street, Reason(r), r.Match)).ToArray();
        return new(rows.Count, import.Rejected.Count, import.Diagnostics.Count, matches, coordinates,
            reasons[nameof(CoverageReason.MatchedWithoutGeometry)], reasons, Array.AsReadOnly(cells), Array.AsReadOnly(failures));
    }
}
