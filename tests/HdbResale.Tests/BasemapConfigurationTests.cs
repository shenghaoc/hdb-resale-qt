using HdbResale.App;
using Xunit;

namespace HdbResale.Tests;

public sealed class BasemapConfigurationTests
{
    private static readonly string ProductionCache = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "hdb-production-tiles"));
    private static readonly string TestCache = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "hdb-test-tiles"));

    [Fact]
    public void OrdinaryLaunchIgnoresTestValuesAndRetainsOneMapDefaults()
    {
        var config = BasemapConfiguration.Create(false, "invalid", "relative", ProductionCache);
        Assert.Equal("https://www.onemap.gov.sg/maps/tiles/Default/", config.TileEndpoint);
        Assert.Equal(ProductionCache, config.CacheDirectory);
    }

    [Fact]
    public void ExplicitTestUsesLoopbackEndpointAndSeparateCache()
    {
        var config = BasemapConfiguration.Create(true, "http://127.0.0.1:12345/tiles/", TestCache, ProductionCache);
        Assert.Equal("http://127.0.0.1:12345/tiles/", config.TileEndpoint);
        Assert.Equal(TestCache, config.CacheDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://www.onemap.gov.sg/maps/tiles/Default/")]
    [InlineData("file:///tmp/tiles/")]
    [InlineData("http://127.0.0.1:12345/tiles")]
    [InlineData("http://user:password@127.0.0.1:12345/tiles/")]
    [InlineData("http://127.0.0.1:12345/tiles/?query=1")]
    [InlineData("http://127.0.0.1:12345/tiles/#fragment")]
    public void TestRejectsMissingOrNonLocalEndpoints(string? endpoint) =>
        Assert.Throws<ArgumentException>(() => BasemapConfiguration.Create(true, endpoint, TestCache, ProductionCache));

    [Fact]
    public void TestRequiresAbsoluteCacheOutsideProductionTree()
    {
        foreach (var cache in new[] { null, "relative", ProductionCache, Path.Combine(ProductionCache, "child"), Path.GetDirectoryName(ProductionCache) })
            Assert.Throws<ArgumentException>(() => BasemapConfiguration.Create(true, "http://127.0.0.1:12345/tiles/", cache, ProductionCache));
    }

    // Symbolic links stand in for junctions too: both resolve through ResolveLinkTarget. Where the platform
    // refuses to create a link (Windows without the privilege), these tests have nothing to exercise.
    [Fact]
    public void TestRejectsLinksThatReachTheProductionCache()
    {
        var root = Directory.CreateTempSubdirectory("hdb-tile-links-").FullName;
        try
        {
            var production = Path.Combine(root, "production", "onemap-default-v1");
            Directory.CreateDirectory(production);
            if (!TryLink(Path.Combine(root, "to-production"), production)) return;
            Assert.True(TryLink(Path.Combine(root, "to-parent"), Path.GetDirectoryName(production)!));
            Assert.True(TryLink(Path.Combine(root, "chain"), Path.Combine(root, "to-production")));
            Assert.True(TryLink(Path.Combine(root, "relative"), Path.Combine("production", "onemap-default-v1")));
            Assert.True(TryLink(Path.Combine(root, "alias"), root));
            foreach (var cache in new[]
            {
                Path.Combine(root, "to-production"),
                Path.Combine(root, "to-production", "tests"),
                Path.Combine(root, "to-parent"),
                Path.Combine(root, "chain", "tests"),
                Path.Combine(root, "relative"),
                Path.Combine(root, "alias", "production", "onemap-default-v1", "tests"),
            })
                Assert.Throws<ArgumentException>(() => BasemapConfiguration.Create(true, Endpoint, cache, production));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TestResolvesTheProductionPathAndAcceptsUnrelatedLinks()
    {
        var root = Directory.CreateTempSubdirectory("hdb-tile-links-").FullName;
        try
        {
            var data = Path.Combine(root, "local-data");
            var elsewhere = Path.Combine(root, "elsewhere");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(elsewhere);
            if (!TryLink(Path.Combine(root, "data-link"), data)) return;
            // The production cache named through a link, the test cache named directly inside it.
            var production = Path.Combine(root, "data-link", "onemap-default-v1");
            Assert.Throws<ArgumentException>(() =>
                BasemapConfiguration.Create(true, Endpoint, Path.Combine(data, "onemap-default-v1", "tests"), production));
            Assert.True(TryLink(Path.Combine(root, "to-elsewhere"), elsewhere));
            var accepted = BasemapConfiguration.Create(true, Endpoint, Path.Combine(root, "to-elsewhere"), production);
            Assert.Equal(Path.Combine(root, "to-elsewhere"), accepted.CacheDirectory);
            Assert.True(TryLink(Path.Combine(root, "loop-a"), Path.Combine(root, "loop-b")));
            Assert.True(TryLink(Path.Combine(root, "loop-b"), Path.Combine(root, "loop-a")));
            Assert.Throws<ArgumentException>(() =>
                BasemapConfiguration.Create(true, Endpoint, Path.Combine(root, "loop-a", "tests"), production));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // "..": the filesystem applies it to the directory a link leads to, not to the link's own name.
    [Fact]
    public void TestAppliesParentSegmentsAfterLinksAsTheFilesystemDoes()
    {
        var root = Directory.CreateTempSubdirectory("hdb-tile-links-").FullName;
        try
        {
            var production = Path.Combine(root, "actual", "onemap");
            Directory.CreateDirectory(production);
            Directory.CreateDirectory(Path.Combine(root, "actual", "child"));
            if (!TryLink(Path.Combine(root, "jump"), Path.Combine("actual", "child"))) return;
            Assert.True(TryLink(Path.Combine(root, "cache"), Path.Combine("jump", "..", "onemap")));
            // Lexically these name root/onemap; physically both are the production cache.
            foreach (var cache in new[] { Path.Combine(root, "cache"), Path.Combine(root, "jump", "..", "onemap") })
                Assert.Throws<ArgumentException>(() => BasemapConfiguration.Create(true, Endpoint, cache, production));
            Assert.Equal(Path.Combine(root, "elsewhere"),
                BasemapConfiguration.Create(true, Endpoint, Path.Combine(root, "elsewhere"), production).CacheDirectory);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private const string Endpoint = "http://127.0.0.1:12345/tiles/";

    private static bool TryLink(string link, string target)
    {
        try { Directory.CreateSymbolicLink(link, target); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
