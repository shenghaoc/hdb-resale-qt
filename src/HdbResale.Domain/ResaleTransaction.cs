namespace HdbResale.Domain;

// Synthetic transactions and approximate coordinates: demonstration only.
public sealed record ResaleTransaction(string Id, string Town, string Address,
    string FlatType, int Price, double Latitude, double Longitude);
