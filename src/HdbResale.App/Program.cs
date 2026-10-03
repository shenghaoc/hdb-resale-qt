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
        Qml.LoadFromRootModule("Main");
        Qml.WaitForExit();
        if (Environment.GetEnvironmentVariable("HDB_RUNTIME_GATE") == "1") Console.WriteLine("HDB_GATE_EXIT");
    }
}
