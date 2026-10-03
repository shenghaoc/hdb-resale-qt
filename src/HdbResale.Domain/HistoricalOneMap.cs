using System.Text.Json;
using System.Text.RegularExpressions;
namespace HdbResale.Domain;

// A cached request and selected first hit, not a verified returned address or candidate set.
public sealed record HistoricalPostalAssertion(string CacheKey, string SearchValue, string Status,
    string? Postal, string? UpdatedAt);
public sealed record HistoricalProjection(string Origin, string RawSha256, int RawRows, string SampleSha256,
    string EvidenceStrength, IReadOnlyList<HistoricalPostalAssertion> Entries);
public sealed record HistoricalExperimentRow(string Id, TransactionFacts Facts, MatchQuality Before, MatchQuality ExperimentalAfter,
    CoordinateQuality BeforeCoordinates, CoordinateQuality ExperimentalCoordinates, AddressMatch Evidence);
public sealed record HistoricalExperimentReport(string EvidenceStrength, string RawSha256, string ProjectionSha256,
    CoverageReport Baseline, CoverageReport ExperimentalPostalAssisted, IReadOnlyDictionary<string,int> CacheOutcomes,
    IReadOnlyDictionary<string,int> Transitions, IReadOnlyList<HistoricalExperimentRow> AllRows,
    IReadOnlyList<HistoricalExperimentRow> AuditRequired, IReadOnlyList<HistoricalExperimentRow> RemainingUnmatchedAudit);
public static class HistoricalOneMap
{
    public const string RawSha256 = "8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4";
    private static string Normalize(string value) => string.Join(' ',value.ToUpperInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    public static string Key(TransactionFacts facts) => Regex.Replace($"{Normalize(facts.Town)}-{Normalize(facts.Block)}-{Normalize(facts.Street)}".ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
    public static string SearchValue(TransactionFacts facts) => $"{Normalize(facts.Block)} {Normalize(facts.Street)} SINGAPORE";
    public static bool Usable(TransactionFacts facts, HistoricalPostalAssertion assertion) => assertion.Status=="HistoricalFirstHit" &&
        assertion.CacheKey==Key(facts) && assertion.SearchValue==SearchValue(facts) && assertion.Postal is {Length:6} p && p.All(char.IsAsciiDigit);
    public static IReadOnlyDictionary<string, HistoricalPostalAssertion> ReadApprovedProjection(string path)
    {
        if (Hash(path) != "7d8af54d5cae591455e5161535218b66c9f85b9718d9a00d730602f72086ccd5")
            throw new InvalidDataException("Historical benchmark projection hash mismatch; no assertion used.");
        var projection = JsonSerializer.Deserialize<HistoricalProjection>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty historical projection.");
        if (projection.Origin != "HistoricalOneMapFirstHit" || projection.RawSha256 != RawSha256 ||
            projection.RawRows != 10333 || projection.SampleSha256 != OneMapEvidence.FrozenSampleSha256)
            throw new InvalidDataException("Wrong historical projection source/sample identity.");
        return projection.Entries.ToDictionary(e => e.CacheKey, StringComparer.Ordinal);
    }
    public static HistoricalExperimentReport Load(string sampleDirectory,string projectionPath,string originalFootprints)
    {
        if (Hash(projectionPath)!="7d8af54d5cae591455e5161535218b66c9f85b9718d9a00d730602f72086ccd5") throw new InvalidDataException("Historical benchmark projection hash mismatch.");
        var projection=JsonSerializer.Deserialize<HistoricalProjection>(File.ReadAllText(projectionPath)) ?? throw new InvalidDataException("Empty historical projection.");
        if (projection.Origin!="HistoricalOneMapFirstHit" || projection.RawSha256!=RawSha256 || projection.RawRows!=10333 || projection.SampleSha256!=OneMapEvidence.FrozenSampleSha256)
            throw new InvalidDataException("Wrong historical projection source/sample identity.");
        var baseline=CsvImport.LoadDirectory(sampleDirectory);OneMapEvidence.Queries(sampleDirectory,baseline);
        if (Hash(originalFootprints)!=OneMapComparison.FrozenFootprintSha256 || Hash(Path.Combine(sampleDirectory,"manifest.json"))!="c83187afa49e39f2d60e60575b6ce6e82ad877137bf6931007a1b165c510e4a1")
            throw new InvalidDataException("Frozen source/manifest mismatch.");
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(sampleDirectory,"manifest.json")));
        foreach (var f in manifest.RootElement.GetProperty("derived_sha256").EnumerateObject())
            if (Hash(Path.Combine(sampleDirectory,f.Name))!=f.Value.GetString()) throw new InvalidDataException("Frozen input mismatch.");
        var entries=projection.Entries.ToDictionary(e=>e.CacheKey,StringComparer.Ordinal);
        if (!entries.Keys.Order(StringComparer.Ordinal).SequenceEqual(baseline.Accepted.Select(t=>Key(t.Facts)).Distinct().Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Historical projection must contain exactly the benchmark identities.");
        foreach (var t in baseline.Accepted)
        {
            var e=entries[Key(t.Facts)];
            if (e.SearchValue!=SearchValue(t.Facts) || e.Status is not ("MissingKey" or "SearchMismatch" or "InvalidPostal" or "HistoricalFirstHit") ||
                (e.Status=="HistoricalFirstHit" && (!Usable(t.Facts,e) || !DateTimeOffset.TryParse(e.UpdatedAt,out _)))) throw new InvalidDataException("Malformed historical assertion.");
        }
        var fullBaseline=CsvImport.LoadDirectory(sampleDirectory,buildingEvidencePath:originalFootprints,supportMultiPolygon:false);
        if (!baseline.Accepted.Select(t=>(t.Id,t.Match.Quality,t.Location.Point)).SequenceEqual(fullBaseline.Accepted.Select(t=>(t.Id,t.Match.Quality,t.Location.Point))))
            throw new InvalidDataException("Full footprint source changes baseline.");
        var after=CsvImport.LoadDirectory(sampleDirectory,buildingEvidencePath:originalFootprints,historicalAssertions:entries,supportMultiPolygon:false);
        var rows=baseline.Accepted.Zip(after.Accepted).Select(p=>new HistoricalExperimentRow(p.First.Id,p.First.Facts,p.First.Match.Quality,p.Second.Match.Quality,p.First.Location.Quality,p.Second.Location.Quality,p.Second.Match)).ToArray();
        var transitions=rows.GroupBy(t=>$"{t.Before}→{t.ExperimentalAfter}").OrderBy(g=>g.Key,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count());
        var audit=rows.Where(t=>t.Before!=t.ExperimentalAfter || t.BeforeCoordinates!=t.ExperimentalCoordinates || t.ExperimentalAfter==MatchQuality.Ambiguous || t.Evidence.HistoricalOneMap?.Status is "SearchMismatch" or "InvalidPostal").ToArray();
        var remaining=rows.Where(t=>t.ExperimentalAfter==MatchQuality.Unmatched).GroupBy(t=>t.Facts.Month.Year).OrderBy(g=>g.Key).SelectMany(g=>g.OrderBy(t=>OneMapEvidence.QueryId("hdb-m5-audit-v1\0"+t.Id),StringComparer.Ordinal).Take(2)).ToArray();
        return new(projection.EvidenceStrength,RawSha256,Hash(projectionPath),CoverageStudy.Summarize(baseline),CoverageStudy.Summarize(after),
            entries.Values.GroupBy(e=>e.Status).ToDictionary(g=>g.Key,g=>g.Count()),transitions,rows,audit,remaining);
    }
    private static string Hash(string path) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
}
