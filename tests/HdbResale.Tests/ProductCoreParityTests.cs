using System.Text.Json;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

// The web app's golden fixtures (tests/fixtures/web-parity, copied from hdb-resale-visualizer) are the
// cross-language contract for buyer logic. This app must reproduce every scenario for the filters it offers,
// built from the same default block the web app's own parity test uses.
public sealed class ProductCoreParityTests
{
    private static readonly JsonElement Golden = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "fixtures", "web-parity", "product-core-golden.json"))).RootElement;

    // Filters this app does not offer (it has no lease or MRT filter); their scenarios cannot apply.
    private static readonly string[] UnofferedFilters = ["filterRemainingLeaseMin", "filterMrtMax"];

    private static string? Text(JsonElement scenario, string name) =>
        scenario.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static decimal? Number(JsonElement scenario, string name) =>
        scenario.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    internal static AddressSummary Block(string town = "BEDOK", decimal medianPrice = 500000, string[]? flatTypes = null,
        IReadOnlyDictionary<string, decimal>? medianByType = null) =>
        new("test", town, "1", "TEST", new(1.35, 103.8), medianPrice, 5500, 5, [80, 100], [2000, 2000], "2026-01",
            ["2024-01", "2026-01"], flatTypes ?? ["4 ROOM"], ["MODEL A"])
        { MedianPriceByFlatType = medianByType };

    [Fact]
    public void ReproducesEveryFilterScenarioForTheFiltersThisAppOffers()
    {
        var applied = new List<string>();
        var notApplicable = new List<string>();
        foreach (var scenario in Golden.GetProperty("filterScenarios").EnumerateArray())
        {
            var name = Text(scenario, "name")!;
            if (UnofferedFilters.Any(f => scenario.TryGetProperty(f, out _))) { notApplicable.Add(name); continue; }
            var block = Block(Text(scenario, "blockTown") ?? "BEDOK", Number(scenario, "blockMedianPrice") ?? 500000,
                scenario.TryGetProperty("blockFlatTypes", out var types) ? types.Deserialize<string[]>() : null);
            var matches = AddressSemantics.Matches(block, Text(scenario, "filterTown"), Text(scenario, "filterFlatType"),
                Number(scenario, "filterBudgetMin"), Number(scenario, "filterBudgetMax"), startMonth: null);
            Assert.True(scenario.GetProperty("expected").GetBoolean() == matches, $"golden filter scenario {name}");
            applied.Add(name);
        }
        Assert.Equal(7, applied.Count);
        // Only scenarios of filters this app lacks are left out, so a new scenario cannot be skipped silently.
        Assert.Equal(["lease-filter-pass", "lease-filter-fail", "mrt-distance-filter-pass", "mrt-distance-filter-fail",
            "mrt-null-filter-fail"], notApplicable);
    }

    [Fact]
    public void ReproducesEveryEffectivePriceScenario()
    {
        var count = 0;
        foreach (var scenario in Golden.GetProperty("effectivePriceScenarios").EnumerateArray())
        {
            var byType = scenario.GetProperty("blockMedianPriceByFlatType").Deserialize<Dictionary<string, decimal>>();
            var block = Block(medianPrice: Number(scenario, "blockMedianPrice")!.Value, medianByType: byType);
            Assert.Equal(Number(scenario, "expectedEffectivePrice"),
                AddressSemantics.EffectiveMedianPrice(block, Text(scenario, "filterFlatType")));
            count++;
        }
        Assert.Equal(3, count);
    }
}
