import QtQuick
import QtLocation

// Isolated test scenario, loaded only with HDB_RUNTIME_GATE=1. Assertions are
// test logic; application filtering/selection still belongs to the C# model.
Item {
    required property var targetMap
    required property var targetList
    required property var townControl
    required property var priceControl
    required property var zoomControl
    required property var recenterControl
    property int phase: 0
    property double phaseStarted: Date.now()
    visible: false
    function rows(count) {
        return Resales.visibleCount === count && targetList.count === count
            && targetMap.mapItems.length === count
            && townControl.currentIndex === Resales.townIndex
            && townControl.currentText === Resales.town
            && JSON.parse(Resales.townsJson)[Resales.townIndex] === Resales.town
    }
    function advance(name) {
        console.log("HDB_GATE_STEP " + name)
        phase += 1
        phaseStarted = Date.now()
    }
    Timer {
        id: timer
        interval: 25
        repeat: true
        running: true
        onTriggered: {
            if (Date.now() - phaseStarted > 5000) {
                console.error("HDB_GATE_FAIL state timeout phase " + phase)
                stop(); Qt.quit(); return
            }
            switch (phase) {
            case 0:
                if (!targetMap.mapReady || targetMap.error !== Map.NoError
                        || !rows(6) || targetMap.width <= 0 || targetMap.height <= 0) return
                if (Resales.importDiagnostics !== "" || Resales.selectedId !== "") {
                    console.error("HDB_GATE_FAIL canonical startup"); stop(); Qt.quit(); return
                }
                advance("loaded")
                Resales.setTown("ANG MO KIO")
                break
            case 1:
                if (!rows(2) || townControl.currentText !== "ANG MO KIO") return
                advance("filtered")
                Resales.selectTransaction("HDB-34")
                break
            case 2:
                if (Resales.selectedId !== "HDB-34" || Resales.selectionDetails.indexOf("HDB-34") < 0) return
                advance("selected")
                Resales.setMaximumPrice(238000)
                break
            case 3:
                if (!rows(1) || Resales.selectedId !== "" || priceControl.value !== 238000
                        || targetMap.mapItems[0].transactionId !== "HDB-1188") return
                advance("selection-cleared")
                Resales.selectTransaction("HDB-1188")
                break
            case 4:
                if (Resales.selectedId !== "HDB-1188") return
                advance("remaining-selected")
                // Negative self-check deliberately skips a mutation; normal gate/app are unchanged.
                if (Resales.runtimeGateFault !== "skip-empty") Resales.setMaximumPrice(0)
                break
            case 5:
                if (!rows(0) || Resales.selectedId !== "" || priceControl.value !== 0) return
                advance("empty")
                Resales.resetFilters()
                break
            case 6:
                if (!rows(6) || townControl.currentText !== "All towns" || priceControl.value !== 1000000) return
                advance("reset")
                zoomControl.clicked()
                break
            case 7:
                if (Math.abs(targetMap.zoomLevel - 12) > 0.01) return
                advance("zoomed")
                targetMap.pan(100, 100)
                break
            case 8:
                if (Math.abs(targetMap.center.latitude - 1.3521) < 0.0001) return
                advance("panned")
                recenterControl.clicked()
                break
            case 9:
                if (Math.abs(targetMap.zoomLevel - 11) > 0.01
                        || Math.abs(targetMap.center.latitude - 1.3521) > 0.0001
                        || Math.abs(targetMap.center.longitude - 103.8198) > 0.0001 || !rows(6)) return
                advance("recentered")
                console.log("HDB_GATE_PASS")
                stop(); Qt.quit()
                break
            }
        }
    }
}
