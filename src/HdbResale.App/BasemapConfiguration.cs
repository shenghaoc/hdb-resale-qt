namespace HdbResale.App;

// Explicit local test opt-in; ordinary launches keep the production provider/cache.
internal sealed record BasemapConfiguration(string TileEndpoint, string CacheDirectory)
{
    internal const string DefaultTileEndpoint = "https://www.onemap.gov.sg/maps/tiles/Default/";

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
        var production = Path.TrimEndingDirectorySeparator(Path.GetFullPath(defaultCacheDirectory));
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (cache.Equals(production, comparison) ||
            cache.StartsWith(production + Path.DirectorySeparatorChar, comparison) ||
            production.StartsWith(cache + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("Tile test cache must be separate from the production tile cache.");
        return new(uri.AbsoluteUri, cache);
    }
}
