using HdbResale.App;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

public sealed class LocationPresentationTests
{
    private static BlockSummary Summary(bool mapped, params MatchQuality[] qualities)
    {
        Assert.True(YearMonth.TryParse("2025-06", out var month));
        var row = new ResaleTransaction("a", new(month!, "T", "1", "ST", "3 ROOM", 400_000),
            mapped ? new(new(1.3, 103.8), CoordinateQuality.BlockApproximation, "e") : new(null, CoordinateQuality.Missing, "e"),
            new(qualities[0], "r", [], [], []));
        return new BlockSummary("k", row, 1, 400_000, 400_000, 400_000) { IsMapped = mapped, MatchQualities = qualities };
    }

    [Fact]
    public void MappedAddressesAreAlwaysDescribedAsBlockLevelNeverExact()
    {
        var exact = Summary(true, MatchQuality.ExactAddress);
        Assert.Equal("approximate", LocationPresentation.State(exact));
        Assert.Equal("Block-level location", LocationPresentation.Short(exact));
        Assert.Contains("matches the building records exactly", LocationPresentation.Detail(exact));
        Assert.Contains("shows the block, not the flat", LocationPresentation.Detail(exact));
        Assert.Contains("after standard spelling normalisation", LocationPresentation.Detail(Summary(true, MatchQuality.ExactAddress, MatchQuality.NormalizedAddress)));
    }

    [Fact]
    public void UnmappedAddressesStateWhyAndNeverImplyAPosition()
    {
        var ambiguous = Summary(false, MatchQuality.Ambiguous, MatchQuality.Unmatched);
        Assert.Equal("ambiguous", LocationPresentation.State(ambiguous));
        Assert.Equal("Location ambiguous", LocationPresentation.Short(ambiguous));
        Assert.Contains("not placed on the map", LocationPresentation.Detail(ambiguous));
        var unmatched = Summary(false, MatchQuality.Unmatched);
        Assert.Equal("unmatched", LocationPresentation.State(unmatched));
        Assert.Equal("Not on map", LocationPresentation.Short(unmatched));
        var mixed = Summary(false, MatchQuality.ExactAddress);
        Assert.Equal("unavailable", LocationPresentation.State(mixed));
        Assert.Contains("do not agree on one position", LocationPresentation.Detail(mixed));
    }
}
