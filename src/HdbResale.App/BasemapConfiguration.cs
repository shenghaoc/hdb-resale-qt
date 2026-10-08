using System.Runtime.InteropServices;
using System.Text;

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
        var physicalCache = Physical(cacheDirectory);
        var production = Physical(defaultCacheDirectory);
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (Within(physicalCache, production, comparison) || Within(production, physicalCache, comparison))
            throw new ArgumentException("Tile test cache must be separate from the production tile cache.");
        return new(uri.AbsoluteUri, cache);
    }

    // The absolute path with every existing symbolic link or junction along it replaced by its target,
    // including links inside those targets. Components are taken in filesystem order from each link's own
    // text, so "." and ".." apply to the physical directory reached so far, as the operating system applies
    // them, rather than being collapsed lexically first. Components that do not exist yet are kept.
    internal static string Physical(string path)
    {
        var absolute = Path.IsPathFullyQualified(path) ? path : Path.Combine(Environment.CurrentDirectory, path);
        var current = Path.GetPathRoot(absolute)!;
        var remaining = new Queue<string>(Components(absolute[current.Length..]));
        var links = 0;
        while (remaining.TryDequeue(out var component))
        {
            if (component == ".") continue;
            if (component == "..") { current = Path.GetDirectoryName(current) ?? current; continue; }
            var next = Path.Combine(current, component);
            FileSystemInfo entry = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
            string? target;
            try { target = entry.LinkTarget; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { throw new ArgumentException("Tile test cache path could not be resolved: " + e.Message, e); }
            if (target is null) { current = LongName(next); continue; }
            if (++links > MaximumLinks) throw new ArgumentException("Tile test cache path has too many links.");
            // A fully qualified target starts again from its root and a relative one continues from the link's
            // directory. A Windows root-relative target ("\\dir") stays on the link's volume; a drive-relative
            // one ("C:dir") depends on per-drive state and is refused.
            if (Path.IsPathFullyQualified(target))
            {
                current = Path.GetPathRoot(target)!;
                target = target[current.Length..];
            }
            else if (Path.IsPathRooted(target))
            {
                var prefix = Path.GetPathRoot(target)!;
                if (prefix.Length == 0 || prefix[0] is not ('\\' or '/'))
                    throw new ArgumentException("Tile test cache path has a drive-relative link: " + target);
                current = Path.GetPathRoot(current)!;
                target = target[prefix.Length..];
            }
            remaining = new Queue<string>(Components(target).Concat(remaining));
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    // Windows can also name an existing directory by its 8.3 short name (ONEMAP~1); use the long name the
    // filesystem stores, so an alias cannot pass for a separate directory. Elsewhere names are already unique.
    private static string LongName(string path)
    {
        if (!OperatingSystem.IsWindows() || !Path.Exists(path)) return path;
        var buffer = new StringBuilder(260);
        var length = GetLongPathNameW(path, buffer, (uint)buffer.Capacity);
        if (length > buffer.Capacity)
        {
            buffer.EnsureCapacity((int)length);
            length = GetLongPathNameW(path, buffer, (uint)buffer.Capacity);
        }
        if (length == 0 || length > buffer.Capacity)
            throw new ArgumentException("Tile test cache path could not be resolved: " + path);
        return buffer.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathNameW(string shortPath, StringBuilder longPath, uint length);

    // Whether path is ancestor or lies inside it, component by component; a root keeps its own separator.
    private static bool Within(string path, string ancestor, StringComparison comparison) =>
        path.Equals(ancestor, comparison) || path.StartsWith(
            Path.EndsInDirectorySeparator(ancestor) ? ancestor : ancestor + Path.DirectorySeparatorChar, comparison);

    private static string[] Components(string relative) => relative.Split(
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
}
