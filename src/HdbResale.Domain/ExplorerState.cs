namespace HdbResale.Domain;

public sealed class ExplorerState
{
    private readonly IReadOnlyList<ResaleTransaction> transactions;
    public ExplorerState(IReadOnlyList<ResaleTransaction> transactions)
    {
        this.transactions = Array.AsReadOnly(transactions.ToArray());
        LatestDatasetMonth = this.transactions.OrderByDescending(t => t.Facts.Month.Year)
            .ThenByDescending(t => t.Facts.Month.Month).FirstOrDefault()?.Facts.Month;
        Rebuild();
    }

    public const string AllTowns = "All towns";
    public const string AllFlatTypes = "All flat types";
    public string Town { get; private set; } = AllTowns;
    public string FlatType { get; private set; } = AllFlatTypes;
    public decimal MinimumPrice { get; private set; }
    public decimal MaximumPrice { get; private set; } = 1_000_000;
    public int RecencyMonths { get; private set; }
    public YearMonth? LatestDatasetMonth { get; }
    public IReadOnlyList<ResaleTransaction> Visible { get; private set; } = [];
    public IReadOnlyList<BlockSummary> Addresses { get; private set; } = [];
    public IReadOnlyList<BlockSummary> MappedAddresses { get; private set; } = [];
    public ResaleTransaction? Selected { get; private set; }
    public BlockSummary? SelectedAddress { get; private set; }
    public IReadOnlyList<ResaleTransaction> RecentTransactions => SelectedAddress?.RecentTransactions ?? [];

    // Preserve the original two-filter meaning for existing callers.
    public void Filter(string town, decimal maximumPrice) => Filter(town, AllFlatTypes, 0, maximumPrice, 0);
    public void Filter(string town, string flatType, decimal minimumPrice, decimal maximumPrice, int recencyMonths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        ArgumentException.ThrowIfNullOrWhiteSpace(flatType);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPrice);
        // Independently edited bounds may temporarily cross; that is an empty
        // result, not an exception or a silently repaired filter.
        if (recencyMonths is not (0 or 12 or 24)) throw new ArgumentOutOfRangeException(nameof(recencyMonths));
        Town = town;
        FlatType = flatType;
        MinimumPrice = minimumPrice;
        MaximumPrice = maximumPrice;
        RecencyMonths = recencyMonths;
        Rebuild();
    }

    private void Rebuild()
    {
        var selectedKey = SelectedAddress?.Key;
        var latestMonthIndex = LatestDatasetMonth is null ? 0 : MonthIndex(LatestDatasetMonth);
        Visible = Array.AsReadOnly(transactions.Where(t =>
            (Town == AllTowns || t.Town == Town) &&
            (FlatType == AllFlatTypes || t.FlatType == FlatType) &&
            t.Price >= MinimumPrice && t.Price <= MaximumPrice &&
            (RecencyMonths == 0 || latestMonthIndex - MonthIndex(t.Facts.Month) < RecencyMonths)).ToArray());
        Addresses = BlockSummaries.All(Visible);
        // Reuse the very same summary instances; map statistics are never
        // recomputed from only located transactions or viewport-visible rows.
        MappedAddresses = Array.AsReadOnly(Addresses.Where(b => b.IsMapped).ToArray());
        SelectedAddress = selectedKey is null ? null : Addresses.FirstOrDefault(b => b.Key == selectedKey);
        if (SelectedAddress is null) Selected = null;
        else if (Selected is null || !Visible.Contains(Selected)) Selected = SelectedAddress.Latest;
    }
    private static int MonthIndex(YearMonth month) => month.Year * 12 + month.Month - 1;

    public void Select(string id)
    {
        Selected = Visible.FirstOrDefault(t => t.Id == id);
        SelectedAddress = Selected is null ? null : Addresses.FirstOrDefault(b => b.Key == BlockSummaries.Key(Selected));
    }
    public void SelectAddress(string key)
    {
        SelectedAddress = Addresses.FirstOrDefault(b => b.Key == key);
        Selected = SelectedAddress?.Latest;
    }
    public void Reset() => Filter(AllTowns, AllFlatTypes, 0, 1_000_000, 0);
}
