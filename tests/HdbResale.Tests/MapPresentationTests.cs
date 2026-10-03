using System.Text.Json;
using HdbResale.App;
using HdbResale.Domain;
using Xunit;
namespace HdbResale.Tests;
public sealed class MapPresentationTests
{
    private static readonly ResaleTransaction Transaction=ExplorerStateTests.Fixture().Accepted[0];
    private static BlockSummary Row(string key,double latitude=1.35,double longitude=103.82,int count=1)=>new(key,
        Transaction with {Id="tx-"+key,Location=new(new(latitude,longitude),CoordinateQuality.BlockApproximation,"test")},count,100,200,150);
    private static string[] Members(MapPresentationPlan plan)=>plan.Rows.SelectMany(r=>JsonSerializer.Deserialize<string[]>(r.MembershipJson)!).Order(StringComparer.Ordinal).ToArray();
    private static MapViewport View(double zoom=11,double latitude=1.3521,double longitude=103.8198,double width=1000,double height=600)=>new(latitude,longitude,zoom,width,height);
    [Fact] public void EveryInViewAddressAppearsExactlyOnceAndOutsideTruthIsNotDropped()
    {
        var rows=Enumerable.Range(0,7618).Select(i=>Row($"address-{i:D4}",1.26+i%90*0.002,103.63+i/90*0.004,i%20+1)).ToArray();
        var plan=MapPresentation.Plan(rows,View(),"");
        Assert.Equal(7618,plan.MappedAddresses);
        Assert.Equal(7618,plan.InViewAddresses);
        Assert.Equal(rows.Select(r=>r.Key),Members(plan));
        Assert.True(plan.ClusterCount>0);
        Assert.True(plan.Rows.Count<200);
        Assert.Equal(rows.Sum(r=>r.Count),plan.Rows.Sum(r=>r.TransactionCount));
        Assert.Equal(plan.InViewAddresses,plan.Rows.Sum(r=>r.AddressCount));
        var close=MapPresentation.Plan(rows,View(16),"");
        Assert.Equal(rows.Length,close.MappedAddresses);
        Assert.True(close.InViewAddresses<rows.Length);
        Assert.Equal(0,close.ClusterCount);
        Assert.Equal(close.InViewAddresses,close.Rows.Count);
        foreach(var row in close.Rows)
        {
            var source=rows.Single(r=>r.Key==row.Key);
            Assert.Equal(source.Latest.Id,row.TransactionId);
            Assert.Equal(source.Latest.Location.Point!.Latitude,row.Latitude);
            Assert.Equal(source.Latest.Location.Point.Longitude,row.Longitude);
        }
    }
    [Fact] public void LowZoomGroupingIsDeterministicAndDoesNotInventAnAddressOrTransaction()
    {
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201,3),Row("c",1.3502,103.8202,7)};
        var first=MapPresentation.Plan(rows,View(),"");
        var second=MapPresentation.Plan(rows,View(),"");
        Assert.Equal(first.Rows,second.Rows);
        var cluster=Assert.Single(first.Rows);
        Assert.True(cluster.IsCluster);Assert.Equal("",cluster.TransactionId);
        Assert.Equal(3,cluster.AddressCount);Assert.Equal(11,cluster.TransactionCount);
        Assert.StartsWith("@cell:",cluster.Key);
        Assert.Contains("mapped addresses",cluster.Address);
        Assert.Equal(new[]{"a","b","c"},Members(first));
    }
    [Fact] public void SelectedAddressIsExtractedWithoutDoubleCountingAndOffscreenSelectionIsPreserved()
    {
        var rows=new[]{Row("a"),Row("b",1.3501,103.8201),Row("c",1.3502,103.8202)};
        var plan=MapPresentation.Plan(rows,View(),"b");
        Assert.True(plan.SelectedInView);
        Assert.Equal(new[]{"a","b","c"},Members(plan));
        var pin=Assert.Single(plan.Rows,r=>r.Key=="b");
        Assert.False(pin.IsCluster);Assert.Equal("tx-b",pin.TransactionId);
        var state=new ExplorerState(rows.Select(r=>r.Latest).ToArray());state.Select("tx-b");
        var elsewhere=MapPresentation.Plan(rows,View(16,1.2,103.6),"b");
        Assert.False(elsewhere.SelectedInView);Assert.Empty(elsewhere.Rows);
        Assert.Equal("tx-b",state.Selected!.Id);
        Assert.Equal(rows.Length,elsewhere.MappedAddresses);
        state.Filter("All towns",0);Assert.Null(state.Selected);
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
        var state=new ExplorerState(ExplorerStateTests.Fixture().Accepted);var id=state.Visible[0].Id;state.Select(id);
        var queue=new UiMutationQueue();var observed=new List<string>();MapPresentationPlan? plan=null;
        void Project(MapViewport viewport){plan=MapPresentation.Plan(BlockSummaries.Located(state.Visible),viewport,state.Selected is null?"":BlockSummaries.Key(state.Selected));observed.Add(state.Selected?.Id??"empty");}
        queue.Enqueue(()=>{
            queue.Enqueue(()=>Project(View(16,1.2,103.6)));
            queue.Enqueue(()=>state.Filter("All towns",0));
            queue.Enqueue(()=>Project(View()));
            queue.Enqueue(()=>state.Reset());
            queue.Enqueue(()=>Project(View()));
        });
        Assert.Equal(new[]{id,"empty","empty"},observed);Assert.Null(state.Selected);
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
