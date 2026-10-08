using System.Net;
using HdbResale.App;
using HdbResale.Domain;
using Xunit;

namespace HdbResale.Tests;

public sealed class AddressDetailStateTests
{
    private const string Key = "bedok-10d-bedok-sth-ave-2";

    private static async Task Drain(ApiRequests requests)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (requests.Busy && DateTime.UtcNow < deadline)
        {
            requests.Drain();
            await Task.Delay(1);
        }
        Assert.False(requests.Busy);
    }

    [Fact]
    public async Task FailedDetailCanRetrySameSelectedKeyAndPopulateRegistrationsAndTrend()
    {
        var attempts = 0;
        using var client = WorkerApiClientTests.Client(new WorkerApiClientTests.RecordedApi(path =>
            path.StartsWith("/api/details/") && ++attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : null));
        var latest = (await client.GetManifestAsync()).LatestMonth;
        using var requests = new ApiRequests();
        var state = new AddressDetailState();
        var notifications = 0;
        state.Select(Key, latest, client.GetAddressDetailAsync, requests, () => notifications++);
        await Drain(requests);
        Assert.True(state.CanRetry);
        Assert.Contains("503", state.Error);
        Assert.Null(state.Detail);
        Assert.Empty(state.Trend.Points);
        state.Retry(latest, client.GetAddressDetailAsync, requests, () => notifications++);
        Assert.True(state.Loading);
        Assert.False(state.CanRetry);
        Assert.Empty(state.Error);
        state.Retry(latest, client.GetAddressDetailAsync, requests, () => notifications++);
        await Drain(requests);
        Assert.Equal(2, attempts);
        Assert.Equal(Key, state.Detail!.Summary.AddressKey);
        Assert.Equal(20, state.Detail.RecentTransactions.Count);
        Assert.True(state.Trend.ObservedMonths > 0);
        Assert.False(state.CanRetry);
        Assert.False(state.Loading);
        Assert.Equal(4, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DropsOldSuccessAndFailureAfterSelectionChangesAndReturnsToSameKey(bool fail)
    {
        using var client = WorkerApiClientTests.Client(new WorkerApiClientTests.RecordedApi());
        var latest = (await client.GetManifestAsync()).LatestMonth;
        var detail = await client.GetAddressDetailAsync(Key);
        using var requests = new ApiRequests();
        var state = new AddressDetailState();
        var old = new TaskCompletionSource<AddressDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = new TaskCompletionSource<AddressDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Select(Key, latest, (_, _) => old.Task, requests, () => { });
        state.Select("other", latest, (_, _) => Task.FromResult<AddressDetail?>(null), requests, () => { });
        state.Select(Key, latest, (_, _) => current.Task, requests, () => { });
        current.SetResult(detail);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (state.Detail is null && DateTime.UtcNow < deadline)
        {
            requests.Drain();
            await Task.Delay(1);
        }
        Assert.Same(detail, state.Detail);
        // Deliver the stale outcome after the current success has been applied.
        if (fail) old.SetException(new WorkerApiException("stale failure"));
        else old.SetResult(null);
        await Drain(requests);
        Assert.Same(detail, state.Detail);
        Assert.Empty(state.Error);
        Assert.True(state.Trend.ObservedMonths > 0);
        state.Select("", latest, client.GetAddressDetailAsync, requests, () => { });
        state.Retry(latest, client.GetAddressDetailAsync, requests, () => { });
        Assert.False(requests.Busy);
        Assert.Null(state.Detail);
        Assert.Empty(state.Trend.Points);
    }
}
