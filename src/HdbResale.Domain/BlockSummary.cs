namespace HdbResale.Domain;

// Advance source remaining-lease observations by elapsed calendar months. This
// is approximate, not an eligibility assessment or a commencement-year formula.
public sealed record LeaseEstimate(YearMonth ReferenceMonth, int MinimumMonths, int MaximumMonths)
{
    public bool IncludesWholeYearObservations { get; init; }
}

// Presentation projection only: all accepted transactions remain in ExplorerState.
public sealed record BlockSummary(string Key, ResaleTransaction Latest, int Count, decimal MinimumPrice,
    decimal MaximumPrice, decimal MedianPrice)
{
    public IReadOnlyList<string> FlatTypes { get; init; } = [];
    public decimal? MinimumAreaSqm { get; init; }
    public decimal? MaximumAreaSqm { get; init; }
    public decimal? MedianPricePerSqm { get; init; }
    public int PricePerSqmCount { get; init; }
    public int LeaseEstimateCount { get; init; }
    public bool LeaseIncludesWholeYearObservations { get; init; }
    public IReadOnlyList<int> LeaseCommenceYears { get; init; } = [];
    public IReadOnlyList<MatchQuality> MatchQualities { get; init; } = [];
    public IReadOnlyList<CoordinateQuality> CoordinateQualities { get; init; } = [];
    public IReadOnlyList<ResaleTransaction> RecentTransactions { get; init; } = [];
    internal IReadOnlyList<int> LeaseExpiryMonthIndices { get; init; } = [];
    // Every contributing row must agree on one non-null point. Mixed located /
    // missing / conflicting points remain in the complete address table only.
    public bool IsMapped { get; init; }
    public LeaseEstimate? EstimateLeaseMonths(YearMonth referenceMonth)
    {
        ArgumentNullException.ThrowIfNull(referenceMonth);
        var referenceIndex = referenceMonth.Year * 12 + referenceMonth.Month - 1;
        return LeaseExpiryMonthIndices.Count == 0 ? null : new(referenceMonth,
            LeaseExpiryMonthIndices.Min() - referenceIndex,
            LeaseExpiryMonthIndices.Max() - referenceIndex) { IncludesWholeYearObservations = LeaseIncludesWholeYearObservations };
    }
}
public static class BlockSummaries
{
    public static string Key(ResaleTransaction row) => $"{row.Town}|{row.Facts.Block}|{row.Facts.Street}";

    public static IOrderedEnumerable<ResaleTransaction> NewestFirst(IEnumerable<ResaleTransaction> rows) => rows
        .OrderByDescending(t => t.Facts.Month.Year).ThenByDescending(t => t.Facts.Month.Month)
        .ThenBy(t => t.FlatType, StringComparer.Ordinal)
        .ThenBy(t => t.Facts.StoreyRange, StringComparer.Ordinal)
        .ThenBy(t => t.Facts.FloorAreaSqm)
        .ThenBy(t => t.Price)
        .ThenBy(t => t.PricePerSqm)
        .ThenBy(t => t.Facts.FlatModel, StringComparer.Ordinal)
        .ThenBy(t => t.Facts.RemainingLeaseSource, StringComparer.Ordinal)
        .ThenBy(t => t.Id, StringComparer.Ordinal);

    public static IReadOnlyList<BlockSummary> All(IReadOnlyList<ResaleTransaction> rows) =>
        Array.AsReadOnly(rows.GroupBy(Key, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var prices = g.Select(t => t.Price).Order().ToArray();
                var areas = g.Select(t => t.Facts.FloorAreaSqm).Where(a => a is > 0).Select(a => a!.Value).ToArray();
                var ratios = g.Select(t => t.PricePerSqm).Where(p => p.HasValue).Select(p => p!.Value).Order().ToArray();
                var recent = NewestFirst(g).Take(15).ToArray();
                var points = g.Select(t => t.Location.Point).Distinct().ToArray();
                return new BlockSummary(g.Key, recent[0], prices.Length, prices[0], prices[^1], Median(prices))
                {
                    FlatTypes = Array.AsReadOnly(g.Select(t => t.FlatType).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                    MinimumAreaSqm = areas.Length == 0 ? null : areas.Min(),
                    MaximumAreaSqm = areas.Length == 0 ? null : areas.Max(),
                    MedianPricePerSqm = ratios.Length == 0 ? null : Median(ratios),
                    PricePerSqmCount = ratios.Length,
                    LeaseEstimateCount = g.Count(t => t.Facts.RemainingLeaseMonths is >= 0),
                    LeaseIncludesWholeYearObservations = g.Any(t => t.Facts.RemainingLeaseReportedInWholeYears),
                    LeaseCommenceYears = Array.AsReadOnly(g.Select(t => t.Facts.LeaseCommenceYear)
                        .Where(y => y is >= 1 and <= 9999).Select(y => y!.Value).Distinct().Order().ToArray()),
                    MatchQualities = Array.AsReadOnly(g.Select(t => t.Match.Quality).Distinct().Order().ToArray()),
                    CoordinateQualities = Array.AsReadOnly(g.Select(t => t.Location.Quality).Distinct().Order().ToArray()),
                    RecentTransactions = Array.AsReadOnly(recent),
                    LeaseExpiryMonthIndices = Array.AsReadOnly(g.Where(t => t.Facts.RemainingLeaseMonths is >= 0)
                        .Select(t => t.Facts.Month.Year * 12 + t.Facts.Month.Month - 1 + t.Facts.RemainingLeaseMonths!.Value)
                        .Distinct().Order().ToArray()),
                    IsMapped = points.Length == 1 && points[0] is not null
                };
            }).ToArray());

    // Retained for callers of the legacy located-transaction projection. Buyer UI
    // must use ExplorerState.MappedAddresses, a subset of complete summaries, so
    // a missing row never disappears from an address's statistics on the map.
    public static IReadOnlyList<BlockSummary> Located(IReadOnlyList<ResaleTransaction> rows)
    {
        var located = rows.Where(t => t.Location.Point is not null).ToArray();
        foreach (var group in located.GroupBy(Key, StringComparer.Ordinal))
            if (group.Select(t => t.Location.Point).Distinct().Count() != 1)
                throw new InvalidDataException($"Located address has inconsistent footprint points: {group.Key}");
        return All(located);
    }
    private static decimal Median(IReadOnlyList<decimal> sorted)
    {
        var lower = sorted[(sorted.Count - 1) / 2];
        var upper = sorted[sorted.Count / 2];
        // Add before dividing to retain decimal rounding at the smallest scale;
        // for very large positive prices, the equivalent difference form avoids overflow.
        return lower <= decimal.MaxValue - upper ? (lower + upper) / 2 : lower + (upper - lower) / 2;
    }
}
