using System.Diagnostics;
namespace HdbResale.Domain;

public sealed record ScaleMeasurement(string Operation, double Milliseconds, long ManagedBytes, long WorkingSetBytes);
public sealed record ScaleRun(int Iteration, int Transactions, int Addresses, int Blocks, int Towns, int FlatTypes,
    int Located, int LocatedAddresses, int AmbiguousAddresses, int UnlocatedAddresses, int Ambiguous, int Unlocated, int Rejected, int Diagnostics, IReadOnlyDictionary<int, int> TransactionsPerAddress,
    IReadOnlyList<ScaleMeasurement> Measurements);
public static class ScaleStudy
{
    public static IReadOnlyList<ScaleRun> Run(string directory)
    {
        var runs = new List<ScaleRun>();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var measurements = new List<ScaleMeasurement>();
            var timer = Stopwatch.StartNew();
            void Measure(string name)
            {
                measurements.Add(new(name, Math.Round(timer.Elapsed.TotalMilliseconds, 1),
                    GC.GetTotalMemory(false), Environment.WorkingSet));
                timer.Restart();
            }
            var imported = CsvImport.LoadDirectory(directory, stage: Measure, indexed: Environment.GetEnvironmentVariable("HDB_SCALE_UNINDEXED") != "1");
            var rows = imported.Accepted;
            if (rows.Count == 0) throw new InvalidDataException("No accepted transactions.");
            var state = new ExplorerState(rows); Measure("state-construction");
            state.Filter(rows[0].Town, 1_000_000); Measure("town-filter");
            state.Filter("All towns", 500_000); Measure("budget-filter");
            state.Filter(rows[0].Town, 500_000); Measure("combined-filter");
            state.Select(state.Visible.FirstOrDefault()?.Id ?? ""); Measure("selection");
            state.Filter("All towns", 0); Measure("empty-hidden-selection");
            state.Reset(); Measure("reset");
            var blocks = BlockSummaries.Located(rows); Measure("map-aggregation");
            var groups = rows.GroupBy(t => (t.Town, t.Facts.Block, t.Facts.Street)).ToArray();
            runs.Add(new(iteration, rows.Count, groups.Length, rows.Select(t => t.Facts.Block).Distinct().Count(),
                rows.Select(t => t.Town).Distinct().Count(), rows.Select(t => t.FlatType).Distinct().Count(),
                rows.Count(t => t.Location.Point is not null), blocks.Count,
                groups.Count(g => g.Any(t => t.Match.Quality == MatchQuality.Ambiguous)), groups.Count(g => g.All(t => t.Location.Point is null)),
                rows.Count(t => t.Match.Quality == MatchQuality.Ambiguous),
                rows.Count(t => t.Location.Point is null), imported.Rejected.Count, imported.Diagnostics.Count, groups.GroupBy(g => g.Count()).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()), measurements));
        }
        return runs;
    }
}
