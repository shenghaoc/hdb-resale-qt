// Synthetic UNIT-test OneMap responses only. Nothing here is acquired Search evidence.
using HdbResale.Domain;
using System.Text.Json;
using Xunit;
namespace HdbResale.Tests;
public sealed class OneMapSyntheticTests
{
    private static TransactionFacts Facts()
    {
        Assert.True(YearMonth.TryParse("2026-01",out var m)); return new(m!,"T","1","TEST AVE 1","3 ROOM",100m);
    }
    private static OneMapCandidate Candidate(string postal="123456",string building="NIL",int page=1,int index=1,string block="1",string road="TEST AVENUE 1") => new(page,index,block,road,postal,building);
    private static OneMapSearch Search(params OneMapCandidate[] candidates) => new("synthetic-unit-id","1","TEST AVE 1","1 TEST AVE 1","SyntheticUnitTest",OneMapEvidence.Endpoint,"2026-10-03T00:00:00+00:00",candidates.Length,1,candidates,[],null);
    private static FootprintRecord Footprint(string postal="123456") => new(new(42,77,"1",postal),new(new(1.3,103.8),CoordinateQuality.BlockApproximation,"SYNTHETIC UNIT-test HDB footprint"));
    private static AddressMatch Match(OneMapSearch search,params PostalAddress[] acra) => AddressMatcher.Match(Facts(),[new(2,"1","TEST AVE 1")],acra,[Footprint(),Footprint("654321")],search);
    [Fact]
    public void UniqueCandidateGainsEvidenceButNeverCoordinatesFromOneMap()
    {
        var json=JsonSerializer.Serialize(Search(Candidate()));
        // Simulated extra provider geometry is outside the identity model and ignored.
        json=json[..^1]+",\"LATITUDE\":\"88\",\"LONGITUDE\":\"999\"}";
        var search=JsonSerializer.Deserialize<OneMapSearch>(json)!;
        var match=Match(search);
        Assert.Equal(MatchQuality.NormalizedAddress,match.Quality);Assert.Equal(OneMapOutcome.UniqueCandidate,match.OneMap!.Outcome);
        Assert.Equal(1.3,match.MatchedFootprint!.Location.Point!.Latitude);Assert.Equal(103.8,match.MatchedFootprint.Location.Point.Longitude);
        match=AddressMatcher.Match(Facts(),[new(2,"1","TEST AVE 1")],[],[],search);
        Assert.Equal(MatchQuality.Unmatched,match.Quality);Assert.Null(match.MatchedFootprint);
    }
    [Fact]
    public void IdenticalCandidatesAggregateWhileDifferentPostalsOrBuildingsAreAmbiguous()
    {
        var match=Match(Search(Candidate(),Candidate(page:2,index:2)));
        Assert.True(match.IsMatched);Assert.Equal(2,match.OneMap!.MatchingCandidates.Count);
        match=Match(Search(Candidate(),Candidate("654321")));
        Assert.Equal(MatchQuality.Ambiguous,match.Quality);Assert.Equal(OneMapOutcome.PostalConflict,match.OneMap!.Outcome);Assert.Null(match.MatchedFootprint);
        match=Match(Search(Candidate(building:"A"),Candidate(building:"B")));
        Assert.Equal(MatchQuality.Ambiguous,match.Quality);Assert.Equal(OneMapOutcome.MultipleCandidates,match.OneMap!.Outcome);
    }
    [Fact]
    public void SearchOrderAddressConflictsAndInvalidPostalsDoNotSupplyIdentity()
    {
        var match=Match(Search(Candidate(block:"2"),Candidate()));
        Assert.True(match.IsMatched);Assert.Single(match.OneMap!.RejectedCandidates);
        foreach (var c in new[]{Candidate(block:"2"),Candidate(road:"TEST AVE. 1"),Candidate(postal:"NIL")})
        {
            match=Match(Search(c));Assert.Equal(MatchQuality.Unmatched,match.Quality);Assert.Equal(OneMapOutcome.AddressConflict,match.OneMap!.Outcome);
        }
        match=AddressMatcher.Match(Facts(),[],[],[Footprint()],Search(Candidate()));
        Assert.Equal(MatchQuality.Unmatched,match.Quality);Assert.Null(match.MatchedFootprint);
    }
    [Fact]
    public void NoCandidatesAndApiErrorAreSeparateAndDoNotEraseAcra()
    {
        var acra=new PostalAddress(2,"1","TEST AVE 1","123456");
        var empty=Match(Search(),acra);Assert.True(empty.IsMatched);Assert.Equal(OneMapOutcome.NoCandidates,empty.OneMap!.Outcome);
        var failed=Match(Search(Candidate()) with { Error=new("PartialPagination","HttpFailure",500,2) },acra);
        Assert.True(failed.IsMatched);Assert.Equal(OneMapOutcome.ApiError,failed.OneMap!.Outcome);Assert.Single(failed.PostalAssertions);
        failed=Match(Search(Candidate()) with { Error=new("ExpiredToken",null,200,1) });
        Assert.Equal(MatchQuality.Unmatched,failed.Quality);Assert.Equal(OneMapOutcome.ApiError,failed.OneMap!.Outcome);
    }
    [Fact]
    public void CrossSourceContradictionKeepsBothSourcesAndSelectsNeither()
    {
        var match=Match(Search(Candidate("654321")),new PostalAddress(2,"1","TEST AVE 1","123456"));
        Assert.Equal(MatchQuality.Ambiguous,match.Quality);Assert.Single(match.PostalAssertions);Assert.Single(match.OneMap!.MatchingCandidates);Assert.Null(match.MatchedFootprint);
    }
    [Fact]
    public void RealM4ConflictCannotBeOverruledBySyntheticOneMapMajorityAgreement()
    {
        var import=CsvImport.LoadDirectory(Path.Combine(AppContext.BaseDirectory,"coverage"));
        var row=Assert.Single(import.Accepted,t=>t.Id=="HDB-6769");
        var candidate=new OneMapCandidate(1,1,"446","HOUGANG AVENUE 8","530446","NIL");
        var search=Search(candidate) with {Block="446",Road="HOUGANG AVE 8",Query="446 HOUGANG AVE 8"};
        var match=AddressMatcher.Match(row.Facts,row.Match.PropertyCandidates,row.Match.PostalAssertions,[],search);
        Assert.Equal(MatchQuality.Ambiguous,match.Quality);Assert.Equal(74,match.PostalAssertions.Count);Assert.Contains(match.PostalAssertions,p=>p.SourceRow==60428&&p.PostalCode=="530836");Assert.Null(match.MatchedFootprint);
    }
    [Fact]
    public void SyntheticCachesCannotMasqueradeAsAcquiredEvidence()
    {
        var query=new OneMapQuery("synthetic-unit-id","1","TEST AVE 1","1 TEST AVE 1");
        var path=Path.GetTempFileName();
        try {File.WriteAllText(path,JsonSerializer.Serialize(Search(Candidate())));Assert.Throws<InvalidDataException>(()=>OneMapEvidence.ReadCache(path,query));}
        finally {File.Delete(path);}
    }
    [Fact]
    public void FrozenAll416SyntheticEmptyResponsesConserveBaselineAndAccounting()
    {
        var dir=Path.Combine(AppContext.BaseDirectory,"coverage");var baseline=CsvImport.LoadDirectory(dir);
        var queries=OneMapEvidence.Queries(dir,baseline);Assert.Equal(378,queries.Queries.Count);Assert.Equal(OneMapEvidence.FrozenSampleSha256,queries.SampleSha256);
        var searches=queries.Queries.ToDictionary(q=>OneMapEvidence.AddressKey(q.Block,q.Road),q=>Search() with {QueryId=q.QueryId,Block=q.Block,Road=q.Road,Query=q.Query});
        var after=CsvImport.LoadDirectory(dir,searches);var report=OneMapComparison.Compare(baseline,after,queries,searches);
        Assert.False(report.AcquisitionComplete);Assert.Equal(416,report.Transitions["Unchanged"]);Assert.Equal(416,report.Transitions.Values.Sum());
        Assert.Equal(378,report.OneMapQueryOutcomes["NoCandidates"]);Assert.Equal(416,report.OneMapTransactionOutcomes["NoCandidates"]);
        Assert.Equal(345,report.WithOneMap.MatchQuality["Unmatched"]);Assert.Equal(1,report.WithOneMap.MatchQuality["Ambiguous"]);
        Assert.Equal(70,report.WithOneMap.CoordinateQuality["BlockApproximation"]);Assert.Equal(0,report.Transitions["AmbiguousToMatched"]);
    }
}
