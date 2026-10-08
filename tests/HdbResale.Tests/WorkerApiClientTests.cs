using System.Net;
using System.Text;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;

// The API client against recorded production responses (tests/fixtures/worker-api), served by path.
public sealed class WorkerApiClientTests
{
    internal static string FixturePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "fixtures", "worker-api", .. parts]);

    // Serves the recorded API by request path; `override` replaces one path's answer.
    internal sealed class RecordedApi(Func<string, HttpResponseMessage?>? replace = null) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests.Add(request.RequestUri!);
            var path = request.RequestUri!.AbsolutePath;
            if (replace?.Invoke(path) is { } replaced) return Task.FromResult(replaced);
            var file = path switch
            {
                "/api/manifest" => FixturePath("manifest.json"),
                "/api/block-summaries" => FixturePath("block-summaries.json"),
                _ when path.StartsWith("/api/details/", StringComparison.Ordinal) =>
                    FixturePath("details", Uri.UnescapeDataString(path["/api/details/".Length..]) + ".json"),
                _ => null,
            };
            return Task.FromResult(file is not null && File.Exists(file)
                ? Json(File.ReadAllText(file))
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"Not found\"}") });
        }
    }

    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal static WorkerApiClient Client(HttpMessageHandler handler) =>
        new(new Uri("https://api.example.test"), handler);

    [Fact]
    public async Task ReadsTheManifestAddressesAndDetailOfTheRecordedApi()
    {
        var api = new RecordedApi();
        using var client = Client(api);
        var manifest = await client.GetManifestAsync();
        Assert.Equal("2.0.0", manifest.SchemaVersion);
        Assert.Equal("2026-10", manifest.LatestMonth.ToString());
        Assert.Contains("KALLANG/WHAMPOA", manifest.FilterOptions.Towns);
        Assert.Equal(988128, manifest.Counts.Transactions);

        var addresses = await client.GetAddressesAsync();
        Assert.Equal(11, addresses.Count);
        var executive = addresses.Single(a => a.AddressKey == "bedok-10d-bedok-sth-ave-2");
        Assert.Equal("10D BEDOK STH AVE 2", executive.Address);
        Assert.Equal(1204444m, executive.MedianPrice);
        Assert.Equal(1204444m, executive.MedianPriceByFlatType!["EXECUTIVE"]);
        Assert.Equal("BEDOK MRT STATION", executive.NearestMrt!.StationName);
        Assert.Equal(1.3217708591015, executive.Coordinates.Lat, 12);

        var detail = await client.GetAddressDetailAsync("bedok-10d-bedok-sth-ave-2");
        Assert.NotNull(detail);
        Assert.Equal(executive.AddressKey, detail!.Summary.AddressKey);
        Assert.Equal(20, detail.RecentTransactions.Count);
        Assert.Equal("68 years 04 months", detail.RecentTransactions[0].RemainingLease);
        Assert.Equal("2026-09", detail.MonthlyTrend[^1].Month);
        Assert.Equal(2, detail.Summary.PriceIqr.Count);
        Assert.Equal(["/api/manifest", "/api/block-summaries", "/api/details/bedok-10d-bedok-sth-ave-2"],
            api.Requests.Select(u => u.AbsolutePath));
    }

    [Fact]
    public async Task EscapesTheAddressKeyAndAnswersNullForAnUnknownAddress()
    {
        var api = new RecordedApi();
        using var client = Client(api);
        Assert.Null(await client.GetAddressDetailAsync("no such/address?x=1"));
        Assert.Equal("/api/details/no%20such%2Faddress%3Fx%3D1", api.Requests.Single().AbsolutePath);
        Assert.Empty(api.Requests.Single().Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task RefusesAFailedResponseWithItsStatus(HttpStatusCode status)
    {
        using var client = Client(new RecordedApi(_ => Json("{\"error\":\"Internal server error\"}", status)));
        var error = await Assert.ThrowsAsync<WorkerApiException>(() => client.GetAddressesAsync());
        Assert.Equal(status, error.Status);
    }

    [Fact]
    public async Task ExplainsThatNoDatasetIsPublishedYet()
    {
        using var client = Client(new RecordedApi(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var error = await Assert.ThrowsAsync<WorkerApiException>(() => client.GetManifestAsync());
        Assert.Equal(HttpStatusCode.NotFound, error.Status);
    }

    public static TheoryData<string, string> InvalidAddressResponses()
    {
        var recorded = File.ReadAllText(FixturePath("block-summaries.json"));
        return new()
        {
            { "not json", "[{" },
            { "a string where a number belongs", recorded.Replace("\"medianPrice\": 1204444", "\"medianPrice\": \"1204444\"") },
            { "a missing required field", recorded.Replace("\"latestMonth\": \"2026-09\",", "") },
            { "a null required field", recorded.Replace("\"town\": \"BEDOK\"", "\"town\": null") },
            { "an impossible month", recorded.Replace("\"latestMonth\": \"2026-09\"", "\"latestMonth\": \"2026-13\"") },
            { "a reversed range", recorded.Replace("\"floorAreaRange\": [\n   141,\n   143\n  ]", "\"floorAreaRange\": [\n   143,\n   141\n  ]") },
            { "a duplicate address", "[" + recorded.Trim()[1..^1] + "," + recorded.Trim()[1..^1] + "]" },
            { "coordinates off the globe", recorded.Replace("\"lat\": 1.3217708591015", "\"lat\": 91") },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidAddressResponses))]
    public async Task RefusesAnAddressListThatDoesNotHold(string reason, string body)
    {
        Assert.True(File.ReadAllText(FixturePath("block-summaries.json")) != body, "the fixture edit did not apply: " + reason);
        using var client = Client(new RecordedApi(path => path == "/api/block-summaries" ? Json(body) : null));
        await Assert.ThrowsAsync<WorkerApiException>(() => client.GetAddressesAsync());
    }

    [Fact]
    public async Task RefusesADetailForAnotherAddressOrWithAnUnorderedTrend()
    {
        var recorded = File.ReadAllText(FixturePath("details", "bedok-10d-bedok-sth-ave-2.json"));
        using (var client = Client(new RecordedApi(_ => Json(recorded))))
            await Assert.ThrowsAsync<WorkerApiException>(() => client.GetAddressDetailAsync("bedok-39-bedok-sth-rd"));
        var unordered = recorded.Replace("{\"month\":\"1999-07\"", "{\"month\":\"2099-07\"");
        Assert.NotEqual(recorded, unordered);
        using (var client = Client(new RecordedApi(_ => Json(unordered))))
            await Assert.ThrowsAsync<WorkerApiException>(() => client.GetAddressDetailAsync("bedok-10d-bedok-sth-ave-2"));
    }

    [Theory]
    [InlineData("2026-10", "2026-10", true)]
    [InlineData("2025-12", "2026-01", true)]
    [InlineData("2026-01", "2025-12", false)]
    [InlineData("2026-10", "2026-09", false)]
    public async Task ValidatesManifestWindowOrder(string min, string max, bool accepted)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(FixturePath("manifest.json")))!;
        json["dataWindow"]!["minMonth"] = min;
        json["dataWindow"]!["maxMonth"] = max;
        using var client = Client(new RecordedApi(_ => Json(json.ToJsonString())));
        if (accepted)
        {
            var manifest = await client.GetManifestAsync();
            Assert.Equal(min, manifest.DataWindow.MinMonth);
            Assert.Equal(max, manifest.DataWindow.MaxMonth);
        }
        else Assert.Contains("dataWindow", (await Assert.ThrowsAsync<WorkerApiException>(() => client.GetManifestAsync())).Message);
    }

    private sealed class Failing(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation) =>
            Task.FromException<HttpResponseMessage>(error);
    }

    [Fact]
    public async Task ReportsAnUnreachableOrSilentApiWithoutLeakingTransportDetail()
    {
        using (var client = Client(new Failing(new HttpRequestException("socket detail"))))
        {
            var error = await Assert.ThrowsAsync<WorkerApiException>(() => client.GetManifestAsync());
            Assert.DoesNotContain("socket detail", error.Message);
        }
        using (var client = Client(new Failing(new TaskCanceledException("timer"))))
            Assert.Contains("in time", (await Assert.ThrowsAsync<WorkerApiException>(() => client.GetManifestAsync())).Message);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        using (var client = Client(new Failing(new TaskCanceledException("cancelled"))))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetManifestAsync(cancelled.Token));
    }

    [Theory]
    [InlineData("ftp://api.example.test/")]
    [InlineData("http://api.example.test/")]
    [InlineData("https://user:secret@api.example.test/")]
    [InlineData("https://api.example.test/?token=1")]
    [InlineData("https://api.example.test/#part")]
    public void AcceptsOnlyAPlainHttpsAddressOrALoopbackTestServer(string address) =>
        Assert.Throws<ArgumentException>(() => new WorkerApiClient(new Uri(address)));

    [Fact]
    public void DefaultsToProductionAndAcceptsAnotherDeployment()
    {
        Assert.Equal(WorkerApiClient.ProductionBaseAddress, WorkerApiClient.ConfiguredBaseAddress(null));
        Assert.Equal(WorkerApiClient.ProductionBaseAddress, WorkerApiClient.ConfiguredBaseAddress("  "));
        using var local = new WorkerApiClient(WorkerApiClient.ConfiguredBaseAddress("http://127.0.0.1:8787"));
        Assert.Equal("http://127.0.0.1:8787/", local.BaseAddress.AbsoluteUri);
        using var preview = new WorkerApiClient(new Uri("https://preview.example.test/base"));
        Assert.Equal("https://preview.example.test/base/", preview.BaseAddress.AbsoluteUri);
    }
}
