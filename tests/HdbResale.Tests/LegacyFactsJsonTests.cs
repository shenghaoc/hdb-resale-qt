using System.Text.Json;
using HdbResale.App;
using Xunit;
namespace HdbResale.Tests;
public sealed class LegacyFactsJsonTests
{
    [Fact]
    public void HistoricalFactsKeepExactOldFieldOrderWhileFullFactsRemainVersionTwo()
    {
        var facts=ExplorerStateTests.Fixture().Accepted[0].Facts;
        Assert.NotNull(facts.RemainingLeaseSource);
        var options=new JsonSerializerOptions();options.Converters.Add(new LegacyFactsJsonConverter());
        var expected=JsonSerializer.Serialize(new {facts.Month,facts.Town,facts.Block,facts.Street,facts.FlatType,facts.Price});
        Assert.Equal(expected,JsonSerializer.Serialize(facts,options));
        Assert.Contains("RemainingLeaseSource",JsonSerializer.Serialize(facts));
        Assert.DoesNotContain("RemainingLeaseSource",expected);
    }
}
