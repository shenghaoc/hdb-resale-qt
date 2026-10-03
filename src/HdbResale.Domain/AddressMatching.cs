using System.Text.Json.Serialization;
namespace HdbResale.Domain;

public enum MatchQuality { ExactAddress, NormalizedAddress, Ambiguous, Unmatched }
public sealed record PropertyAddress(long SourceRow, string Block, string Street);
public sealed record PostalAddress(long SourceRow, string Block, string Street, string PostalCode);
public sealed record FootprintIdentity(int ObjectId, int EntityId, string Block, string PostalCode);
public sealed record FootprintRecord(FootprintIdentity Identity, DerivedLocation Location);
public sealed record AddressMatch(MatchQuality Quality, string Reason,
    IReadOnlyList<PropertyAddress> PropertyCandidates, IReadOnlyList<PostalAddress> PostalAssertions,
    IReadOnlyList<FootprintRecord> FootprintCandidates)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OneMapAssessment? OneMap { get; init; }
    public bool IsMatched => Quality is MatchQuality.ExactAddress or MatchQuality.NormalizedAddress;
    public FootprintRecord? MatchedFootprint => IsMatched && FootprintCandidates.Count == 1
        ? FootprintCandidates[0] : null;
}

public static class AddressNormalizer
{
    public static string Block(string value) => value.Trim().ToUpperInvariant();
    public static string Street(string value)
    {
        var tokens = value.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        // Only the two observed road tokens, immediately before a numeric suffix.
        // Keep punctuation, block suffixes and number spelling; no fuzzy matching.
        if (tokens.Length > 1 && tokens[^1].All(char.IsAsciiDigit))
            tokens[^2] = tokens[^2] switch { "AVE" => "AVENUE", "CTRL" => "CENTRAL", _ => tokens[^2] };
        return string.Join(' ', tokens);
    }
}

public static class AddressMatcher
{
    public static AddressMatch Match(TransactionFacts facts, IReadOnlyList<PropertyAddress> properties,
        IReadOnlyList<PostalAddress> postalAddresses, IReadOnlyList<FootprintRecord> footprints, OneMapSearch? oneMapSearch = null)
    {
        var propertyMatches = Array.AsReadOnly(properties.Where(p => SameAddress(facts, p.Block, p.Street)).ToArray());
        var assertions = Array.AsReadOnly(postalAddresses.Where(p => SameAddress(facts, p.Block, p.Street)).ToArray());
        IReadOnlyList<FootprintRecord> candidates = Array.Empty<FootprintRecord>();
        var oneMap = oneMapSearch is null ? null : OneMapEvidence.Assess(facts, oneMapSearch);
        AddressMatch Result(MatchQuality quality, string reason) => new(quality, reason, propertyMatches, assertions, candidates) { OneMap = oneMap };
        if (propertyMatches.Count == 0) return Result(MatchQuality.Unmatched, "No HDB property address matches block and street.");
        if (propertyMatches.Count > 1) return Result(MatchQuality.Ambiguous, $"{propertyMatches.Count} HDB property records match; no record selected.");

        // Corporate records are assertions, not candidate buildings. Preserve every
        // source row; agreement on one postal value does not pick an entity winner.
        var postalCodes = assertions.Select(p => p.PostalCode).Distinct(StringComparer.Ordinal).ToArray();
        if (postalCodes.Length > 1) return Result(MatchQuality.Ambiguous, $"ACRA address assertions conflict: {postalCodes.Length} postal candidates.");
        if (oneMap?.Outcome is OneMapOutcome.PostalConflict or OneMapOutcome.MultipleCandidates)
            return Result(MatchQuality.Ambiguous, "OneMap has distinct plausible postal/building candidates; no winner selected.");
        if (oneMap?.Postal is { } oneMapPostal)
        {
            if (postalCodes.Length == 1 && postalCodes[0] != oneMapPostal)
                return Result(MatchQuality.Ambiguous, "ACRA and OneMap postal assertions contradict; neither source overrides the other.");
            if (postalCodes.Length == 0) postalCodes = [oneMapPostal];
        }
        if (postalCodes.Length == 0) return Result(MatchQuality.Unmatched, "HDB property matched; no ACRA address/postal corroboration." +
            (oneMap is null ? "" : $" OneMap outcome: {oneMap.Outcome}."));
        candidates = Array.AsReadOnly(footprints.Where(p => p.Identity.PostalCode == postalCodes[0] &&
            AddressNormalizer.Block(p.Identity.Block) == AddressNormalizer.Block(facts.Block)).ToArray());
        if (candidates.Count == 0) return Result(MatchQuality.Unmatched, "Property and postal address corroborated; no footprint matches both postal code and block.");
        if (candidates.Count > 1) return Result(MatchQuality.Ambiguous, $"{candidates.Count} footprints match postal code and block; no footprint selected.");
        var exact = propertyMatches.All(p => p.Block == facts.Block && p.Street == facts.Street) &&
            assertions.All(p => p.Block == facts.Block && p.Street == facts.Street) && candidates[0].Identity.Block == facts.Block &&
            (oneMap?.Outcome != OneMapOutcome.UniqueCandidate || oneMap.MatchingCandidates.All(p => p.Block == facts.Block && p.Road == facts.Street));
        return Result(exact ? MatchQuality.ExactAddress : MatchQuality.NormalizedAddress,
            oneMap?.Outcome == OneMapOutcome.UniqueCandidate
            ? $"One HDB property address + {assertions.Count} retained ACRA assertions + {oneMap.MatchingCandidates.Count} agreeing OneMap candidate records + one HDB footprint on block/postal. Search evidence is not historical ground truth."
            : $"One HDB property address + {assertions.Count} agreeing ACRA postal assertions + one footprint on block/postal. " +
            "Official-record corroboration, not an authoritative HDB ID link.");
    }
    private static bool SameAddress(TransactionFacts facts, string block, string street) =>
        AddressNormalizer.Block(facts.Block) == AddressNormalizer.Block(block) &&
        AddressNormalizer.Street(facts.Street) == AddressNormalizer.Street(street);
}
