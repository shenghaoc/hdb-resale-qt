using Qt.Quick;
using HdbResale.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace HdbResale.App;
internal static class Program
{
    private static void Main(string[] args)
    {
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
        Qml.LoadFromRootModule("Main");
        Qml.WaitForExit();
        if (Environment.GetEnvironmentVariable("HDB_RUNTIME_GATE") == "1") Console.WriteLine("HDB_GATE_EXIT");
    }
    private static void WriteReport<T>(string path, T value)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(path, JsonSerializer.Serialize(value,options)+"\n");
    }
}
