using System.Globalization;
namespace HdbResale.Domain;

public sealed record YearMonth
{
    private YearMonth(int year, int month) { Year = year; Month = month; }
    public int Year { get; }
    public int Month { get; }
    public static bool TryParse(string value, out YearMonth? result)
    {
        result = null;
        if (value.Length != 7 || !DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)) return false;
        result = new(date.Year, date.Month);
        return true;
    }
    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
public enum CoordinateQuality { Missing, BlockApproximation }
public sealed record GeoPoint
{
    public GeoPoint(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude));
        Latitude = latitude; Longitude = longitude;
    }
    public double Latitude { get; }
    public double Longitude { get; }
}
public sealed record DerivedLocation
{
    public DerivedLocation(GeoPoint? point, CoordinateQuality quality, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (!Enum.IsDefined(quality) || (quality == CoordinateQuality.Missing) != (point is null))
            throw new ArgumentException("Location quality must agree with coordinate presence.");
        Point = point; Quality = quality; Source = source;
    }
    public GeoPoint? Point { get; }
    public CoordinateQuality Quality { get; }
    public string Source { get; }
}
public sealed record TransactionFacts(YearMonth Month, string Town, string Block, string Street,
    string FlatType, decimal Price)
{
    // These columns are optional. Preserve their exact source text separately
    // from safe, nullable numeric interpretations; absent is not zero.
    public string? StoreyRange { get; init; }
    public string? FloorAreaSqmSource { get; init; }
    public decimal? FloorAreaSqm { get; init; }
    public string? FlatModel { get; init; }
    public string? LeaseCommenceDateSource { get; init; }
    public int? LeaseCommenceYear { get; init; }
    public string? RemainingLeaseSource { get; init; }
    public int? RemainingLeaseMonths { get; init; }
}
public sealed record ResaleTransaction(string Id, TransactionFacts Facts, DerivedLocation Location, AddressMatch Match)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TransactionProvenance? Provenance { get; init; }
    public string Town => Facts.Town;
    public string Address => $"{Facts.Block} {Facts.Street}";
    public string FlatType => Facts.FlatType;
    public decimal Price => Facts.Price;
    public decimal? PricePerSqm
    {
        get
        {
            if (Facts.FloorAreaSqm is not > 0 || Price <= 0) return null;
            try { return Price / Facts.FloorAreaSqm.Value; }
            catch (OverflowException) { return null; }
        }
    }
}
