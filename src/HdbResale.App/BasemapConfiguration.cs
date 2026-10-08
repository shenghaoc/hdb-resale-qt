namespace HdbResale.App;

// Explicit local test opt-in; ordinary launches keep the production provider/cache.
internal sealed record BasemapConfiguration(string TileEndpoint, string CacheDirectory)
{
    internal const string DefaultTileEndpoint = "https://www.onemap.gov.sg/maps/tiles/Default/";
    // More links than this along one path is treated as a loop, as operating systems do.
    private const int MaximumLinks = 40;

    internal static BasemapConfiguration FromEnvironment() => Create(
        Environment.GetEnvironmentVariable("HDB_TILE_TEST") == "1",
        Environment.GetEnvironmentVariable("HDB_TEST_TILE_ENDPOINT"),
        Environment.GetEnvironmentVariable("HDB_TEST_TILE_CACHE_DIRECTORY"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HdbResaleQt", "onemap-default-v1"));

    internal static BasemapConfiguration Create(bool testEnabled, string? endpoint, string? cacheDirectory, string defaultCacheDirectory)
    {
        if (!testEnabled) return new(DefaultTileEndpoint, defaultCacheDirectory);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || !uri.AbsolutePath.EndsWith('/'))
            throw new ArgumentException("Tile tests require a loopback HTTP(S) endpoint ending in / without credentials, query or fragment.");
        if (string.IsNullOrWhiteSpace(cacheDirectory) || !Path.IsPathFullyQualified(cacheDirectory))
            throw new ArgumentException("Tile tests require an absolute, isolated cache directory.");
        var cache = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cacheDirectory));
        // Compare where the directories really are: a symbolic link or junction could otherwise give the
        // production cache a second, lexically separate name.
        var physicalCache = Physical(cache);
        var production = Physical(defaultCacheDirectory);
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (physicalCache.Equals(production, comparison) ||
            physicalCache.StartsWith(production + Path.DirectorySeparatorChar, comparison) ||
            production.StartsWith(physicalCache + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("Tile test cache must be separate from the production tile cache.");
        return new(uri.AbsoluteUri, cache);
    }

    // The absolute path with every existing symbolic link or junction along it replaced by its target,
    // including links inside those targets. Components that do not exist yet are kept as written.
    internal static string Physical(string path)
    {
        var current = Path.GetPathRoot(Path.GetFullPath(path))!;
        var remaining = new Queue<string>(Components(path));
        var links = 0;
        while (remaining.TryDequeue(out var component))
        {
            var next = Path.Combine(current, component);
            FileSystemInfo entry = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            FileSystemInfo? target;
            try { target = entry.LinkTarget is null ? null : entry.ResolveLinkTarget(returnFinalTarget: false); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { throw new ArgumentException("Tile test cache path could not be resolved: " + e.Message, e); }
            if (target is null) { current = next; continue; }
            if (++links > MaximumLinks) throw new ArgumentException("Tile test cache path has too many links.");
            // Walk the target from its root as well, then continue with what followed the link.
            remaining = new Queue<string>(Components(target.FullName).Concat(remaining));
            current = Path.GetPathRoot(Path.GetFullPath(target.FullName))!;
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    private static string[] Components(string path)
    {
        var full = Path.GetFullPath(path);
        return full[Path.GetPathRoot(full)!.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
    }
}
