using HdbResale.App;
using Xunit;

namespace HdbResale.Tests;

public sealed class TileFailureStatusTests
{
    [Fact]
    public void NoticeWaitsForRepeatedExhaustedRequestsAndNotifiesOnce()
    {
        var status = new TileFailureStatus();
        Assert.False(status.Observe(0));
        Assert.False(status.RepeatedFailures);
        Assert.False(status.Observe(1));
        Assert.False(status.RepeatedFailures);
        Assert.True(status.Observe(2));
        Assert.True(status.RepeatedFailures);
        Assert.False(status.Observe(2));
        Assert.False(status.Observe(20));
        Assert.True(status.RepeatedFailures);
    }

    [Fact]
    public void LaterPollsCannotClearAnObservedFailureAndNewLaunchStartsClear()
    {
        var status = new TileFailureStatus();
        status.Observe(3);
        Assert.False(status.Observe(0));
        Assert.True(status.RepeatedFailures);
        Assert.False(new TileFailureStatus().RepeatedFailures);
    }
}
