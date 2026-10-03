using HdbResale.Domain;
using System.Text.Json;
using System.Diagnostics;
using System.Globalization;
var import = CsvImport.LoadDirectory(args[0]);
Directory.CreateDirectory(args[1]);
var state = new ExplorerState(import.Accepted);
var town = import.Accepted.First().Town;
var other = import.Accepted.Select(t => t.Town).Distinct().Order(StringComparer.Ordinal).First(t => t != town);
var max = (int)Math.Max(1_000_000, decimal.Ceiling(import.Accepted.Max(t => t.Price)));
var reports = new List<object>();
foreach (var (name, selectedTown, price) in new [] {("full", "All towns", max),("town",town,max),("different",other,max),("budget",other,500000),("empty","All towns",0),("default","All towns",1000000)}) {
 var t = Stopwatch.StartNew(); state.Filter(selectedTown,price); var filter = t.Elapsed.TotalMilliseconds;
 t.Restart(); var rows = BlockSummaries.Located(state.Visible); var aggregate = t.Elapsed.TotalMilliseconds;
 var features = rows.Select(b => new {type="Feature",id=b.Key,geometry=new{type="Point",coordinates=new[]{b.Latest.Location.Point!.Longitude,b.Latest.Location.Point.Latitude}},properties=new {key=b.Key,transactionId=b.Latest.Id,transactionCount=b.Count,address=b.Latest.Address,medianPrice=b.MedianPrice,priceLabel=$"{b.Count} transactions · median S${b.MedianPrice.ToString("N0",CultureInfo.InvariantCulture)} · latest {b.Latest.Facts.Month}"}}).ToArray();
 t.Restart(); var json=JsonSerializer.Serialize(new{type="FeatureCollection",features}); var serialize=t.Elapsed.TotalMilliseconds;
 File.WriteAllText(Path.Combine(args[1],name+".geojson"),json);
 reports.Add(new{name,town=selectedTown,maximumPrice=price,transactions=state.Visible.Count,locatedTransactions=state.Visible.Count(t=>t.Location.Point is not null),addresses=rows.Count,filterMs=filter,aggregateMs=aggregate,serializationMs=serialize,geojsonBytes=System.Text.Encoding.UTF8.GetByteCount(json),selectedId=rows.FirstOrDefault()?.Latest.Id,selectedKey=rows.FirstOrDefault()?.Key});
}
File.WriteAllText(Path.Combine(args[1],"manifest.json"),JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
