using Qt.Quick;
using HdbResale.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace HdbResale.App;
internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] is "--address-coverage" or "--address-coverage-baseline")
        {
            WriteReport(args[2], AddressCoverageStudy.Run(args[1], supportMultiPolygon: args[0] != "--address-coverage-baseline"));
            return;
        }
        if (args.Length == 3 && args[0] == "--coverage")
        {
            var report = CoverageStudy.Summarize(CsvImport.LoadDirectory(args[1]));
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            File.WriteAllText(args[2], JsonSerializer.Serialize(report, options) + "\n");
            return;
        }
        if (args.Length == 3 && args[0] == "--onemap-queries")
        {
            WriteReport(args[2], OneMapEvidence.Queries(args[1], CsvImport.LoadDirectory(args[1])));
            return;
        }
        if (args.Length == 5 && args[0] == "--onemap-compare")
        {
            var report = OneMapComparison.Load(args[1],args[2],args[3]);
            WriteReport(args[4], report);
            if (!report.AcquisitionComplete) Environment.ExitCode = 1;
            return;
        }
        if (args.Length == 5 && args[0] == "--historical-onemap")
        {
            WriteReport(args[4],HistoricalOneMap.Load(args[1],args[2],args[3]));
            return;
        }
        if (args.Length == 3 && args[0] == "--scale")
        {
            WriteReport(args[2], ScaleStudy.Run(args[1]));
            return;
        }
        if (args.Length == 3 && args[0] is "--import-digest" or "--legacy-import-digest")
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            using var sink = new System.Security.Cryptography.CryptoStream(Stream.Null, hash,
                System.Security.Cryptography.CryptoStreamMode.Write);
            var imported = CsvImport.LoadDirectory(args[1]);
            if (args[0] == "--legacy-import-digest")
                JsonSerializer.Serialize(sink, new {
                    Accepted = imported.Accepted.Select(t => new {
                        t.Id, Facts = new { t.Facts.Month, t.Facts.Town, t.Facts.Block, t.Facts.Street, t.Facts.FlatType, t.Facts.Price },
                        t.Location, t.Match, t.Town, t.Address, t.FlatType, t.Price
                    }), imported.Rejected, imported.Diagnostics, imported.MatchedCount, imported.AmbiguousCount, imported.UnmatchedCount
                });
            else JsonSerializer.Serialize(sink, imported);
            sink.FlushFinalBlock();
            File.WriteAllText(args[2], Convert.ToHexString(hash.Hash!).ToLowerInvariant() + "\n");
            return;
        }
        if (args.Length == 3 && args[0] == "--startup-profile")
        {
            WriteReport(args[2], StartupStudy.Run(args[1]));
            return;
        }
        Qml.LoadFromRootModule(Environment.GetEnvironmentVariable("HDB_STARTUP_PROFILE") == "1" &&
            Environment.GetEnvironmentVariable("HDB_STARTUP_VIEW") == "qml-shell" ? "StartupShell" : "Main");
        Qml.WaitForExit();
        if (Environment.GetEnvironmentVariable("HDB_RUNTIME_GATE") == "1" || Environment.GetEnvironmentVariable("HDB_SCALE_GATE") == "1" || Environment.GetEnvironmentVariable("HDB_STARTUP_PROFILE") == "1" || Environment.GetEnvironmentVariable("HDB_BUYER_GATE") == "1") Console.WriteLine("HDB_GATE_EXIT");
        if (Environment.GetEnvironmentVariable("HDB_PACKAGE_SMOKE") == "1") Console.WriteLine("HDB_PACKAGE_EXIT");
    }
    private static void WriteReport<T>(string path, T value)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(path, JsonSerializer.Serialize(value,options)+"\n");
    }
}
