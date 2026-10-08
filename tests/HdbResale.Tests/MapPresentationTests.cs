using System.Text.Json;
using HdbResale.App;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class MapPresentationTests
{
    private static MapAddress Row(string key,double latitude=1.35,double longitude=103.82,int count=1)=>
        new(key,latitude,longitude,"1 TEST ST "+key,count,150,"2026-09");
    private static string[] Members(MapPresentationPlan plan)=>plan.Rows.SelectMany(r=>JsonSerializer.Deserialize<string[]>(r.MembershipJson)!).Order(StringComparer.Ordinal).ToArray();
    private static MapViewport View(double zoom=11,double latitude=1.3521,double longitude=103.8198,double width=1000,double height=600)=>new(latitude,longitude,zoom,width,height);
    [Fact] public void EveryInViewAddressAppearsExactlyOnceAndOutsideTruthIsNotDropped()
    {
        var rows=Enumerable.Range(0,9730).Select(i=>Row($"address-{i:D4}",1.26+i%90*0.002,103.63+i/90*0.004,i%20+1)).ToArray();
        var plan=MapPresentation.Plan(rows,View(),"");
        Assert.Equal(9730,plan.MappedAddresses);
        Assert.Equal(rows.Length,plan.InViewAddresses+rows.Count(r=>!MapPresentation.Contains(View(),r.Latitude,r.Longitude)));
        Assert.Equal(rows.Where(r=>MapPresentation.Contains(View(),r.Latitude,r.Longitude)).Select(r=>r.Key),Members(plan));
        Assert.True(plan.ClusterCount>0);
        Assert.True(plan.Rows.Count<200);
        Assert.Equal(plan.InViewAddresses,plan.Rows.Sum(r=>r.AddressCount));
        Assert.Equal(rows.Length,plan.InViewAddresses);
        Assert.Equal(rows.Sum(r=>r.SalesCount),plan.Rows.Sum(r=>r.TransactionCount));
        var close=MapPresentation.Plan(rows,View(16),"");
        Assert.Equal(rows.Length,close.MappedAddresses);
        Assert.True(close.InViewAddresses<rows.Length);
        Assert.Equal(0,close.ClusterCount);
        Assert.Equal(close.InViewAddresses,close.Rows.Count);
        foreach(var row in close.Rows)
        {
            var source=rows.Single(r=>r.Key==row.Key);
            Assert.Equal(source.Latitude,row.Latitude);
            Assert.Equal(source.Longitude,row.Longitude);
            Assert.Equal(source.SalesCount,row.TransactionCount);
        }
    }
    [Fact] public void LowZoomGroupingIsDeterministicAndDoesNotInventAnAddressOrSale()
    {
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201,3),Row("c",1.3502,103.8202,7)};
        var first=MapPresentation.Plan(rows,View(),"");
        var second=MapPresentation.Plan(rows,View(),"");
        Assert.Equal(first.Rows,second.Rows);
        var cluster=Assert.Single(first.Rows);
        Assert.True(cluster.IsCluster);
        Assert.Equal(3,cluster.AddressCount);Assert.Equal(11,cluster.TransactionCount);
        Assert.StartsWith("@cell:",cluster.Key);
        Assert.Contains("mapped addresses",cluster.Address);
        Assert.Equal("3 addresses · 11 sales · zoom in",cluster.PriceLabel);
        Assert.Equal(new[]{"a","b","c"},Members(first));
    }
    [Fact] public void SelectedAddressIsExtractedWithoutDoubleCountingAndOffscreenSelectionIsPreserved()
    {
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201),Row("c",1.3502,103.8202)};
        var plan=MapPresentation.Plan(rows,View(),"b");
        Assert.True(plan.SelectedInView);
        Assert.Equal(new[]{"a","b","c"},Members(plan));
        var pin=Assert.Single(plan.Rows,r=>r.Key=="b");
        Assert.False(pin.IsCluster);
        Assert.Equal("1 sale · median S$150 · latest 2026-09",pin.PriceLabel);
        var elsewhere=MapPresentation.Plan(rows,View(16,1.2,103.6),"b");
        Assert.False(elsewhere.SelectedInView);Assert.Empty(elsewhere.Rows);
        Assert.Equal(rows.Length,elsewhere.MappedAddresses);
    }
    [Fact] public void NullInvalidAndEmptyViewportNeverChangesCompleteTruth()
    {
        var rows=new[]{Row("a")};
        foreach(var viewport in new MapViewport?[]{null,View(width:0),View(latitude:double.NaN),View(zoom:double.PositiveInfinity)})
        {
            var plan=MapPresentation.Plan(rows,viewport,"a");
            Assert.Equal(1,plan.MappedAddresses);Assert.Empty(plan.Rows);Assert.False(plan.SelectedInView);
        }
        var empty=MapPresentation.Plan([],View(),"");Assert.Equal(0,empty.MappedAddresses);Assert.Empty(empty.Rows);
    }
    [Fact] public void ZoomBoundaryUsesIndividualsAndViewportContainsKnownCenterEdges()
    {
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201)};
        Assert.Equal(0,MapPresentation.Plan(rows,View(15),"").ClusterCount);
        Assert.Equal(2,MapPresentation.Plan(rows,View(15),"").Rows.Count);
        Assert.True(MapPresentation.Contains(View(),1.3521,103.8198));
        Assert.False(MapPresentation.Contains(View(16),1.2,103.6));
        Assert.True(MapPresentation.Contains(new(0,179.99,10,100,100),0,-179.99));
    }
    [Fact] public void ViewportAndFilterReentryRemainFifoWithoutChangingSelectionForCameraOnlyInput()
    {
        var explorer=AddressExplorerTests.Recorded();
        var key=explorer.Addresses[0].AddressKey;explorer.Select(key);
        var queue=new UiMutationQueue();var observed=new List<string>();MapPresentationPlan? plan=null;
        void Project(MapViewport viewport)
        {
            var rows=explorer.Addresses.Select(a=>new MapAddress(a.AddressKey,a.Coordinates.Lat,a.Coordinates.Lng,a.Address,
                a.TransactionCount,a.MedianPrice,a.LatestMonth)).ToArray();
            plan=MapPresentation.Plan(rows,viewport,explorer.Selected?.AddressKey??"");observed.Add(explorer.Selected?.AddressKey??"empty");
        }
        queue.Enqueue(()=>{
            queue.Enqueue(()=>Project(View(16,1.2,103.6)));
            queue.Enqueue(()=>explorer.Filter(AddressFilters.Default with{MaximumPrice=0}));
            queue.Enqueue(()=>Project(View()));
            queue.Enqueue(()=>explorer.Reset());
            queue.Enqueue(()=>Project(View()));
        });
        Assert.Equal(new[]{key,"empty","empty"},observed);Assert.Null(explorer.Selected);
        Assert.Equal(6,plan!.MappedAddresses);Assert.Equal(5,queue.MaximumPendingCount);
    }
    [Fact] public void PresentationDiffRetainsKeysAndNotifiesOnlyChangedRoles()
    {
        var a=MapPresentationRow.AddressRow(Row("a"));var b=MapPresentationRow.AddressRow(Row("b"));
        var updated=a with{TransactionCount=4,PriceLabel="changed"};
        var edits=PresentationDiff.Plan([a,b],[updated]);
        var removal=Assert.Single(edits,e=>e.Kind==MapRowEditKind.Remove);Assert.Equal(1,removal.First);
        var update=Assert.Single(edits,e=>e.Kind==MapRowEditKind.Update);
        Assert.Equal(new[]{260,262},PresentationDiff.ChangedRoles([a],update));
        Assert.Empty(PresentationDiff.Plan([a,b],[a,b]));
        var moved=a with{Latitude=a.Latitude+0.001};
        Assert.Equal(new[]{257},PresentationDiff.ChangedRoles([a],new(MapRowEditKind.Update,0,1,[moved])));
    }
    [Fact] public void ExactViewportEdgesAndZoomThresholdAreIncludedWithoutEpsilonDropping()
    {
        var viewport=new MapViewport(0,0,10,256,256);
        const double edgeLongitude=0.17578125; // 128px at zoom10, 360/(256*2^10) degrees per pixel.
        Assert.True(MapPresentation.Contains(viewport,0,edgeLongitude));
        Assert.True(MapPresentation.Contains(viewport,0,-edgeLongitude));
        Assert.True(MapPresentation.Contains(viewport,0,edgeLongitude-1e-8));
        Assert.False(MapPresentation.Contains(viewport,0,edgeLongitude+1e-8));
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201)};
        Assert.Equal(1,MapPresentation.Plan(rows,View(14.999),"").ClusterCount);
        Assert.Equal(0,MapPresentation.Plan(rows,View(15),"").ClusterCount);
        Assert.True(new MapViewport(85.05,0,11,256,256).IsValid);
        Assert.True(new MapViewport(-85.05,0,11,256,256).IsValid);
        Assert.False(new MapViewport(85.06,0,11,256,256).IsValid);
    }
    [Fact] public void ClusterMembershipChangesKeepCellIdentityButUpdateCountsAndCentroid()
    {
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201),Row("c",1.3502,103.8202,10)};
        var before=Assert.Single(MapPresentation.Plan(rows,View(),"").Rows);
        var after=Assert.Single(MapPresentation.Plan(rows.Take(2).ToArray(),View(),"").Rows);
        Assert.Equal(before.Key,after.Key);Assert.NotEqual(before.Latitude,after.Latitude);
        Assert.Equal(2,after.AddressCount);Assert.Equal(2,after.TransactionCount);
        var edit=Assert.Single(PresentationDiff.Plan([before],[after]));
        Assert.Equal(MapRowEditKind.Update,edit.Kind);
        Assert.Equal(new[]{257,258,259,260,262,263},PresentationDiff.ChangedRoles([before],edit));
    }
    [Fact] public void PresentationDiffRejectsDuplicatesOrNonOrdinalOrder()
    {
        var a=MapPresentationRow.AddressRow(Row("a"));var b=MapPresentationRow.AddressRow(Row("b"));
        Assert.Throws<ArgumentException>(()=>PresentationDiff.Plan([b,a],[]));
        Assert.Throws<ArgumentException>(()=>PresentationDiff.Plan([],[a,a]));
    }
}
