using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class HistoricalOneMapTests
{
    private static TransactionFacts Facts(string town="QUEENSTOWN",string block="81",string street="C'WEALTH CL")
    { Assert.True(YearMonth.TryParse("2026-01",out var month));return new(month!,town,block,street,"3 ROOM",100m); }
    private static HistoricalPostalAssertion Synthetic(TransactionFacts f,string postal="123456") => new(HistoricalOneMap.Key(f),HistoricalOneMap.SearchValue(f),"HistoricalFirstHit",postal,"2026-05-24T00:00:00Z");
    [Fact]
    public void KeysUseExactWebSemanticsWithoutNativeAliasesOrFuzzyJoin()
    {
        Assert.Equal("queenstown-81-c-wealth-cl",HistoricalOneMap.Key(Facts()));
        Assert.Equal("81 C'WEALTH CL SINGAPORE",HistoricalOneMap.SearchValue(Facts()));
        Assert.NotEqual(HistoricalOneMap.Key(Facts(street:"C'WEALTH CL")),HistoricalOneMap.Key(Facts(street:"COMMONWEALTH CLOSE")));
        Assert.NotEqual(HistoricalOneMap.Key(Facts(street:"TEST AVE 1")),HistoricalOneMap.Key(Facts(street:"TEST AVENUE 1")));
    }
    [Fact]
    public void FirstHitExperimentKeepsWeakEvidenceSeparateAndRequiresHdbFootprint()
    {
        var f=Facts();var h=Synthetic(f);var property=new PropertyAddress(2,f.Block,f.Street);
        var footprint=new FootprintRecord(new(1,1,f.Block,"123456"),new(new(1.3,103.8),CoordinateQuality.BlockApproximation,"SYNTHETIC UNIT HDB footprint"));
        var m=AddressMatcher.Match(f,[property],[],[footprint],historicalAssertion:h);
        Assert.Equal(MatchQuality.NormalizedAddress,m.Quality);Assert.Null(m.OneMap);Assert.Equal(h,m.HistoricalOneMap);Assert.Contains("EXPERIMENTAL historical first-hit",m.Reason);Assert.Equal(1.3,m.MatchedFootprint!.Location.Point!.Latitude);
        Assert.Equal(MatchQuality.Unmatched,AddressMatcher.Match(f,[property],[],[],historicalAssertion:h).Quality);
        foreach(var invalid in new[]{h with {SearchValue="WRONG"},h with {CacheKey="wrong"},h with {Postal="123"},h with {Status="MissingKey"}})
            Assert.Equal(MatchQuality.Unmatched,AddressMatcher.Match(f,[property],[],[footprint],historicalAssertion:invalid).Quality);
    }
    [Fact]
    public void ContradictingHistoricalPostalCannotOverrideAcraAndKeepsSourcesSeparate()
    {
        var f=Facts();var h=Synthetic(f,"654321");var m=AddressMatcher.Match(f,[new(2,f.Block,f.Street)],[new(3,f.Block,f.Street,"123456")],[],historicalAssertion:h);
        Assert.Equal(MatchQuality.Ambiguous,m.Quality);Assert.Single(m.PostalAssertions);Assert.Equal("654321",m.HistoricalOneMap!.Postal);Assert.Null(m.MatchedFootprint);
    }
    [Fact]
    public void OriginalRealConflictRemainsAmbiguousWithHistoricalMajorityAgreement()
    {
        var row=Assert.Single(CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory,"coverage")).Accepted,t=>t.Id=="HDB-6769");
        var h=Synthetic(row.Facts,"530446");var m=AddressMatcher.Match(row.Facts,row.Match.PropertyCandidates,row.Match.PostalAssertions,[],historicalAssertion:h);
        Assert.Equal(MatchQuality.Ambiguous,m.Quality);Assert.Equal(74,m.PostalAssertions.Count);Assert.Contains(m.PostalAssertions,p=>p.SourceRow==60428&&p.PostalCode=="530836");Assert.Null(m.MatchedFootprint);
    }
}
