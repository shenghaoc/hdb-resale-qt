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
    property bool modelLifetimeChecked: false
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
    property int priorPresentationCount: 0
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
                || item.transactionCount !== row.transactionCount || item.addressCount !== row.addressCount || item.address !== row.address
                || item.priceLabel !== row.priceLabel || Math.abs(item.latitude-row.latitude)>1e-10
                || Math.abs(item.longitude-row.longitude)>1e-10) return false
            seen[item.mapKey] = true
        }
        return targetMap.mapItems.length === expected.length
    }
    function ready(rows, mapped) {
        const complete = attributionImage.status === Image.Ready && Resales.visibleCount === rows && targetList.count === Resales.addressCount
            && Resales.mappedCount === mapped && targetMap.mapItems.length === Resales.presentationCount
            && Resales.mapViewportReady && !targetMap.viewportPending
            && Math.abs(Resales.mapViewportLatitude-targetMap.center.latitude)<1e-10
            && Math.abs(Resales.mapViewportLongitude-targetMap.center.longitude)<1e-10
            && Math.abs(Resales.mapViewportZoom-targetMap.zoomLevel)<1e-10
            && townControl.currentIndex === Resales.townIndex
            && townControl.currentText === Resales.town
            && JSON.parse(Resales.townsJson)[Resales.townIndex] === Resales.town
            && priceControl.value === Resales.maximumPrice && identitiesAgree()
        if (!complete) return false
        if (!modelLifetimeChecked) {
            modelLifetimeChecked = true
            // Gate-only lifetime regression. Normal UI/startup never forces QML GC.
            gc()
            console.log("HDB_MODEL_LIFETIME_GC")
            return identitiesAgree()
        }
        return true
    }
    function failDeadline(elapsed) {
        console.error("HDB_GATE_FAIL scale phase " + phase + " elapsed-ms=" + elapsed
            + " budget-ms=" + ((Resales.scaleMeasurement || Resales.scaleExpandedCoverage) ? 10000 : 5000)
            + " mapReady=" + targetMap.mapReady + " mapError=" + targetMap.error
            + " rows=" + Resales.visibleCount + " list=" + targetList.count
            + " delegates=" + targetMap.mapItems.length + " expected=" + Resales.mappedCount
            + " identities=" + identitiesAgree() + " town=" + townControl.currentText + " price=" + priceControl.value)
        targetMap.traceDelegates=false; gateTimer.stop(); Qt.quit()
    }
    function advance(name) {
        const snapshot = {}
        for (const item of targetMap.mapItems) snapshot[item.mapKey] = item
        // Callback-entry checks alone miss synchronous work or a costly identity
        // scan. Include completed validation/snapshot work in every phase budget.
        const elapsed = Date.now()-started
        if (elapsed > ((Resales.scaleMeasurement || Resales.scaleExpandedCoverage) ? 10000 : 5000)) { failDeadline(elapsed); return false }
        console.log("HDB_SCALE_STEP " + name + " ms=" + elapsed
            + " rows=" + Resales.visibleCount + " delegates=" + targetMap.mapItems.length
            + " created=" + (targetMap.createdDelegates-priorCreated)
            + " destroyed=" + (targetMap.destroyedDelegates-priorDestroyed)
            + " last-create-ms=" + (targetMap.lastDelegateCreatedMs >= started ? targetMap.lastDelegateCreatedMs-started : -1)
            + " last-destroy-ms=" + (targetMap.lastDelegateDestroyedMs >= started ? targetMap.lastDelegateDestroyedMs-started : -1)
            + " observed-ms=" + (observedMs >= started ? observedMs-started : -1))
        if (Date.now()-started > ((Resales.scaleMeasurement || Resales.scaleExpandedCoverage) ? 10000 : 5000)) { failDeadline(Date.now()-started); return false }
        priorItems=snapshot
        priorCreated=targetMap.createdDelegates; priorDestroyed=targetMap.destroyedDelegates; priorPresentationCount=targetMap.mapItems.length
        phase++; started=Date.now()
        return true
    }
    function reentrantStep() {
        switch (phase) {
        case 0:
            if (!targetMap.mapReady || targetMap.error !== Map.NoError || !ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            chosen=Resales.gateHiddenSelectionId
            if (chosen === "") return
            if (!advance("loaded")) return; Resales.selectTransaction(chosen); break
        case 1:
            if (Resales.selectedId !== chosen || !ready(Resales.gateInitialCount,Resales.gateInitialMapped)) return
            if (!advance("selected-retained-address")) return;
            targetMap.center=QtPositioning.coordinate(Resales.selectedLatitude,Resales.selectedLongitude); targetMap.zoomLevel=16
            Resales.setTown(Resales.gateTown); break
        case 2:
            if (!ready(Resales.gateTownCount, Resales.gateTownMapped) || Resales.selectedId !== chosen) return
            for (const item of targetMap.mapItems) if (item.mapKey === Resales.gateRetainedSelectionKey) { retainedSelectedItem=item; break }
            if (!retainedSelectedItem) return
            if (!advance("selected-in-town")) return; Resales.setMaximumPrice(500000); break
        case 3:
            if (!ready(Resales.gateBudgetCount, Resales.gateBudgetMapped) || Resales.selectedId === "" || Resales.selectedId === chosen || Resales.selectedMapKey !== Resales.gateRetainedSelectionKey) return
            let retained=false
            for (const item of targetMap.mapItems) if (item.mapKey === Resales.gateRetainedSelectionKey && item === retainedSelectedItem && item.z === 1) retained=true
            if (!retained) return
            if (!advance("hidden-transaction-address-retained")) return;
            Resales.resetFilters(); targetMap.center=QtPositioning.coordinate(1.3521,103.8198); targetMap.zoomLevel=11; break
        case 4:
            if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            chosen=Resales.firstVisibleId
            if (!advance("reset")) return; Resales.selectTransaction(chosen); break
        case 5:
            if (chosen === "" || Resales.selectedId !== chosen) return
            if (!advance("selected-for-burst")) return;
            // Queued empty intentionally destroys all keys before restoring them.
            priorItems=({}); burstArmed=true; Resales.setTown(Resales.gateTown); break
        case 6:
            if (burstFired !== 1 || Resales.gateMaximumQueuedMutations < 4
                || !ready(Resales.gateAllCount, Resales.gateAllMapped) || Resales.selectedId !== "") return
            // Async incubation may cancel an intermediate queued creation. Every
            // completed live object must still balance against created/destroyed events.
            if (Resales.scaleLifecycle && targetMap.createdDelegates-priorCreated
                - (targetMap.destroyedDelegates-priorDestroyed) !== Resales.presentationCount-priorPresentationCount) return
            if (!advance("reentrant-burst-drained")) return; Resales.resetFilters(); break
        case 7:
            if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped) || Resales.selectedId !== "") return
            if (!advance("final-reset")) return; console.log("HDB_SCALE_PASS"); targetMap.traceDelegates=false; return true
        }
        return false
    }
    function extendedStep() {
        switch (phase) {
        case 0:
            if (!targetMap.mapReady || targetMap.error !== Map.NoError || !ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            if (!advance("loaded")) return; Resales.setMaximumPrice(Resales.maximumAvailablePrice); break
        case 1:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            if (!advance("full")) return; Resales.setTown(Resales.gateTown); break
        case 2:
            if (!ready(Resales.gateAllTownCount, Resales.gateAllTownMapped)) return
            if (!advance("subset")) return; Resales.setTown(Resales.gateOtherTown); break
        case 3:
            if (!ready(Resales.gateOtherTownCount, Resales.gateOtherTownMapped)) return
            if (!advance("different")) return; if (Resales.runtimeGateFault !== "skip-empty") Resales.setMaximumPrice(0); break
        case 4:
            if (!ready(0, 0) || Resales.selectedId !== "") return
            if (!advance("empty")) return; Resales.resetFilters(); break
        case 5:
            if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
            if (!advance("reset")) return; Resales.setMaximumPrice(Resales.maximumAvailablePrice); break
        case 6:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            chosen=Resales.gateTownSelectionId
            if (chosen === "") return
            if (!advance("full-restored")) return; Resales.selectTransaction(chosen); break
        case 7:
            if (Resales.selectedId !== chosen || !ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            let selected=null
            for (const item of targetMap.mapItems) if (item.transactionId === chosen) { selected=item; break }
            if (!selected || !selected.visible || selected.z !== 1) return
            if (!advance("selected")) return; Resales.setTown(Resales.gateOtherTown); break
        case 8:
            if (!ready(Resales.gateOtherTownCount, Resales.gateOtherTownMapped) || Resales.selectedId !== "" || Resales.selectedMapKey !== "") return
            if (!advance("hidden-selection-cleared")) return; Resales.setTown("All towns"); break
        case 9:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            if (!advance("full-again")) return; Resales.setTown(Resales.gateTown); break
        case 10:
            if (!ready(Resales.gateAllTownCount, Resales.gateAllTownMapped)) return
            if (!advance("repeat-subset")) return; Resales.setTown(Resales.gateOtherTown); break
        case 11:
            if (!ready(Resales.gateOtherTownCount, Resales.gateOtherTownMapped)) return
            if (!advance("repeat-different")) return; Resales.setTown("All towns"); break
        case 12:
            if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            if (!advance("repeat-full")) return; targetMap.zoomLevel=12; break
        case 13:
            if (Math.abs(targetMap.zoomLevel-12)>0.01 || !ready(Resales.gateAllCount,Resales.gateAllMapped)) return
            if (!advance("zoomed")) return; targetMap.pan(100,100); break
        case 14:
            if (Math.abs(targetMap.center.latitude-1.3521)<0.0001 || !ready(Resales.gateAllCount,Resales.gateAllMapped)) return
            if (!advance("panned")) return; targetMap.center=QtPositioning.coordinate(1.3521,103.8198); targetMap.zoomLevel=11; break
        case 15:
            if (Math.abs(targetMap.center.latitude-1.3521)>0.0001 || Math.abs(targetMap.zoomLevel-11)>0.01 || !ready(Resales.gateAllCount, Resales.gateAllMapped)) return
            if (!advance("recentered")) return; console.log("HDB_SCALE_PASS"); targetMap.traceDelegates=false; return true
        }
        return false
    }
    Timer {
        id: gateTimer
        interval: 25; repeat: true; running: true
        onTriggered: {
            if (Date.now()-started > ((Resales.scaleMeasurement || Resales.scaleExpandedCoverage) ? 10000 : 5000)) {
                failDeadline(Date.now()-started); return
            }
            if (Resales.scaleReentrant) { if (reentrantStep()) { stop(); Qt.quit() }; return }
            if (Resales.scaleTransitions) { if (extendedStep()) { stop(); Qt.quit() }; return }
            switch (phase) {
            case 0:
                if (!targetMap.mapReady || targetMap.error !== Map.NoError || targetMap.width <= 0 || targetMap.height <= 0 || !ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
                if (!advance("loaded")) return; Resales.setTown(Resales.gateTown); break
            case 1:
                if (!ready(Resales.gateTownCount, Resales.gateTownMapped)) return
                if (!advance("filtered")) return; Resales.setMaximumPrice(500000); break
            case 2:
                if (!ready(Resales.gateBudgetCount, Resales.gateBudgetMapped)) return
                if (!advance("budget-filtered")) return; chosen=Resales.firstVisibleId; Resales.selectTransaction(chosen); break
            case 3:
                if (chosen === "" || Resales.selectedId !== chosen || !ready(Resales.gateBudgetCount,Resales.gateBudgetMapped)) return
                if (!advance("selected")) return; if (Resales.runtimeGateFault !== "skip-empty") Resales.setMaximumPrice(0); break
            case 4:
                if (!ready(0,0) || Resales.selectedId !== "") return
                if (!advance("empty-cleared")) return; Resales.resetFilters(); break
            case 5:
                if (!ready(Resales.gateInitialCount, Resales.gateInitialMapped)) return
                if (!advance("reset")) return; targetMap.zoomLevel=12; break
            case 6:
                if (Math.abs(targetMap.zoomLevel-12)>0.01 || !ready(Resales.gateInitialCount,Resales.gateInitialMapped)) return
                if (!advance("zoomed")) return; targetMap.pan(100,100); break
            case 7:
                if (Math.abs(targetMap.center.latitude-1.3521)<0.0001 || !ready(Resales.gateInitialCount,Resales.gateInitialMapped)) return
                if (!advance("panned")) return; targetMap.center=QtPositioning.coordinate(1.3521,103.8198); targetMap.zoomLevel=11; break
            case 8:
                if (Math.abs(targetMap.center.latitude-1.3521)>0.0001 || Math.abs(targetMap.zoomLevel-11)>0.01 || !ready(Resales.gateInitialCount,Resales.gateInitialMapped)) return
                if (!advance("recentered")) return; Resales.setMaximumPrice(Resales.maximumAvailablePrice); break
            case 9:
                if (!ready(Resales.gateAllCount, Resales.gateAllMapped)) return
                if (!advance("all-prices")) return; Resales.measureScaleHeap(); console.log("HDB_SCALE_PASS"); targetMap.traceDelegates=false; stop(); Qt.quit(); break
            }
        }
    }
}
