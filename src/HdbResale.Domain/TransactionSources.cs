using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HdbResale.Domain;

// A source locator, not an official sale ID or a claim of historical location.
public sealed record TransactionProvenance(string SourceIdentity, string RawSha256, int SourceRow);

internal sealed record TransactionSource(string SourceIdentity, string Path, string Sha256, long Bytes,
    int Records, string RawSha256, long RawBytes, string[] Columns);
internal sealed record TransactionSourceManifest(string SchemaVersion, TransactionSource[] Sources);

internal static class TransactionSources
{
    internal const string ManifestName = "transaction-sources.json";
    internal static TransactionSource[] Read(string directory)
    {
        var path = System.IO.Path.Combine(directory, ManifestName);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Source manifest symlinks are not supported.");
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < 1 or > 1_048_576) throw new InvalidDataException("Source manifest must be 1 byte–1 MiB.");
        var bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw new InvalidDataException("Source manifest changed during reading.");
        var manifest = JsonSerializer.Deserialize<TransactionSourceManifest>(bytes,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true });
        if (manifest is null || manifest.SchemaVersion != "hdb-transaction-sources-v1" ||
            manifest.Sources is null || manifest.Sources.Length is < 1 or > 16)
            throw new InvalidDataException("Unsupported or empty transaction source manifest.");
        if (File.Exists(System.IO.Path.Combine(directory, "transactions.csv")))
            throw new InvalidDataException("Choose transaction sources or transactions.csv; both are present.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var source in manifest.Sources)
        {
            if (source is null || source.SourceIdentity is null || source.SourceIdentity.Length is < 1 or > 64 ||
                source.SourceIdentity.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-') ||
                !identities.Add(source.SourceIdentity)) throw new InvalidDataException("Invalid or duplicate source identity.");
            // One predictable local layout; no general URI/path resolver.
            if (source.Path != $"transactions/{source.SourceIdentity}.csv" || !paths.Add(source.Path))
                throw new InvalidDataException("Invalid or duplicate transaction source path.");
            if (!Hash(source.Sha256) || !Hash(source.RawSha256) || source.Bytes is < 1 or > 268_435_456 ||
                source.RawBytes is < 1 or > 268_435_456 || source.Records is < 1 or > 2_000_000 ||
                source.Columns is null || source.Columns.Length is < 1 or > 128 ||
                source.Columns.Any(c => c is null || c.Length > 256) ||
                source.Columns.Distinct(StringComparer.Ordinal).Count() != source.Columns.Length ||
                source.Columns[0] != "source_row") throw new InvalidDataException("Invalid source size, count, hash or columns.");
            total += source.Bytes;
            if (total > 536_870_912) throw new InvalidDataException("Transaction sources exceed 512 MiB.");
            foreach (var component in new[] { directory, System.IO.Path.Combine(directory, "transactions"),
                System.IO.Path.Combine(directory, source.Path) })
                if ((File.GetAttributes(component) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Transaction source symlinks are not supported.");
        }
        return manifest.Sources;
    }
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
    internal static void Verify(FileStream input, TransactionSource source)
    {
        if (input.Length != source.Bytes || Convert.ToHexStringLower(SHA256.HashData(input)) != source.Sha256)
            throw new InvalidDataException($"Transaction source integrity failed: {source.Path}.");
        input.Position = 0;
    }
}
