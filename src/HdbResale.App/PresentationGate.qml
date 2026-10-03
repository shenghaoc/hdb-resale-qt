import QtQuick
import QtLocation
import QtPositioning
Item {
    required property var attributionImage
    required property var targetMap
    required property var targetList
    required property var townControl
    required property var priceControl
    property int phase: 0
    property double started: Date.now()
    property string selected: ""
    property string selectedKey: ""
    property var priorItems: ({})
    property bool gcChecked: false
    property bool burstArmed: false
    property int burstFired: 0
    property int priorCreated: 0
    property int priorDestroyed: 0
    property double selectedLatitude: 0
    property double selectedLongitude: 0
    visible: false
    Connections {
        target: Resales.mapPoints
        function onRowsAboutToBeRemoved(parent, first, last) {
            if (!burstArmed || Resales.runtimeGateFault === "skip-burst") return
            burstArmed = false; burstFired++
            Resales.setMaximumPrice(0)
            Resales.selectTransaction(selected)
            Resales.resetFilters()
            Resales.setMaximumPrice(Resales.maximumAvailablePrice)
            targetMap.center = QtPositioning.coordinate(1.3521,103.8198)
            targetMap.zoomLevel = 11
            console.log("HDB_PRESENTATION_BURST queued=" + Resales.gateMaximumQueuedMutations)
        }
    }
    function identitiesAgree() {
        const expected = JSON.parse(Resales.gateMapRowsJson)
        const truth = JSON.parse(Resales.gateTruthRowsJson)
        if (truth.length !== Resales.mappedCount || expected.length !== Resales.presentationCount) return false
        const truthByKey = {}; const expectedInView = {}; let expectedCount = 0
        // Independent native Qt projection oracle, not the C# plan's own counts.
        const origin = targetMap.fromCoordinate(QtPositioning.coordinate(0,0),false)
        const worldSize = 256*Math.pow(2,targetMap.zoomLevel)
        const gridZoom = Math.floor(targetMap.zoomLevel)
        const cellSize = 64*Math.pow(2,targetMap.zoomLevel-gridZoom)
        for (const row of truth) {
            truthByKey[row.mapKey] = row
            const point = targetMap.fromCoordinate(QtPositioning.coordinate(row.latitude,row.longitude),false)
            if (point.x >= 0 && point.x <= targetMap.width && point.y >= 0 && point.y <= targetMap.height) {
                const cellX=Math.floor((point.x-origin.x+worldSize/2)/cellSize)
                const cellY=Math.floor((point.y-origin.y+worldSize/2)/cellSize)
                expectedInView[row.mapKey]="@cell:"+gridZoom+":"+cellX+":"+cellY
                expectedCount++
            }
        }
        if (expectedCount !== Resales.inViewAddressCount) return false
        const byKey = {}; for (const row of expected) byKey[row.mapKey] = row
        const seen = {}; const addresses = {}; let represented = 0; let clusters = 0
        for (const item of targetMap.mapItems) {
            const row = byKey[item.mapKey]
            if (!row || seen[item.mapKey] || (priorItems[item.mapKey] && priorItems[item.mapKey] !== item)
                || item.transactionId !== row.transactionId || item.transactionCount !== row.transactionCount
                || item.addressCount !== row.addressCount || item.address !== row.address || item.priceLabel !== row.priceLabel
                || Math.abs(item.latitude-row.latitude)>1e-10 || Math.abs(item.longitude-row.longitude)>1e-10) return false
            if (row.members.length !== row.addressCount) return false
            let transactionCount=0
            for (const key of row.members) {
                if (addresses[key] || !truthByKey[key] || !expectedInView[key]) return false
                if (row.addressCount > 1 && expectedInView[key] !== row.mapKey) return false
                addresses[key]=true; transactionCount+=truthByKey[key].transactionCount
            }
            if (transactionCount !== row.transactionCount) return false
            if (row.addressCount === 1) {
                const source=truthByKey[row.members[0]]
                if (row.mapKey !== source.mapKey || row.transactionId !== source.transactionId
                    || Math.abs(row.latitude-source.latitude)>1e-10 || Math.abs(row.longitude-source.longitude)>1e-10) return false
            } else { if (row.transactionId !== "" || targetMap.zoomLevel >= 15) return false; clusters++ }
            represented += row.addressCount; seen[item.mapKey]=true
        }
        for (const key in expectedInView) if (!addresses[key]) return false
        return targetMap.mapItems.length === expected.length && represented === expectedCount && clusters === Resales.clusterCount
    }
    function ready(rows,mapped) {
        if (!targetMap.mapReady || targetMap.viewportPending || !Resales.mapViewportReady
            || attributionImage.status !== Image.Ready || targetMap.error !== Map.NoError
            || Resales.visibleCount !== rows || targetList.count !== Resales.addressCount || Resales.mappedCount !== mapped
            || targetMap.mapItems.length !== Resales.presentationCount
            || Math.abs(Resales.mapViewportLatitude-targetMap.center.latitude)>1e-10
            || Math.abs(Resales.mapViewportLongitude-targetMap.center.longitude)>1e-10
            || Math.abs(Resales.mapViewportZoom-targetMap.zoomLevel)>1e-10
            || Resales.mapViewportWidth !== targetMap.width || Resales.mapViewportHeight !== targetMap.height
            || townControl.currentText !== Resales.town || townControl.currentIndex !== Resales.townIndex
            || JSON.parse(Resales.townsJson)[Resales.townIndex] !== Resales.town || priceControl.value !== Resales.maximumPrice || !identitiesAgree()) return false
        if (!gcChecked) { gcChecked=true; gc(); console.log("HDB_PRESENTATION_GC"); return identitiesAgree() }
        return true
    }
    function advance(name) {
        const snapshot={}; for(const item of targetMap.mapItems)snapshot[item.mapKey]=item
        const elapsed=Date.now()-started
        if (elapsed>((Resales.scaleExpandedCoverage||Resales.scaleMeasurement)?10000:5000)) { fail(); return false }
        console.log("HDB_PRESENTATION_STEP " + name + " ms=" + elapsed + " rows=" + Resales.visibleCount
            + " mapped=" + Resales.mappedCount + " in-view=" + Resales.inViewAddressCount + " objects=" + targetMap.mapItems.length
            + " clusters=" + Resales.clusterCount + " created=" + (targetMap.createdDelegates-priorCreated)
            + " destroyed=" + (targetMap.destroyedDelegates-priorDestroyed) + " zoom=" + targetMap.zoomLevel
            + " width="+targetMap.width+" height="+targetMap.height+" latitude="+targetMap.center.latitude+" longitude="+targetMap.center.longitude)
        if (Date.now()-started>((Resales.scaleExpandedCoverage||Resales.scaleMeasurement)?10000:5000)) { fail(); return false }
        priorItems=snapshot; priorCreated=targetMap.createdDelegates; priorDestroyed=targetMap.destroyedDelegates
        phase++; started=Date.now(); return true
    }
    function fail() {
        console.error("HDB_GATE_FAIL presentation phase="+phase+" elapsed="+(Date.now()-started)
            +" truth="+Resales.mappedCount+" objects="+targetMap.mapItems.length+" expected="+Resales.presentationCount+" identities="+identitiesAgree())
        targetMap.traceDelegates=false; timer.stop(); Qt.quit()
    }
    Timer {
        id: timer; interval: 25; running: true; repeat: true
        onTriggered: {
            if(Date.now()-started>((Resales.scaleExpandedCoverage||Resales.scaleMeasurement)?10000:5000)){fail();return}
            switch(phase) {
            case 0:
                if(!ready(Resales.gateInitialCount,Resales.gateInitialMapped))return
                if(!advance("loaded"))return; Resales.setMaximumPrice(Resales.maximumAvailablePrice);break
            case 1:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped))return
                if(Resales.gateAllMapped>1000 && (Resales.clusterCount===0 || Resales.presentationCount>=Resales.mappedCount/2))return
                if(!advance("full"))return; Resales.setTown(Resales.gateTown);break
            case 2:
                if(!ready(Resales.gateAllTownCount,Resales.gateAllTownMapped))return
                if(!advance("town"))return;Resales.setTown(Resales.gateOtherTown);break
            case 3:
                if(!ready(Resales.gateOtherTownCount,Resales.gateOtherTownMapped))return
                if(!advance("different"))return;Resales.setMaximumPrice(500000);break
            case 4:
                if(!ready(Resales.gateOtherBudgetCount,Resales.gateOtherBudgetMapped))return
                if(!advance("budget"))return;Resales.setTown("All towns");Resales.setMaximumPrice(Resales.maximumAvailablePrice);break
            case 5:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped))return
                if(!advance("small-to-full"))return;if(Resales.runtimeGateFault!=="skip-empty")Resales.setMaximumPrice(0);break
            case 6:
                if(!ready(0,0)||Resales.selectedId!=="")return
                if(!advance("empty"))return;Resales.setMaximumPrice(Resales.maximumAvailablePrice);break
            case 7:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped))return
                selected=Resales.gateTownSelectionId
                if(!advance("empty-to-full"))return;Resales.selectTransaction(selected);break
            case 8:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||Resales.selectedId!==selected)return
                selectedKey=Resales.selectedMapKey;selectedLatitude=Resales.selectedLatitude;selectedLongitude=Resales.selectedLongitude
                let marker=null;for(const item of targetMap.mapItems)if(item.mapKey===selectedKey)marker=item
                if(!marker||!marker.selected||marker.z!==1)return
                if(!advance("selection-only"))return;targetMap.center=QtPositioning.coordinate(selectedLatitude,selectedLongitude);targetMap.zoomLevel=16;break
            case 9:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||Resales.clusterCount!==0||targetMap.zoomLevel!==16)return
                if(!advance("individuals"))return;targetMap.center=QtPositioning.coordinate(1.2,103.6);break
            case 10:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||Resales.selectedId!==selected||Resales.selectionMapStatus.indexOf("outside")<0)return
                for(const item of targetMap.mapItems)if(item.mapKey===selectedKey)return
                if(!advance("selection-offscreen"))return;targetMap.center=QtPositioning.coordinate(selectedLatitude,selectedLongitude);break
            case 11:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||Resales.selectedId!==selected)return
                if(!advance("selection-returned"))return;Resales.setTown(Resales.gateOtherTown);break
            case 12:
                if(!ready(Resales.gateOtherTownCount,Resales.gateOtherTownMapped)||Resales.selectedId!==""||Resales.selectedMapKey!=="")return
                if(!advance("domain-hidden-cleared"))return;Resales.setTown("All towns");targetMap.center=QtPositioning.coordinate(1.3521,103.8198);targetMap.zoomLevel=11;break
            case 13:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped))return
                if(!advance("recentered"))return;targetMap.zoomLevel=12;break
            case 14:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||targetMap.zoomLevel!==12)return
                if(!advance("zoom-twelve"))return;targetMap.zoomLevel=13;break
            case 15:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||targetMap.zoomLevel!==13)return
                if(!advance("zoom-thirteen"))return;targetMap.zoomLevel=14;break
            case 16:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||targetMap.zoomLevel!==14)return
                if(!advance("zoom-fourteen"))return;targetMap.zoomLevel=15;break
            case 17:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||targetMap.zoomLevel!==15||Resales.clusterCount!==0)return
                if(!advance("zoom-fifteen"))return;targetMap.zoomLevel=11;break
            case 18:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped))return
                if(!advance("before-burst"))return;priorItems=({});burstArmed=true;Resales.setTown(Resales.gateTown);break
            case 19:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||burstFired!==1||Resales.gateMaximumQueuedMutations<4||Resales.selectedId!=="")return
                if(!advance("rapid-reentrant"))return
                // Multiple camera events in one turn must flush exactly the final camera.
                targetMap.zoomLevel=13;targetMap.pan(80,80);targetMap.zoomLevel=16;targetMap.center=QtPositioning.coordinate(selectedLatitude,selectedLongitude)
                break
            case 20:
                if(!ready(Resales.gateAllCount,Resales.gateAllMapped)||Resales.clusterCount!==0||targetMap.zoomLevel!==16)return
                if(!advance("rapid-viewport"))return;Resales.resetFilters();targetMap.center=QtPositioning.coordinate(1.3521,103.8198);targetMap.zoomLevel=11;break
            case 21:
                if(!ready(Resales.gateInitialCount,Resales.gateInitialMapped)||Resales.selectedId!=="")return
                if(!advance("reset"))return;console.log("HDB_PRESENTATION_PASS");targetMap.traceDelegates=false;stop();Qt.quit();break
            }
        }
    }
}
