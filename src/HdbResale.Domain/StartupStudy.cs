using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.FileIO;
namespace HdbResale.Domain;

// Opt-in diagnostics. ThreadAllocatedBytes is only the calling managed thread;
// ProcessAllocatedBytes includes all managed threads, not native Qt allocations.
public sealed record StartupMeasurement(string Operation, double Milliseconds, int ManagedThreadId,
    long? ThreadAllocatedBytes, long ProcessAllocatedBytes, long ManagedBytes, long WorkingSetBytes);
public sealed class StartupProfiler
{
    private readonly Stopwatch timer = Stopwatch.StartNew();
    private long threadAllocated = GC.GetAllocatedBytesForCurrentThread();
    private int threadId = Environment.CurrentManagedThreadId;
    private long processAllocated = GC.GetTotalAllocatedBytes(true);
    public List<StartupMeasurement> Measurements { get; } = [];
    public StartupMeasurement Measure(string operation)
    {
        var milliseconds = timer.Elapsed.TotalMilliseconds;
        var thread = GC.GetAllocatedBytesForCurrentThread();
        var process = GC.GetTotalAllocatedBytes(true);
        var result = new StartupMeasurement(operation, milliseconds, Environment.CurrentManagedThreadId,
            threadId == Environment.CurrentManagedThreadId ? thread - threadAllocated : null, process - processAllocated, GC.GetTotalMemory(false), Environment.WorkingSet);
        Measurements.Add(result);
        threadId = Environment.CurrentManagedThreadId;
        threadAllocated = GC.GetAllocatedBytesForCurrentThread();
        processAllocated = GC.GetTotalAllocatedBytes(true);
        timer.Restart();
        return result;
    }
}
public sealed record StartupRun(int Iteration, int Transactions, int Addresses, int Located, int Markers,
    int Rejected, int Diagnostics, IReadOnlyList<StartupMeasurement> Measurements);
public static class StartupStudy
{
    public static IReadOnlyList<StartupRun> Run(string directory) => Enumerable.Range(0, 3).Select(i => RunOnce(directory, i)).ToArray();
    // A separate frame ensures prior iteration's import cannot be a retained root.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static StartupRun RunOnce(string directory, int iteration)
    {
        var profile = new StartupProfiler();
        var imported = CsvImport.LoadDirectory(directory, stage: name => profile.Measure(name),
            referenceCsv: Environment.GetEnvironmentVariable("HDB_CSV_REFERENCE") == "1");
        profile.Measure("import-return");
        var rows = imported.Accepted;
        if (rows.Count == 0) throw new InvalidDataException("No accepted transactions.");
        var state = new ExplorerState(rows); profile.Measure("state-construction");
        var blocks = BlockSummaries.Located(state.Visible); profile.Measure("map-aggregation");
        state.Filter(rows[0].Town, 500_000); profile.Measure("town-budget-filter");
        state.Reset(); profile.Measure("reset");
        // Diagnostic experiment only, after the timed production path. Keep the
        // import, current state and address summaries alive across this collection.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        profile.Measure("diagnostic-retained-after-import");
        // Explanatory warm-cache probes, NOT additive stages of the import above.
        foreach (var file in new[] { "address-evidence.csv", "postal-address-evidence.csv", "transactions.csv", "building-evidence.geojson" })
        {
            using (var input = File.OpenRead(Path.Combine(directory, file))) input.CopyTo(Stream.Null);
            profile.Measure("probe-file-io-" + file);
        }
        using (var parser = new TextFieldParser(Path.Combine(directory, "transactions.csv"))
            { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false })
        {
            parser.SetDelimiters(",");
            while (!parser.EndOfData) _ = parser.ReadFields();
        }
        profile.Measure("probe-textfieldparser-tokenization-only");
        var addresses = rows.Select(t => (t.Facts.Block, t.Facts.Street)).Distinct().ToArray();
        profile.Measure("probe-distinct-addresses");
        foreach (var address in addresses)
        {
            _ = AddressNormalizer.Block(address.Block);
            _ = AddressNormalizer.Street(address.Street);
        }
        profile.Measure("probe-distinct-normalization");
        var result = new StartupRun(iteration, rows.Count,
            rows.Select(t => (t.Town, t.Facts.Block, t.Facts.Street)).Distinct().Count(),
            rows.Count(t => t.Location.Point is not null), BlockSummaries.Located(rows).Count,
            imported.Rejected.Count, imported.Diagnostics.Count, profile.Measurements);
        GC.KeepAlive(imported); GC.KeepAlive(state); GC.KeepAlive(blocks);
        return result;
    }
}
