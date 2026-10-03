using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class AddressMatchingTests
{
    private static TransactionFacts Facts(string block = "509", string street = "ANG MO KIO AVE 8")
    {
        Assert.True(YearMonth.TryParse("2017-01", out var month));
        return new(month!, "ANG MO KIO", block, street, "3 ROOM", 390000);
    }
    private static FootprintRecord Footprint(int id = 937499, string block = "509", string postal = "560509") =>
        new(new(id, 7861, block, postal), new(new(1.3739973579, 103.8501378907), CoordinateQuality.BlockApproximation, $"HDB OBJECTID {id}"));
    [Theory]
    [InlineData("ANG MO KIO ST 52", "ANG MO KIO STREET 52")]
    [InlineData("ANCHORVALE ST", "ANCHORVALE STREET")]
    [InlineData("BEDOK RESERVOIR RD", "BEDOK RESERVOIR ROAD")]
    [InlineData("EUNOS RD 5", "EUNOS ROAD 5")]
    [InlineData("BRIGHT HILL DR", "BRIGHT HILL DRIVE")]
    [InlineData("PASIR RIS DR 10", "PASIR RIS DRIVE 10")]
    [InlineData("TELOK BLANGAH CRES", "TELOK BLANGAH CRESCENT")]
    public void ExplicitProfileExpandsOnlyObservedRoadTypeContexts(string raw,string full)
    {
        Assert.NotEqual(AddressNormalizer.Street(raw),AddressNormalizer.Street(full));
        Assert.Equal(AddressNormalizer.Street(raw,true),AddressNormalizer.Street(full,true));
    }
    [Theory]
    [InlineData("ST 11", "STREET 11")]
    [InlineData("ST", "STREET")]
    [InlineData("ST JOHN RD", "STREET JOHN ROAD")]
    [InlineData("ST. GEORGE'S RD", "ST GEORGES ROAD")]
    [InlineData("ANG MO KIO ST 01", "ANG MO KIO STREET 1")]
    [InlineData("ANG MO KIO ST. 11", "ANG MO KIO STREET 11")]
    [InlineData("ANG MO KIO ST １１", "ANG MO KIO STREET １１")]
    [InlineData("MAIN ST 11 ANNEX", "MAIN STREET 11 ANNEX")]
    [InlineData("EUNOS RD 05", "EUNOS ROAD 5")]
    [InlineData("TEST CRES 1", "TEST CRESCENT 1")]
    [InlineData("BROADWAY", "BROADWAY ROAD")]
    [InlineData("STADIUM", "STREETADIUM")]
    public void ExpandedProfileNeverChangesNumbersPunctuationLeadingSaintOrSubstrings(string a,string b)
    {
        Assert.NotEqual(AddressNormalizer.Street(a,true),AddressNormalizer.Street(b,true));
    }
    [Fact]
    public void ConservativeObservedNormalizationMatchesButDoesNotCollapseSimilarAddresses()
    {
        Assert.Equal("ANG MO KIO AVENUE 8", AddressNormalizer.Street("  ang mo kio\tAVE   8 "));
        Assert.Equal("TAMPINES CENTRAL 1", AddressNormalizer.Street("TAMPINES CTRL 1"));
        Assert.Equal("509A", AddressNormalizer.Block(" 509a "));
        foreach (var (a, b) in new[] { ("AVE. 8", "AVENUE 8"), ("AVE 08", "AVE 8"),
            ("CTRL 10", "CTRL 1"), ("AVE 8 HOUSE", "AVENUE 8 HOUSE"), ("ST 1", "STREET 1"),
            ("ROAD-A", "ROAD A"), ("O'NEIL", "ONEIL") })
            Assert.NotEqual(AddressNormalizer.Street(a), AddressNormalizer.Street(b));
        foreach (var (a, b) in new[] { ("0509", "509"), ("509 A", "509A"), ("509-A", "509A") })
            Assert.NotEqual(AddressNormalizer.Block(a), AddressNormalizer.Block(b));
    }
    [Fact]
    public void NormalizedMatchRetainsAllAgreeingPostalAssertionsAndFootprintIdentity()
    {
        var match = AddressMatcher.Match(Facts(), [new(10, "509", "ANG MO KIO AVE 8")],
            [new(18184, "509", "ANG MO KIO AVENUE 8", "560509"), new(51472, "509", "ANG MO KIO AVENUE 8", "560509")], [Footprint()]);
        Assert.Equal(MatchQuality.NormalizedAddress, match.Quality);
        Assert.Equal(2, match.PostalAssertions.Count);
        Assert.Equal(new long[] { 18184, 51472 }, match.PostalAssertions.Select(p => p.SourceRow));
        Assert.Equal(937499, match.MatchedFootprint!.Identity.ObjectId);
        Assert.Equal("560509", match.MatchedFootprint.Identity.PostalCode);
    }
    [Fact]
    public void ConflictingPostalAssertionsAndMultipleFootprintsNeverChooseAWinner()
    {
        PropertyAddress[] properties = [new(10, "509", "ANG MO KIO AVE 8")];
        PostalAddress[] assertions = [new(2, "509", "ANG MO KIO AVENUE 8", "560509"), new(3, "509", "ANG MO KIO AVENUE 8", "999999")];
        var match = AddressMatcher.Match(Facts(), properties, assertions, [Footprint()]);
        Assert.Equal(MatchQuality.Ambiguous, match.Quality);
        Assert.Equal(2, match.PostalAssertions.Count);
        Assert.Null(match.MatchedFootprint);
        match = AddressMatcher.Match(Facts(), properties, [assertions[0]], [Footprint(), Footprint(999)]);
        Assert.Equal(MatchQuality.Ambiguous, match.Quality);
        Assert.Equal(2, match.FootprintCandidates.Count);
        Assert.Null(match.MatchedFootprint);
        match = AddressMatcher.Match(Facts(), [properties[0], properties[0] with { SourceRow = 11 }], [assertions[0]], [Footprint()]);
        Assert.Equal(MatchQuality.Ambiguous, match.Quality);
        Assert.Null(match.MatchedFootprint);
    }
    [Fact]
    public void UnmatchedAndAmbiguousTransactionsStayFilterableAndSelectable()
    {
        var facts = Facts();
        var missing = AddressMatcher.Match(facts, [], [], []);
        var ambiguous = AddressMatcher.Match(facts, [new(2, "509", facts.Street), new(3, "509", facts.Street)], [], []);
        var location = new DerivedLocation(null, CoordinateQuality.Missing, "No proven footprint.");
        var state = new ExplorerState([new("unmatched", facts, location, missing), new("ambiguous", facts, location, ambiguous)]);
        Assert.Equal(2, state.Visible.Count);
        state.Select("ambiguous"); Assert.NotNull(state.Selected);
        state.Filter("ANG MO KIO", 390000); Assert.Equal("ambiguous", state.Selected?.Id);
        state.Select("unmatched"); Assert.NotNull(state.Selected);
        state.Filter("CLEMENTI", 390000); Assert.Null(state.Selected);
        state.Reset(); Assert.Equal(2, state.Visible.Count);
    }
    [Fact]
    public void PostalCodeAloneOrBlockAloneDoesNotProveFootprintIdentity()
    {
        var properties = new[] { new PropertyAddress(10, "509", "ANG MO KIO AVE 8") };
        var assertions = new[] { new PostalAddress(2, "509", "ANG MO KIO AVENUE 8", "560509") };
        var match = AddressMatcher.Match(Facts(), properties, assertions,
            [Footprint(block: "510"), Footprint(postal: "560510")]);
        Assert.Equal(MatchQuality.Unmatched, match.Quality);
        Assert.Null(match.MatchedFootprint);
    }
}
