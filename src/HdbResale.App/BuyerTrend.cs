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
    // The chart's text alternative, from the same points it plots.
    public string Summary => BuyerTrend.Describe(this);
}
internal static class BuyerTrend
{
    // What the chart shows, as one sentence: the highest and lowest monthly medians with their months, and how many of
    // the months shown had a sale. Empty when none did, as the chart is then replaced by a caption.
    internal static string Describe(BuyerTrendData trend)
    {
        var seen = trend.Points.Where(p => p.Count > 0 && p.MedianPrice is not null).ToArray();
        if (seen.Length == 0) return "";
        var shown = trend.Points.Count;
        var high = seen.Max(p => p.MedianPrice!.Value);
        var low = seen.Min(p => p.MedianPrice!.Value);
        string In(decimal price) => Join(seen.Where(p => p.MedianPrice == price).Select(p => p.Month).ToArray());
        if (seen.Length == 1)
            return $"{BuyerPresentation.Money(high)} in {In(high)}, the only month with sales of the {shown} shown.";
        if (high == low)
            return $"{BuyerPresentation.Money(high)} in {In(high)}, the only {seen.Length} months with sales of the {shown} shown.";
        return $"Highest {BuyerPresentation.Money(high)} in {In(high)}, lowest {BuyerPresentation.Money(low)} in {In(low)}, "
            + $"{seen.Length} months with sales of the {shown} shown.";
    }

    private static string Join(IReadOnlyList<string> months) =>
        months.Count == 1 ? months[0] : string.Join(", ", months.Take(months.Count - 1)) + " and " + months[^1];

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
