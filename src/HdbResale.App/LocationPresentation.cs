using HdbResale.Domain;

namespace HdbResale.App;

// Presentation-only wording for how reliably an address is placed on the map.
// It restates the existing match/coordinate semantics in plain language and never
// strengthens them: a mapped address is always a block-level position.
internal static class LocationPresentation
{
    internal const string Approximate = "approximate", Ambiguous = "ambiguous", Unmatched = "unmatched", Unavailable = "unavailable";

    internal static string State(BlockSummary b) =>
        b.IsMapped ? Approximate
        : b.MatchQualities.Contains(MatchQuality.Ambiguous) ? Ambiguous
        : b.MatchQualities.Contains(MatchQuality.Unmatched) ? Unmatched
        : Unavailable;

    // Short label for list rows.
    internal static string Short(BlockSummary b) => State(b) switch
    {
        Approximate => "Block-level location",
        Ambiguous => "Location ambiguous",
        _ => "Not on map",
    };

    // One or two sentences for the details view.
    internal static string Detail(BlockSummary b)
    {
        var identity = b.MatchQualities.All(q => q == MatchQuality.ExactAddress)
            ? "The address matches the building records exactly."
            : b.MatchQualities.All(q => q is MatchQuality.ExactAddress or MatchQuality.NormalizedAddress)
                ? "The address matches the building records after standard spelling normalisation."
                : "";
        return State(b) switch
        {
            Approximate => $"{identity} The pin is placed on the building block's footprint, so it shows the block, not the flat.".TrimStart(),
            Ambiguous => "More than one building or postal code fits this address, so it is not placed on the map.",
            Unmatched => "No matching building record was found, so it is not placed on the map.",
            _ => "The records for this address do not agree on one position, so it is not shown on the map.",
        };
    }
}
