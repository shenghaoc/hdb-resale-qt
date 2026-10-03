using System.Security.Cryptography;
using System.Text.Json;
namespace HdbResale.Domain;

public sealed record OneMapTransition(string Id, string Town, string Month, string Block, string Street,
    MatchQuality Before, MatchQuality After, CoordinateQuality BeforeCoordinates, CoordinateQuality AfterCoordinates, AddressMatch Evidence);
public sealed record OneMapComparisonReport(bool AcquisitionComplete, int QueryCount, CoverageReport Baseline, CoverageReport WithOneMap,
    IReadOnlyDictionary<string,int> OneMapQueryOutcomes, IReadOnlyDictionary<string,int> OneMapTransactionOutcomes,
    IReadOnlyDictionary<string,int> Transitions, IReadOnlyList<OneMapTransition> AuditRequired,
    IReadOnlyList<OneMapTransition> RemainingUnmatchedAudit);
public static class OneMapComparison
{
    public const string FrozenFootprintSha256 = "7c987511548de3a82da403cabca02702031e02ade8703a9e401417883dfeb702";
    public static OneMapComparisonReport Load(string sampleDirectory, string cacheDirectory, string originalFootprints)
    {
        // Frozen inputs and full original source, never a new sample or OneMap coordinates.
        if (Hash(Path.Combine(sampleDirectory,"manifest.json")) != "c83187afa49e39f2d60e60575b6ce6e82ad877137bf6931007a1b165c510e4a1") throw new InvalidDataException("Frozen M4 manifest hash mismatch.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(sampleDirectory,"manifest.json")));
        foreach (var file in manifest.RootElement.GetProperty("derived_sha256").EnumerateObject())
            if (Hash(Path.Combine(sampleDirectory,file.Name)) != file.Value.GetString()) throw new InvalidDataException("Frozen sample input hash mismatch: " + file.Name);
        if (Hash(originalFootprints) != FrozenFootprintSha256) throw new InvalidDataException("Original HDB footprint snapshot hash mismatch.");
        var baseline = CsvImport.LoadDirectory(sampleDirectory);
        var queries = OneMapEvidence.Queries(sampleDirectory,baseline);
        var searches = queries.Queries.ToDictionary(q => OneMapEvidence.AddressKey(q.Block,q.Road),
            q => OneMapEvidence.ReadCache(Path.Combine(cacheDirectory,q.QueryId+".json"),q),StringComparer.Ordinal);
        var fullBaseline = CsvImport.LoadDirectory(sampleDirectory,buildingEvidencePath:originalFootprints);
        if (!baseline.Accepted.Select(t => (t.Id,t.Match.Quality,t.Location.Point)).SequenceEqual(fullBaseline.Accepted.Select(t => (t.Id,t.Match.Quality,t.Location.Point))))
            throw new InvalidDataException("Full frozen footprint source changes baseline; investigate before comparison.");
        var after = CsvImport.LoadDirectory(sampleDirectory,searches,originalFootprints);
        return Compare(baseline,after,queries,searches);
    }
    public static OneMapComparisonReport Compare(ImportResult baseline, ImportResult after, OneMapQuerySet queries,
        IReadOnlyDictionary<string,OneMapSearch> searches)
    {
        if (!baseline.Accepted.Select(t => t.Id).SequenceEqual(after.Accepted.Select(t => t.Id))) throw new InvalidDataException("Comparison lost/reordered golden transactions.");
        var transitions = baseline.Accepted.Zip(after.Accepted).Select(p => new OneMapTransition(p.First.Id,p.First.Town,p.First.Facts.Month.ToString(),
            p.First.Facts.Block,p.First.Facts.Street,p.First.Match.Quality,p.Second.Match.Quality,p.First.Location.Quality,p.Second.Location.Quality,p.Second.Match)).ToArray();
        var counts = new Dictionary<string,int>
        {
            ["UnmatchedToMatched"] = transitions.Count(t => t.Before==MatchQuality.Unmatched && t.Evidence.IsMatched),
            ["UnmatchedToAmbiguous"] = transitions.Count(t => t.Before==MatchQuality.Unmatched && t.After==MatchQuality.Ambiguous),
            ["AmbiguousToMatched"] = transitions.Count(t => t.Before==MatchQuality.Ambiguous && t.Evidence.IsMatched),
            ["MatchedToAmbiguous"] = transitions.Count(t => t.Before is MatchQuality.ExactAddress or MatchQuality.NormalizedAddress && t.After==MatchQuality.Ambiguous),
            ["MatchedToUnmatched"] = transitions.Count(t => t.Before is MatchQuality.ExactAddress or MatchQuality.NormalizedAddress && t.After==MatchQuality.Unmatched),
            ["Unchanged"] = transitions.Count(t => t.Before==t.After && t.BeforeCoordinates==t.AfterCoordinates)
        };
        counts["Other"] = transitions.Length-counts.Values.Sum();
        var audit = transitions.Where(t => t.Before!=t.After || t.BeforeCoordinates!=t.AfterCoordinates || t.After==MatchQuality.Ambiguous ||
            t.Evidence.OneMap?.Outcome is OneMapOutcome.PostalConflict or OneMapOutcome.MultipleCandidates or OneMapOutcome.ApiError).ToArray();
        // Fixed deterministic remaining-unmatched audit; all changed/conflicting rows are separate.
        var remaining = transitions.Where(t => t.Before==MatchQuality.Unmatched && t.After==MatchQuality.Unmatched)
            .GroupBy(t => t.Month[..4]).OrderBy(g => g.Key,StringComparer.Ordinal)
            .SelectMany(g => g.OrderBy(t => OneMapEvidence.QueryId("hdb-m5-audit-v1\0"+t.Id),StringComparer.Ordinal).Take(2)).ToArray();
        var queryOutcomes = Enum.GetValues<OneMapOutcome>().ToDictionary(q => q.ToString(),q => searches.Values.Count(s =>
        {
            var fact = baseline.Accepted.First(t => OneMapEvidence.AddressKey(t.Facts.Block,t.Facts.Street)==OneMapEvidence.AddressKey(s.Block,s.Road)).Facts;
            return OneMapEvidence.Assess(fact,s).Outcome==q;
        }));
        var transactionOutcomes = Enum.GetValues<OneMapOutcome>().ToDictionary(q => q.ToString(),q => after.Accepted.Count(t => t.Match.OneMap?.Outcome==q));
        var complete = searches.Count==queries.Queries.Count && searches.Values.All(s => s.Origin=="OneMapSearch" && s.Error is null);
        return new(complete,queries.Queries.Count,CoverageStudy.Summarize(baseline),CoverageStudy.Summarize(after),queryOutcomes,transactionOutcomes,counts,
            Array.AsReadOnly(audit),Array.AsReadOnly(remaining));
    }
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
