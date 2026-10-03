using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class ExplorerStateTests
{
    [Fact]
    public void TownAndBudgetCombineInclusivelyAndClearHiddenSelection()
    {
        var state = new ExplorerState(Fixture.Transactions);
        state.Select("TP-1");
        state.Filter("Tampines", 420_000);
        Assert.Equal("TP-2", Assert.Single(state.Visible).Id);
        Assert.Null(state.Selected);
        state.Select("CL-1");
        Assert.Null(state.Selected); // Hidden transactions cannot be selected.
        state.Select("TP-2");
        state.Filter("All towns", 470_000);
        Assert.Equal(3, state.Visible.Count);
        Assert.Equal("TP-2", state.Selected?.Id); // A still-visible selection survives.
        state.Reset();
        Assert.Equal(6, state.Visible.Count);
        Assert.Equal(1_000_000, state.MaximumPrice);
    }
    [Fact]
    public void EmptyResultsAndInvalidBudgetDoNotCorruptState()
    {
        var state = new ExplorerState(Fixture.Transactions);
        state.Filter("Clementi", 0);
        Assert.Empty(state.Visible);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Filter("All towns", -1));
        Assert.Equal("Clementi", state.Town);
        Assert.Equal(0, state.MaximumPrice);
    }
}
