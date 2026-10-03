using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace HdbResale.Domain;

public enum OneMapOutcome { UniqueCandidate, NoCandidates, MultipleCandidates, PostalConflict, AddressConflict, ApiError }
public sealed record OneMapQuery(string QueryId, string Block, string Road, string Query);
public sealed record OneMapQuerySet(string SampleSha256, IReadOnlyList<OneMapQuery> Queries);
public sealed record OneMapCandidate(int Page, int Index, string Block, string Road, string Postal, string Building);
public sealed record OneMapPageHash(int Page, string Sha256);
public sealed record OneMapApiFailure(string Kind, string? Cause, int? HttpStatus, int Page);
public sealed record OneMapSearch(string QueryId, string Block, string Road, string Query, string Origin,
    string SourceUrl, string RetrievedUtc, int Found, int TotalPages, IReadOnlyList<OneMapCandidate> Candidates,
    IReadOnlyList<OneMapPageHash> Pages, OneMapApiFailure? Error);
public sealed record OneMapRejection(OneMapCandidate Candidate, string Reason);
public sealed record OneMapAssessment(OneMapOutcome Outcome, OneMapSearch Search,
    IReadOnlyList<OneMapCandidate> MatchingCandidates, IReadOnlyList<OneMapRejection> RejectedCandidates)
{
    public string? Postal => Outcome == OneMapOutcome.UniqueCandidate ? MatchingCandidates[0].Postal : null;
}

public static class OneMapEvidence
{
    public const string Endpoint = "https://www.onemap.gov.sg/api/common/elastic/search";
    public const string FrozenSampleSha256 = "875aceb10230d4a52ea6159397cd40a69cbe81346094f6bb1a81423c6e1b5b76";
    public static string QueryId(string query) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(query)));
    public static string AddressKey(string block, string road) => AddressNormalizer.Block(block) + "\0" + AddressNormalizer.Street(road);
    public static OneMapQuerySet Queries(string directory, ImportResult baseline)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "transactions.csv"))));
        if (hash != FrozenSampleSha256 || baseline.Accepted.Count != 416 || baseline.Diagnostics.Count != 0 || baseline.Rejected.Count != 0)
            throw new InvalidDataException("OneMap study requires the unchanged, valid M4 golden sample.");
        var queries = baseline.Accepted.GroupBy(t => AddressKey(t.Facts.Block,t.Facts.Street),StringComparer.Ordinal)
            .OrderBy(g => g.Key,StringComparer.Ordinal).Select(g =>
            {
                var f = g.First().Facts; var query = f.Block + " " + f.Street;
                return new OneMapQuery(QueryId(query), f.Block, f.Street, query);
            }).ToArray();
        return new(hash, Array.AsReadOnly(queries));
    }
    public static OneMapSearch ReadCache(string path, OneMapQuery expected)
    {
        var search = JsonSerializer.Deserialize<OneMapSearch>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty OneMap cache record.");
        if (search.QueryId != expected.QueryId || search.Query != expected.Query || search.Block != expected.Block || search.Road != expected.Road ||
            search.SourceUrl != Endpoint || search.Origin != "OneMapSearch" ||
            !DateTimeOffset.TryParse(search.RetrievedUtc, out _) || search.Candidates is null || search.Pages is null || search.Found < 0 || search.TotalPages < 0)
            throw new InvalidDataException("OneMap cache identity/source/metadata is invalid. Synthetic records cannot be consumed as acquired evidence.");
        if (search.Pages.Any(p => p.Sha256 is null || p.Sha256.Length != 64 || !p.Sha256.All(char.IsAsciiHexDigit)))
            throw new InvalidDataException("Invalid OneMap response hash.");
        if (search.Error is null && (search.Found != search.Candidates.Count || (search.Found == 0 && search.TotalPages != 0) ||
            search.Candidates.Select(c => (c.Page,c.Index)).Distinct().Count() != search.Candidates.Count ||
            !search.Pages.Select(p => p.Page).SequenceEqual(Enumerable.Range(1, Math.Max(1,search.TotalPages))) ||
            search.Candidates.Any(c => c.Page < 1 || c.Page > search.TotalPages || c.Index < 1 ||
                c.Block is null || c.Road is null || c.Postal is null || c.Building is null)))
            throw new InvalidDataException("OneMap cache has incomplete pagination or malformed candidates.");
        return search;
    }
    public static OneMapAssessment Assess(TransactionFacts facts, OneMapSearch search)
    {
        if (search.Error is not null) return new(OneMapOutcome.ApiError,search,[],[]);
        var matching = new List<OneMapCandidate>(); var rejected = new List<OneMapRejection>();
        foreach (var c in search.Candidates)
        {
            string? reason = null;
            if (AddressKey(c.Block,c.Road) != AddressKey(facts.Block,facts.Street)) reason = "Block/road do not agree with the HDB address under existing normalization.";
            else if (c.Postal.Length != 6 || !c.Postal.All(char.IsAsciiDigit)) reason = "Postal is not a usable six-digit code.";
            if (reason is null) matching.Add(c); else rejected.Add(new(c,reason));
        }
        OneMapOutcome outcome;
        if (AddressKey(search.Block,search.Road) != AddressKey(facts.Block,facts.Street)) outcome = OneMapOutcome.AddressConflict;
        else if (search.Candidates.Count == 0) outcome = OneMapOutcome.NoCandidates;
        else if (matching.Count == 0) outcome = OneMapOutcome.AddressConflict;
        else if (matching.Select(c => c.Postal).Distinct(StringComparer.Ordinal).Count() > 1) outcome = OneMapOutcome.PostalConflict;
        else if (matching.Select(c => string.Join(' ',c.Building.ToUpperInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)))
            .Distinct(StringComparer.Ordinal).Count() > 1) outcome = OneMapOutcome.MultipleCandidates;
        else outcome = OneMapOutcome.UniqueCandidate;
        return new(outcome,search,matching.AsReadOnly(),rejected.AsReadOnly());
    }
}
