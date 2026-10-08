using System.Globalization;
using HdbResale.Domain;
namespace HdbResale.App;

// Small, immutable C# series for the selected address only, from the API's monthly medians. No chart
// framework or alternate filter contract. A month without a registration stays null.
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
    // The 24 source months ending at the dataset's latest month; the API lists only months with a sale.
    internal static BuyerTrendData Build(AddressDetail? detail, YearMonth latest)
    {
        if (detail is null) return BuyerTrendData.Empty;
        var end = new DateTime(latest.Year, latest.Month, 1);
        var monthCount = Math.Min(24, (latest.Year - 1) * 12 + latest.Month);
        var start = end.AddMonths(1 - monthCount);
        var observed = detail.MonthlyTrend.ToDictionary(p => p.Month, StringComparer.Ordinal);
        var points = Enumerable.Range(0, monthCount).Select(index =>
        {
            var month = start.AddMonths(index).ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return observed.TryGetValue(month, out var point)
                ? new BuyerTrendPoint(month, index, point.TransactionCount, point.MedianPrice)
                : new BuyerTrendPoint(month, index, 0, null);
        }).ToArray();
        var seen = points.Where(p => p.Count > 0).ToArray();
        var minimum = seen.Length == 0 ? 0 : decimal.Floor(seen.Min(p => p.MedianPrice!.Value) / 50_000) * 50;
        var maximum = seen.Length == 0 ? 1 : decimal.Ceiling(seen.Max(p => p.MedianPrice!.Value) / 50_000) * 50;
        if (seen.Length > 0) { minimum = Math.Max(0, minimum - 50); maximum += 50; }
        return new(Array.AsReadOnly(points), points[0].Month, points[^1].Month, seen.Length,
            seen.Sum(p => p.Count), minimum, maximum);
    }
}
