namespace HdbResale.Domain;

public sealed class ExplorerState
{
    private readonly IReadOnlyList<ResaleTransaction> transactions;
    public ExplorerState(IReadOnlyList<ResaleTransaction> transactions)
    {
        this.transactions = Array.AsReadOnly(transactions.ToArray());
        Visible = this.transactions;
    }

    public string Town { get; private set; } = "All towns";
    public int MaximumPrice { get; private set; } = 1_000_000;
    public IReadOnlyList<ResaleTransaction> Visible { get; private set; }
    public ResaleTransaction? Selected { get; private set; }

    public void Filter(string town, int maximumPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(town);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumPrice);
        Town = town;
        MaximumPrice = maximumPrice;
        Visible = Array.AsReadOnly(transactions.Where(t =>
            (town == "All towns" || t.Town == town) && t.Price <= maximumPrice).ToArray());
        if (Selected is not null && !Visible.Contains(Selected))
            Selected = null;
    }

    public void Select(string id) => Selected = Visible.FirstOrDefault(t => t.Id == id);
    public void Reset() => Filter("All towns", 1_000_000);
}
