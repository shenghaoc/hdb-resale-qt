import QtQuick
import QtLocation
import QtPositioning
Item {
    required property var attributionImage
    required property var targetMap
    required property var townControl
    required property var priceControl
    required property var targetList
    property int phase: 0
    property double started: Date.now()
    property string chosen: ""
    property bool burstArmed: false
    property int burstFired: 0
    property var retainedSelectedItem: null
    Connections {
        target: Resales.mapPoints
        enabled: Resales.scaleReentrant
        function onRowsAboutToBeRemoved(parent, first, last) {
            if (!burstArmed || Resales.runtimeGateFault === "skip-burst") return
            burstArmed=false; burstFired++
            // Deliberately reenter while Bridge is synchronizing BeginRemoveRows.
            Resales.setMaximumPrice(0)
            Resales.selectTransaction(chosen)
            Resales.resetFilters()
            Resales.setMaximumPrice(Resales.maximumAvailablePrice)
            console.log("HDB_MAP_BURST fired=" + burstFired + " queued=" + Resales.gateMaximumQueuedMutations)
        }
    }
    property var priorItems: ({})
    property int priorCreated: 0
    property int priorDestroyed: 0
    property int observedRevision: Resales.mapRevision
    property double observedMs: 0
    onObservedRevisionChanged: {
        observedMs = Date.now()
        console.log("HDB_MAP_OBSERVED revision=" + observedRevision + " after-csharp-ms=" + (observedMs - Resales.lastFilterCompletedMs))
    }
    visible: false
    function identitiesAgree() {
        const expected = JSON.parse(Resales.gateMapRowsJson)
        const incremental = Resales.mapUpdateStrategy === "incremental"
        const byKey = {}
        for (const row of expected) byKey[row.mapKey] = row
        const seen = {}
        for (const item of targetMap.mapItems) {
            const row = byKey[item.mapKey]
            if (!row || seen[item.mapKey]
                || (incremental && priorItems[item.mapKey] && priorItems[item.mapKey] !== item)
                || item.transactionId !== row.transactionId
                || item.transactionCount !== row.transactionCount || item.address !== row.address
                || item.priceLabel !== row.priceLabel || Math.abs(item.latitude-row.latitude)>1e-10
                || Math.abs(item.longitude-row.longitude)>1e-10) return false
            seen[item.mapKey] = true
        }
        return targetMap.mapItems.length === expected.length
    }
    function ready(rows, mapped) {
        return attributionImage.status === Image.Ready && Resales.visibleCount === rows && targetList.count === rows
            && targetMap.mapItems.length === mapped
            && townControl.currentIndex === Resales.townIndex
            && townControl.currentText === Resales.town
            && JSON.parse(Resales.townsJson)[Resales.townIndex] === Resales.town
            && priceControl.value === Resales.maximumPrice && identitiesAgree()
    }
    function advance(name) {
        console.log("HDB_SCALE_STEP " + name + " ms=" + (Date.now()-started)
            + " rows=" + Resales.visibleCount + " delegates=" + targetMap.mapItems.length
            + " created=" + (targetMap.createdDelegates-priorCreated)
            + " destroyed=" + (targetMap.destroyedDelegates-priorDestroyed)
            + " last-create-ms=" + (targetMap.lastDelegateCreatedMs >= started ? targetMap.lastDelegateCreatedMs-started : -1)
            + " last-destroy-ms=" + (targetMap.lastDelegateDestroyedMs >= started ? targetMap.lastDelegateDestroyedMs-started : -1)
            + " observed-ms=" + (observedMs >= started ? observedMs-started : -1))
        const snapshot = {}
        for (const item of targetMap.mapItems) snapshot[item.mapKey] = item
        priorItems=snapshot
        priorCreated=targetMap.createdDelegates; priorDestroyed=targetMap.destroyedDelegates
        phase++; started=Date.now()
    }
    function reentrantStep() {
        switch (phase) {
        case 0:
            if (!targetMap.mapReady || targetMap.error !== Map.NoError || !ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            chosen=Resales.gateHiddenSelectionId
            if (chosen === "") return
            advance("loaded"); Resales.selectTransaction(chosen); break
        case 1:
            if (Resales.selectedId !== chosen) return
            advance("selected-retained-address"); Resales.setTown(Resales.gateTown); break
        case 2:
            if (!ready(Resales.gateTownCount, Resales.gateTownMapped) || Resales.selectedId !== chosen) return
            for (const item of targetMap.mapItems) if (item.mapKey === Resales.gateRetainedSelectionKey) { retainedSelectedItem=item; break }
            if (!retainedSelectedItem) return
            advance("selected-in-town"); Resales.setMaximumPrice(500000); break
        case 3:
            if (!ready(Resales.gateBudgetCount, Resales.gateBudgetMapped) || Resales.selectedId !== "" || Resales.selectedMapKey !== "") return
            let retained=false
            for (const item of targetMap.mapItems) if (item.mapKey === Resales.gateRetainedSelectionKey && item === retainedSelectedItem && item.z === 0) retained=true
            if (!retained) return
            advance("hidden-transaction-cleared"); Resales.resetFilters(); break
        case 4:
            if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            chosen=Resales.firstVisibleId
            advance("reset"); Resales.selectTransaction(chosen); break
        case 5:
            if (chosen === "" || Resales.selectedId !== chosen) return
            advance("selected-for-burst")
            // Queued empty intentionally destroys all keys before restoring them.
            priorItems=({}); burstArmed=true; Resales.setTown(Resales.gateTown); break
        case 6:
            if (burstFired !== 1 || Resales.gateMaximumQueuedMutations < 4
                || !ready(Resales.gateAllCount, Resales.gateAllMapped) || Resales.selectedId !== "") return
            if (Resales.scaleLifecycle && (targetMap.createdDelegates-priorCreated !== Resales.gateAllMapped
                || targetMap.destroyedDelegates-priorDestroyed !== Resales.gateInitialMapped)) return
            advance("reentrant-burst-drained"); Resales.resetFilters(); break
        case 7:
            if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped) || Resales.selectedId !== "") return
            advance("final-reset"); console.log("HDB_SCALE_PASS"); targetMap.traceDelegates=false; return true
        }
        return false
    }
    function extendedStep() {
        switch (phase) {
        case 0:
            if (!targetMap.mapReady || targetMap.error !== Map.NoError || !ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            advance("loaded"); Resales.setMaximumPrice(Resales.maximumAvailablePrice); break
        case 1:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            advance("full"); Resales.setTown(Resales.gateTown); break
        case 2:
            if (!ready(Resales.gateAllTownCount, Resales.gateAllTownMapped)) return
            advance("subset"); Resales.setTown(Resales.gateOtherTown); break
        case 3:
            if (!ready(Resales.gateOtherTownCount, Resales.gateOtherTownMapped)) return
            advance("different"); if (Resales.runtimeGateFault !== "skip-empty") Resales.setMaximumPrice(0); break
        case 4:
            if (!ready(0, 0) || Resales.selectedId !== "") return
            advance("empty"); Resales.resetFilters(); break
        case 5:
            if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            advance("reset"); Resales.setMaximumPrice(Resales.maximumAvailablePrice); break
        case 6:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            chosen=""
            for (const item of targetMap.mapItems) if (item.mapKey.startsWith(Resales.gateTown + "|")) { chosen=item.transactionId; break }
            if (chosen === "") return
            advance("full-restored"); Resales.selectTransaction(chosen); break
        case 7:
            if (Resales.selectedId !== chosen || !ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            let selected=null
            for (const item of targetMap.mapItems) if (item.transactionId === chosen) { selected=item; break }
            if (!selected || !selected.visible || selected.z !== 1) return
            advance("selected"); Resales.setTown(Resales.gateOtherTown); break
        case 8:
            if (!ready(Resales.gateOtherTownCount, Resales.gateOtherTownMapped) || Resales.selectedId !== "" || Resales.selectedMapKey !== "") return
            advance("hidden-selection-cleared"); Resales.setTown("All towns"); break
        case 9:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            advance("full-again"); Resales.setTown(Resales.gateTown); break
        case 10:
            if (!ready(Resales.gateAllTownCount, Resales.gateAllTownMapped)) return
            advance("repeat-subset"); Resales.setTown(Resales.gateOtherTown); break
        case 11:
            if (!ready(Resales.gateOtherTownCount, Resales.gateOtherTownMapped)) return
            advance("repeat-different"); Resales.setTown("All towns"); break
        case 12:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            advance("repeat-full"); targetMap.zoomLevel=12; break
        case 13:
            if (Math.abs(targetMap.zoomLevel-12)>0.01) return
            advance("zoomed"); targetMap.pan(100,100); break
        case 14:
            if (Math.abs(targetMap.center.latitude-1.3521)<0.0001) return
            advance("panned"); targetMap.center=QtPositioning.coordinate(1.3521,103.8198); targetMap.zoomLevel=11; break
        case 15:
            if (Math.abs(targetMap.center.latitude-1.3521)>0.0001 || Math.abs(targetMap.zoomLevel-11)>0.01 || !ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            advance("recentered"); console.log("HDB_SCALE_PASS"); targetMap.traceDelegates=false; return true
        }
        return false
    }
    Timer {
        interval: 25; repeat: true; running: true
        onTriggered: {
            if (Date.now()-started > (Resales.scaleMeasurement ? 10000 : 5000)) {
                console.error("HDB_GATE_FAIL scale phase " + phase + " mapReady=" + targetMap.mapReady + " mapError=" + targetMap.error + " rows=" + Resales.visibleCount + " list=" + targetList.count + " delegates=" + targetMap.mapItems.length + " expected=" + Resales.mappedCount + " identities=" + identitiesAgree() + " town=" + townControl.currentText + " price=" + priceControl.value)
                if (targetMap.mapItems.length) {
                    const item=targetMap.mapItems[0]; const rows=JSON.parse(Resales.gateMapRowsJson)
                    console.log("HDB_MAP_DIAGNOSTIC " + JSON.stringify({key:item.mapKey,id:item.transactionId,count:item.transactionCount,address:item.address,price:item.priceLabel,latitude:item.latitude,longitude:item.longitude,expected:rows.find(r=>r.mapKey===item.mapKey)}))
                }
                targetMap.traceDelegates=false; stop(); Qt.quit(); return
            }
            if (Resales.scaleReentrant) { if (reentrantStep()) { stop(); Qt.quit() }; return }
            if (Resales.scaleTransitions) { if (extendedStep()) { stop(); Qt.quit() }; return }
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
                advance("all-prices"); Resales.measureScaleHeap(); console.log("HDB_SCALE_PASS"); targetMap.traceDelegates=false; stop(); Qt.quit(); break
            }
        }
    }
}
