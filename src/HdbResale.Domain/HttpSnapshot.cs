using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HdbResale.Domain;

// One concrete static HTTP pack contract. No database, source API or UI state.
public static class HttpSnapshot
{
    private sealed record Current(string SchemaVersion, string ManifestSha256);
    private sealed record Pack(string SchemaVersion, int ImporterVersion, Entry[] Files);
    private sealed record Entry(string Path, string Sha256, long Bytes, string GzipSha256, long GzipBytes);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
    private static readonly string[] Required = [TransactionSources.ManifestName, "address-evidence.csv",
        "postal-address-evidence.csv", "building-evidence.geojson"];
    private static readonly string[] Optional = ["address-normalization.txt"];
    private const long MaxFile = 268_435_456, MaxTotal = 805_306_368;

    public static async Task<string> SynchronizeAsync(Uri root, string cache, CancellationToken cancellation = default)
    {
        if (!root.IsAbsoluteUri || root.Scheme is not ("http" or "https") ||
            root.Scheme == "http" && !root.IsLoopback || root.UserInfo.Length != 0 ||
            root.Query.Length != 0 || root.Fragment.Length != 0 || !root.AbsolutePath.EndsWith('/'))
            throw new ArgumentException("Snapshot root must be an HTTPS directory, or loopback HTTP directory.", nameof(root));
        cache = Path.GetFullPath(cache);
        Directory.CreateDirectory(cache);
        NoLinks(cache);
        var lockPath = Path.Combine(cache, ".sync.lock");
        NoLinks(lockPath);
        using var cacheLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(10));
        var token = deadline.Token;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None, UseCookies = false }) { Timeout = TimeSpan.FromMinutes(2) };
        var currentBytes = await GetSmall(client, new(root, "current.json"), 4096, token);
        var current = ReadCurrent(currentBytes);
        var manifest = await GetSmall(client, new(root, $"manifests/{current.ManifestSha256}.json"), 1_048_576, token);
        if (Digest(manifest) != current.ManifestSha256) throw new InvalidDataException("Snapshot manifest integrity failed.");
        var pack = ReadPack(manifest);
        var snapshots = Path.Combine(cache, "snapshots");
        Directory.CreateDirectory(snapshots);
        NoLinks(snapshots);
        var complete = Path.Combine(snapshots, current.ManifestSha256);
        NoLinks(complete);
        string? stage = null;
        try
        {
            if (!Directory.Exists(complete))
            {
                stage = Path.Combine(snapshots, ".download-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                foreach (var entry in pack.Files)
                {
                    token.ThrowIfCancellationRequested();
                    var compressed = Path.Combine(stage, ".object.gz");
                    using (var response = await client.GetAsync(new Uri(root, $"objects/{entry.GzipSha256}.gz"),
                        HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        response.EnsureSuccessStatusCode();
                        CheckLength(response, entry.GzipBytes);
                        await using var input = await response.Content.ReadAsStreamAsync(token);
                        await using var output = new FileStream(compressed, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        await CopyVerified(input, output, entry.GzipBytes, entry.GzipSha256, token);
                        output.Flush(true);
                    }
                    var target = Path.Combine(stage, entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using (var input = File.OpenRead(compressed))
                    await using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                    await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        await CopyVerified(gzip, output, entry.Bytes, entry.Sha256, token);
                        output.Flush(true);
                    }
                    File.Delete(compressed);
                }
                File.WriteAllBytes(Path.Combine(stage, "snapshot.json"), manifest);
                ValidateImport(stage, pack);
            }
            else VerifyFiles(complete, pack, current.ManifestSha256);
            // A changing discovery pointer never activates the downloaded older
            // generation. A complete previous snapshot stays usable on failure.
            var after = ReadCurrent(await GetSmall(client, new(root, "current.json"), 4096, token));
            if (after.ManifestSha256 != current.ManifestSha256)
                throw new InvalidDataException("Current snapshot changed during download; retry explicitly.");
            token.ThrowIfCancellationRequested();
            if (stage is not null) { Directory.Move(stage, complete); stage = null; }
            var pointer = Path.Combine(cache, ".active-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var output = new FileStream(pointer, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(currentBytes); output.Flush(true); }
                NoLinks(Path.Combine(cache, "active.json"));
                File.Move(pointer, Path.Combine(cache, "active.json"), overwrite: true);
            }
            finally { if (File.Exists(pointer)) File.Delete(pointer); }
            return complete;
        }
        finally { if (stage is not null) Directory.Delete(stage, recursive: true); }
    }

    public static string ActiveDirectory(string cache)
    {
        cache = Path.GetFullPath(cache);
        NoLinks(cache);
        var pointer = Path.Combine(cache, "active.json");
        NoLinks(pointer);
        var current = ReadCurrent(ReadSmall(pointer, 4096));
        var directory = Path.Combine(cache, "snapshots", current.ManifestSha256);
        NoLinks(Path.Combine(cache, "snapshots"));
        NoLinks(directory);
        var path = Path.Combine(directory, "snapshot.json");
        NoLinks(path);
        var manifest = ReadSmall(path, 1_048_576);
        if (Digest(manifest) != current.ManifestSha256) throw new InvalidDataException("Cached manifest integrity failed.");
        VerifyFiles(directory, ReadPack(manifest), current.ManifestSha256);
        return directory;
    }

    private static Current ReadCurrent(byte[] bytes)
    {
        var value = JsonSerializer.Deserialize<Current>(bytes, Json);
        if (value is null || value.SchemaVersion != "hdb-snapshot-current-v1" || !Hash(value.ManifestSha256))
            throw new InvalidDataException("Unsupported snapshot current pointer.");
        return value;
    }
    private static Pack ReadPack(byte[] bytes)
    {
        var pack = JsonSerializer.Deserialize<Pack>(bytes, Json);
        if (pack is null || pack.SchemaVersion != "hdb-desktop-snapshot-v1" || pack.ImporterVersion is not (1 or 2) ||
            pack.Files is null || pack.Files.Length is < 5 or > 21)
            throw new InvalidDataException("Unsupported snapshot/importer version or file count.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0, compressed = 0;
        foreach (var file in pack.Files)
        {
            if (file is null || file.Path is null || !paths.Add(file.Path) || !Allowed(file.Path) ||
                !Hash(file.Sha256) || !Hash(file.GzipSha256) || file.Bytes is < 1 or > MaxFile ||
                file.GzipBytes is < 1 or > MaxFile) throw new InvalidDataException("Invalid snapshot file declaration.");
            total += file.Bytes; compressed += file.GzipBytes;
            if (total > MaxTotal || compressed > MaxTotal) throw new InvalidDataException("Snapshot exceeds 768 MiB.");
        }
        if (Required.Any(path => !pack.Files.Any(f => f.Path == path)))
            throw new InvalidDataException("Required snapshot evidence is missing.");
        return pack;
    }
    private static bool Allowed(string path)
    {
        if (Required.Contains(path) || Optional.Contains(path)) return true;
        return path.StartsWith("transactions/", StringComparison.Ordinal) && path.EndsWith(".csv", StringComparison.Ordinal) &&
            path[13..^4] is { Length: >= 1 and <= 64 } identity &&
            identity.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    }
    private static void ValidateImport(string directory, Pack pack)
    {
        var sources = TransactionSources.Read(directory);
        var expected = sources.Select(s => s.Path).Concat(Required).Concat(Optional.Where(name => File.Exists(Path.Combine(directory, name))));
        if (!expected.Order(StringComparer.Ordinal).SequenceEqual(pack.Files.Select(f => f.Path).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Snapshot inventory differs from source descriptor.");
        var imported = CsvImport.LoadDirectory(directory);
        if (imported.Accepted.Count != sources.Sum(s => (long)s.Records) ||
            imported.Diagnostics.Any(d => d.File == TransactionSources.ManifestName))
            throw new InvalidDataException("Snapshot transaction import did not accept the declared cohort.");
        // Existing evidence-quality diagnostics remain explicit in the buyer
        // import; they are not turned into coordinates or silently repaired.
    }
    private static void VerifyFiles(string directory, Pack pack, string manifestSha256)
    {
        var manifestPath = Path.Combine(directory, "snapshot.json");
        NoLinks(manifestPath);
        if (Digest(ReadSmall(manifestPath, 1_048_576)) != manifestSha256)
            throw new InvalidDataException("Cached manifest integrity failed.");
        var expected = pack.Files.Select(f => f.Path).Append("snapshot.json").Order(StringComparer.Ordinal);
        var subdirectories = Directory.GetDirectories(directory);
        if (subdirectories.Any(path => Path.GetFileName(path) != "transactions"))
            throw new InvalidDataException("Unexpected snapshot directory.");
        var transactionDirectory = Path.Combine(directory, "transactions");
        NoLinks(transactionDirectory);
        if (Directory.Exists(transactionDirectory) && Directory.GetDirectories(transactionDirectory).Length != 0)
            throw new InvalidDataException("Unexpected nested snapshot directory.");
        var actual = Directory.EnumerateFiles(directory).Concat(Directory.Exists(transactionDirectory)
            ? Directory.EnumerateFiles(transactionDirectory) : [])
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/')).Order(StringComparer.Ordinal);
        if (!expected.SequenceEqual(actual)) throw new InvalidDataException("Cached snapshot inventory differs from manifest.");
        foreach (var entry in pack.Files)
        {
            var path = Path.Combine(directory, entry.Path);
            NoLinks(Path.GetDirectoryName(path)!);
            NoLinks(path);
            using var input = File.OpenRead(path);
            if (input.Length != entry.Bytes || Convert.ToHexStringLower(SHA256.HashData(input)) != entry.Sha256)
                throw new InvalidDataException($"Cached snapshot integrity failed: {entry.Path}.");
        }
    }
    private static void NoLinks(string path)
    {
        var item = new FileInfo(path);
        if (item.LinkTarget is not null || Path.Exists(path) && (item.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Snapshot cache symlinks are not supported.");
    }
    private static byte[] ReadSmall(string path, int maximum)
    {
        using var input = File.OpenRead(path);
        if (input.Length is < 1 || input.Length > maximum) throw new InvalidDataException("Snapshot metadata size exceeded.");
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes);
        if (input.ReadByte() != -1) throw new InvalidDataException("Snapshot metadata changed during reading.");
        return bytes;
    }
    private static async Task<byte[]> GetSmall(HttpClient client, Uri uri, int maximum, CancellationToken token)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && (length < 1 || length > maximum))
            throw new InvalidDataException("Snapshot metadata size exceeded.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        { if (output.Length + read > maximum) throw new InvalidDataException("Snapshot metadata size exceeded."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    private static void CheckLength(HttpResponseMessage response, long expected)
    {
        if (response.Content.Headers.ContentLength is { } length && length != expected)
            throw new InvalidDataException("Snapshot object length differs from manifest.");
    }
    private static async Task CopyVerified(Stream input, Stream output, long expected, string sha256, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536]; long bytes = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            bytes += read;
            if (bytes > expected) throw new InvalidDataException("Snapshot object exceeds declared length.");
            hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
        if (bytes != expected || Convert.ToHexStringLower(hash.GetHashAndReset()) != sha256)
            throw new InvalidDataException("Snapshot object integrity failed.");
    }
    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
}
