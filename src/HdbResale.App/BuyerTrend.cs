using HdbResale.Domain;
namespace HdbResale.App;

// Small, immutable C# series for the selected matching cohort only. No chart
// framework, database or alternate filter contract. A missing month stays null.
internal sealed record BuyerTrendPoint(string Month, int X, int Count, decimal? MedianPrice)
{
    public decimal? PriceThousands => MedianPrice / 1000;
}
internal sealed record BuyerTrendData(IReadOnlyList<BuyerTrendPoint> Points, string Start, string End,
    int ObservedMonths, int Sales, decimal MinimumY, decimal MaximumY)
{
    internal static BuyerTrendData Empty { get; } = new([], "", "", 0, 0, 0, 1);
}
internal static class BuyerTrend
{
    internal static BuyerTrendData Build(ExplorerState state)
    {
        if (state.SelectedAddress is not { } selected || state.LatestDatasetMonth is not { } latest)
            return BuyerTrendData.Empty;
        var end = new DateTime(latest.Year, latest.Month, 1);
        var monthCount = Math.Min(24, (latest.Year - 1) * 12 + latest.Month);
        var start = end.AddMonths(1 - monthCount);
        var identity = selected.Latest.Facts;
        var buckets = state.Visible.Where(t => t.Town == identity.Town && t.Facts.Block == identity.Block && t.Facts.Street == identity.Street)
            .Where(t => t.Facts.Month.Year * 12 + t.Facts.Month.Month >= start.Year * 12 + start.Month)
            .GroupBy(t => t.Facts.Month.ToString()).ToDictionary(g => g.Key, g => g.Select(t => t.Price).Order().ToArray(), StringComparer.Ordinal);
        var points = Enumerable.Range(0, monthCount).Select(index => {
            var month = start.AddMonths(index).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
            if (!buckets.TryGetValue(month, out var prices)) return new BuyerTrendPoint(month, index, 0, null);
            var lower=prices[(prices.Length-1)/2]; var upper=prices[prices.Length/2];
            var median=lower<=decimal.MaxValue-upper?(lower+upper)/2:lower+(upper-lower)/2;
            return new BuyerTrendPoint(month, index, prices.Length, median);
        }).ToArray();
        var observed = points.Where(p => p.Count > 0).ToArray();
        var minimum = observed.Length == 0 ? 0 : decimal.Floor(observed.Min(p => p.MedianPrice!.Value) / 50_000) * 50;
        var maximum = observed.Length == 0 ? 1 : decimal.Ceiling(observed.Max(p => p.MedianPrice!.Value) / 50_000) * 50;
        if (observed.Length > 0) { minimum=Math.Max(0,minimum-50); maximum+=50; }
        return new(Array.AsReadOnly(points), points[0].Month, points[^1].Month, observed.Length,
            observed.Sum(p => p.Count), minimum, maximum);
    }
}
