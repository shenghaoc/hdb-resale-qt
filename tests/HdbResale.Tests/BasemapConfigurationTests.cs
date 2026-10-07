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
}
