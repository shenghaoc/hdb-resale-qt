namespace HdbResale.Domain;

// Presentation projection only: all accepted transactions remain in ExplorerState.
public sealed record BlockSummary(string Key, ResaleTransaction Latest, int Count, decimal MinimumPrice,
    decimal MaximumPrice, decimal MedianPrice);
public static class BlockSummaries
{
    public static string Key(ResaleTransaction row) => $"{row.Town}|{row.Facts.Block}|{row.Facts.Street}";
    public static IReadOnlyList<BlockSummary> Located(IReadOnlyList<ResaleTransaction> rows)
    {
        return rows.Where(t => t.Location.Point is not null).GroupBy(Key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
            {
                // Never combine inconsistent points into an invented representative coordinate.
                if (g.Select(t => t.Location.Point).Distinct().Count() != 1)
                    throw new InvalidDataException($"Located address has inconsistent footprint points: {g.Key}");
                var prices = g.Select(t => t.Price).Order().ToArray();
                var latest = g.OrderByDescending(t => t.Facts.Month.Year).ThenByDescending(t => t.Facts.Month.Month)
                    .ThenBy(t => t.Id, StringComparer.Ordinal).First();
                return new BlockSummary(g.Key, latest, prices.Length, prices[0], prices[^1],
                    (prices[(prices.Length - 1) / 2] + prices[prices.Length / 2]) / 2);
            }).ToArray();
    }
}
