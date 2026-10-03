import QtQuick
import QtLocation
import QtPositioning
Item {
    required property var targetMap
    required property var townControl
    required property var priceControl
    required property var targetList
    property int phase: 0
    property double started: Date.now()
    property string chosen: ""
    visible: false
    function ready(rows, mapped) {
        return Resales.visibleCount === rows && targetList.count === rows
            && targetMap.mapItems.length === mapped
            && townControl.currentIndex === Resales.townIndex
            && townControl.currentText === Resales.town
            && JSON.parse(Resales.townsJson)[Resales.townIndex] === Resales.town
            && priceControl.value === Resales.maximumPrice
    }
    function advance(name) {
        console.log("HDB_SCALE_STEP " + name + " ms=" + (Date.now()-started)
            + " rows=" + Resales.visibleCount + " delegates=" + targetMap.mapItems.length)
        phase++; started=Date.now()
    }
    Timer {
        interval: 25; repeat: true; running: true
        onTriggered: {
            if (Date.now()-started > 5000) {
                console.error("HDB_GATE_FAIL scale phase " + phase); stop(); Qt.quit(); return
            }
            switch (phase) {
            case 0:
                if (!targetMap.mapReady || targetMap.error !== Map.NoError || targetMap.width <= 0 || targetMap.height <= 0 || !ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
                advance("loaded"); Resales.setTown(Resales.gateTown); break
            case 1:
                if (!ready(Resales.gateTownCount, Resales.gateTownMapped)) return
                advance("filtered"); Resales.setMaximumPrice(500000); break
            case 2:
                if (!ready(Resales.gateBudgetCount, Resales.gateBudgetMapped)) return
                advance("budget-filtered"); chosen=Resales.firstVisibleId; Resales.selectTransaction(chosen); break
            case 3:
                if (chosen === "" || Resales.selectedId !== chosen) return
                advance("selected"); if (Resales.runtimeGateFault !== "skip-empty") Resales.setMaximumPrice(0); break
            case 4:
                if (!ready(0,0) || Resales.selectedId !== "") return
                advance("empty-cleared"); Resales.resetFilters(); break
            case 5:
                if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
                advance("reset"); targetMap.zoomLevel=12; break
            case 6:
                if (Math.abs(targetMap.zoomLevel-12)>0.01) return
                advance("zoomed"); targetMap.pan(100,100); break
            case 7:
                if (Math.abs(targetMap.center.latitude-1.3521)<0.0001) return
                advance("panned"); targetMap.center=QtPositioning.coordinate(1.3521,103.8198); targetMap.zoomLevel=11; break
            case 8:
                if (Math.abs(targetMap.center.latitude-1.3521)>0.0001 || Math.abs(targetMap.zoomLevel-11)>0.01) return
                advance("recentered"); Resales.setMaximumPrice(Resales.maximumAvailablePrice); break
            case 9:
                if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
                advance("all-prices"); Resales.measureScaleHeap(); console.log("HDB_SCALE_PASS"); stop(); Qt.quit(); break
            }
        }
    }
}
