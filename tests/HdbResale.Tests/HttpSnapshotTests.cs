using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using HdbResale.Domain;
using Xunit;

namespace HdbResale.Tests;

public sealed class HttpSnapshotTests : IDisposable
{
    private readonly string cache = Path.Combine(Path.GetTempPath(), "hdb-http-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(cache)) Directory.Delete(cache, true); }
    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class Fixture
    {
        public Dictionary<string, byte[]> Responses { get; } = new();
        public JsonObject Manifest { get; }
        public string Hash { get; private set; } = "";
        public Fixture(int price = 400000, int records = 2)
        {
            var header = "source_row,month,town,flat_type,block,street_name,resale_price,remaining_lease";
            var csv = Encoding.UTF8.GetBytes($"{header}\n2,2015-01,T,3 ROOM,1,ST,{price},070\n3,2015-01,T,3 ROOM,1,ST,{price},070\n");
            var sources = new JsonObject { ["schemaVersion"] = "hdb-transaction-sources-v1", ["sources"] = new JsonArray(new JsonObject {
                ["sourceIdentity"] = "years", ["path"] = "transactions/years.csv", ["sha256"] = Sha(csv), ["bytes"] = csv.Length,
                ["records"] = records, ["rawSha256"] = new string('a', 64), ["rawBytes"] = csv.Length,
                ["columns"] = new JsonArray(header.Split(',').Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()) }) };
            var files = new Dictionary<string, byte[]> {
                ["transactions/years.csv"] = csv, ["transaction-sources.json"] = Encoding.UTF8.GetBytes(sources.ToJsonString()),
                ["address-evidence.csv"] = Encoding.UTF8.GetBytes("source_row,blk_no,street\n"),
                ["postal-address-evidence.csv"] = Encoding.UTF8.GetBytes("source_row,block,street_name,postal_code\n"),
                ["building-evidence.geojson"] = Encoding.UTF8.GetBytes("{\"type\":\"FeatureCollection\",\"features\":[]}") };
            var entries = new JsonArray();
            foreach (var (path, bytes) in files)
            {
                using var stream = new MemoryStream();
                using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true)) gzip.Write(bytes);
                var compressed = stream.ToArray(); var hash = Sha(compressed);
                Responses["/objects/" + hash + ".gz"] = compressed;
                entries.Add(new JsonObject { ["path"] = path, ["sha256"] = Sha(bytes), ["bytes"] = bytes.Length,
                    ["gzipSha256"] = hash, ["gzipBytes"] = compressed.Length });
            }
            Manifest = new JsonObject { ["schemaVersion"] = "hdb-desktop-snapshot-v1", ["importerVersion"] = 1, ["files"] = entries };
            Seal();
        }
        public void Seal()
        {
            var bytes = Encoding.UTF8.GetBytes(Manifest.ToJsonString()); Hash = Sha(bytes);
            Responses["/manifests/" + Hash + ".json"] = bytes;
            Responses["/current.json"] = Encoding.UTF8.GetBytes(new JsonObject {
                ["schemaVersion"] = "hdb-snapshot-current-v1", ["manifestSha256"] = Hash }.ToJsonString());
        }
        public JsonObject First => (JsonObject)Manifest["files"]![0]!;
        public string FirstObject => "/objects/" + First["gzipSha256"]!.GetValue<string>() + ".gz";
    }
    // Actual loopback HTTP, including content-length truncation and cancellation.
    private sealed class Server : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task run;
        public List<string> Requests { get; } = [];
        public Func<string, int, (byte[] Body, long Length, int Status)> Reply { get; set; }
        public TaskCompletionSource ObjectStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool StallObjects { get; set; }
        public Uri Root { get; }
        public Server(Fixture fixture)
        {
            Reply = (path, _) => fixture.Responses.TryGetValue(path, out var body) ? (body, body.Length, 200) : ([], 0, 404);
            listener.Start(); Root = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/"); run = Serve();
        }
        private async Task Serve()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(stop.Token);
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var request = await reader.ReadLineAsync(stop.Token);
                    if (request is null) continue;
                    var path = request.Split(' ')[1];
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stop.Token))) { }
                    Requests.Add(path);
                    var (body, length, status) = Reply(path, Requests.Count(p => p == path));
                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Fixture\r\nContent-Length: {length}\r\nConnection: close\r\nLocation: http://127.0.0.1:1/escaped\r\n\r\n");
                    await stream.WriteAsync(header, stop.Token);
                    if (StallObjects && path.StartsWith("/objects/"))
                    { ObjectStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, stop.Token); }
                    await stream.WriteAsync(body, stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await run; stop.Dispose(); }
    }
    private async Task<string> Activate(Fixture fixture)
    {
        await using var server = new Server(fixture);
        return await HttpSnapshot.SynchronizeAsync(server.Root, cache);
    }
    private void PreviousStillWorks(string previous)
    {
        Assert.Equal(previous, HttpSnapshot.ActiveDirectory(cache));
        var rows = CsvImport.LoadDirectory(previous).Accepted;
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(400000, r.Price));
        Assert.Empty(Directory.GetDirectories(Path.Combine(cache, "snapshots"), ".download-*"));
    }
    [Fact]
    public async Task DownloadActivationOfflineReuseAndExistingBuyerStatePreserveOccurrences()
    {
        var fixture = new Fixture();
        await using var server = new Server(fixture);
        var active = await HttpSnapshot.SynchronizeAsync(server.Root, cache);
        Assert.Equal(active, HttpSnapshot.ActiveDirectory(cache));
        Assert.Equal(8, server.Requests.Count); // current, manifest, five objects, current
        var imported = CsvImport.LoadDirectory(active);
        Assert.Equal(new[] { "HDB-years-2", "HDB-years-3" }, imported.Accepted.Select(r => r.Id));
        Assert.Equal(imported.Accepted[0].Facts, imported.Accepted[1].Facts);
        var state = new ExplorerState(imported.Accepted); state.SelectAddress("T|1|ST");
        Assert.Equal(2, state.SelectedAddress!.Count); Assert.Equal(400000, state.SelectedAddress.MedianPrice);
        Assert.True(state.SelectedAddress.LeaseIncludesWholeYearObservations); Assert.Empty(state.MappedAddresses);
        server.Requests.Clear();
        Assert.Equal(active, await HttpSnapshot.SynchronizeAsync(server.Root, cache));
        Assert.Equal(new[] { "/current.json", "/manifests/" + fixture.Hash + ".json", "/current.json" }, server.Requests);
    }
    [Theory]
    [InlineData("truncated")]
    [InlineData("compressed-corrupt")]
    [InlineData("raw-corrupt")]
    [InlineData("unsupported-pack")]
    [InlineData("unsupported-importer")]
    [InlineData("unsupported-pointer")]
    [InlineData("changed-pointer")]
    [InlineData("wrong-records")]
    [InlineData("invalid-path")]
    [InlineData("redirect")]
    [InlineData("oversized")]
    public async Task FailurePreservesPreviousCompleteSnapshot(string kind)
    {
        var previous = await Activate(new Fixture());
        var fixture = new Fixture(500000, kind == "wrong-records" ? 3 : 2);
        if (kind == "raw-corrupt") fixture.First["sha256"] = new string('0', 64);
        if (kind == "unsupported-pack") fixture.Manifest["schemaVersion"] = "future";
        if (kind == "unsupported-importer") fixture.Manifest["importerVersion"] = 2;
        if (kind == "invalid-path") fixture.First["path"] = "../outside.csv";
        fixture.Seal();
        await using var server = new Server(fixture);
        var original = server.Reply;
        server.Reply = (path, count) => {
            var normal = original(path, count);
            if (path == fixture.FirstObject && kind == "truncated") return (normal.Body[..(normal.Body.Length / 2)], normal.Length, 200);
            if (path == fixture.FirstObject && kind == "compressed-corrupt") { var bytes = normal.Body.ToArray(); bytes[^1] ^= 1; return (bytes, bytes.Length, 200); }
            if (path == "/current.json" && (kind == "unsupported-pointer" || kind == "changed-pointer" && count == 2))
            {
                var body = Encoding.UTF8.GetBytes(new JsonObject { ["schemaVersion"] = kind == "unsupported-pointer" ? "future" : "hdb-snapshot-current-v1",
                    ["manifestSha256"] = new string('0', 64) }.ToJsonString()); return (body, body.Length, 200);
            }
            if (kind == "redirect") return ([], 0, 302);
            if (kind == "oversized") return ([], 4097, 200);
            return normal;
        };
        await Assert.ThrowsAnyAsync<Exception>(() => HttpSnapshot.SynchronizeAsync(server.Root, cache));
        PreviousStillWorks(previous);
        Assert.DoesNotContain(server.Requests, path => path.Contains("outside") || path.Contains("escaped"));
    }
    [Fact]
    public async Task CancellationDuringHttpObjectPreservesPreviousCompleteSnapshot()
    {
        var previous = await Activate(new Fixture());
        await using var server = new Server(new Fixture(500000)) { StallObjects = true };
        using var cancellation = new CancellationTokenSource();
        var sync = HttpSnapshot.SynchronizeAsync(server.Root, cache, cancellation.Token);
        await server.ObjectStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync);
        PreviousStillWorks(previous);
    }
    [Theory]
    [InlineData("https://example.test/root")]
    [InlineData("http://example.test/root/")]
    [InlineData("https://user@example.test/root/")]
    [InlineData("https://example.test/root/?x=1")]
    public async Task InvalidRootIsRejectedBeforeNetwork(string root)
    { await Assert.ThrowsAsync<ArgumentException>(() => HttpSnapshot.SynchronizeAsync(new Uri(root), cache)); }
    [Fact]
    public async Task CacheLockRejectsOverlappingActivation()
    {
        var previous = await Activate(new Fixture());
        using var held = new FileStream(Path.Combine(cache, ".sync.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await using var server = new Server(new Fixture(500000));
        await Assert.ThrowsAsync<IOException>(() => HttpSnapshot.SynchronizeAsync(server.Root, cache));
        Assert.Empty(server.Requests); PreviousStillWorks(previous);
    }
    [Fact]
    public async Task OfflineReadDetectsCachedEvidenceCorruption()
    {
        var active = await Activate(new Fixture());
        File.AppendAllText(Path.Combine(active, "address-evidence.csv"), "changed");
        Assert.Throws<InvalidDataException>(() => HttpSnapshot.ActiveDirectory(cache));
    }
}
