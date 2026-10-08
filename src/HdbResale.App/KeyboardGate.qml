import QtQuick
import QtTest

// Opt-in keyboard regression check over the recorded API (tools/api_native_smoke.py --mode keyboard), adapted from
// the draft stack's desktop gate. QtTest key events are delivered inside the process, so it runs offscreen; it never
// loads in normal use. Expectations are the recorded fixture's addresses, spelled out.
Item {
    id: gate
    required property var targetWindow
    property int phase: 0
    property bool dispatching: false
    property double started: Date.now()
    property int frames: 0
    property var remembered: ({})
    property int selectionSignals: 0
    // A binding, like the inspector's repeater: it only sees what the model notifies.
    readonly property string shownInspector: Resales.selectedInspectorJson
    function shownFact(label) {
        for (const section of JSON.parse(shownInspector)) for (const fact of section.facts) if (fact.label === label) return fact.value
        return ""
    }
    TestEvent { id: events }
    Connections { target: gate.targetWindow; function onFrameSwapped() { gate.frames++ } }
    Connections { target: Resales; function onSelectedMapKeyChanged() { gate.selectionSignals++ } }
    readonly property var w: targetWindow
    readonly property var list: targetWindow.resultList
    readonly property var search: targetWindow.searchField
    function key(value, modifiers = Qt.NoModifier) { events.keyClick(value, modifiers, -1) }
    function type(text) { for (const c of text) events.keyClickChar(c, Qt.NoModifier, -1) }
    function keys() { const out = []; for (let i = 0; i < list.count; i++) out.push(list.itemAtIndex(i) ? list.itemAtIndex(i).addressKey : "?"); return out }
    function same(a, b) { return JSON.stringify(a) === JSON.stringify(b) }
    function advance(name) { console.log("HDB_KEYBOARD_STEP " + name); phase++; started = Date.now() }
    function fail(message) { console.error("HDB_KEYBOARD_GATE_FAIL " + message + " phase=" + phase); timer.stop(); Qt.quit() }
    Timer {
        id: timer; interval: 50; repeat: true; running: true
        onTriggered: {
            if (gate.dispatching) return          // QtTest delivery pumps events; never re-enter a phase
            gate.dispatching = true
            try { gate.step() } finally { gate.dispatching = false }
        }
    }
    function step() {
        if (Date.now() - started > 6000) { fail("deadline; search=" + JSON.stringify(Resales.searchText) + " count=" + list.count
            + " selected=" + Resales.selectedMapKey + " focus=" + w.activeFocusItem); return }
        switch (phase) {
        case 0:
            if (!w.active) w.requestActivate()
            if (!w.mapView.mapReady || !Resales.mapViewportReady || list.count !== 6 || !w.active) return
            key(Qt.Key_F, Qt.ControlModifier)          // ⌘F on macOS, Ctrl+F elsewhere
            advance("loaded"); break
        case 1:
            if (!search.activeFocus) return
            type("bedok res")
            advance("find-focuses-search"); break
        case 2: {
            const expected = ["bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres"]
            if (Resales.searchText !== "bedok res" || list.count !== 3) return
            if (!same(keys(), expected)) { fail("search order " + keys()); return }
            if (Resales.mappedCount !== 3) { fail("map shows " + Resales.mappedCount + " of the 3 listed addresses"); return }
            key(Qt.Key_Down); key(Qt.Key_Down)
            selectionSignals = 0
            key(Qt.Key_Return)
            advance("search-filters-list-and-map"); break
        }
        case 3: {
            if (Resales.selectedMapKey !== "bedok-748b-bedok-reservoir-cres" || !Resales.detailReady) return
            const row = list.itemAtIndex(1)
            if (!w.detailsView.visible || !row.highlighted || !row.Accessible.selected || !search.activeFocus) {
                fail("selection not shown: details=" + w.detailsView.visible + " highlighted=" + row.highlighted); return
            }
            // Loading the details must not signal the selection again (it would re-announce it and move the list).
            if (selectionSignals !== 1) { fail("one selection signalled " + selectionSignals + " times"); return }
            // The details' middle half of sales (recorded priceIqr 705750–880000) reaches the inspector, whose four
            // sections are laid out as fact rows.
            if (shownFact("Middle half, all types") !== "S$705,750–S$880,000") {
                fail("details missing from the shown inspector: " + shownInspector); return
            }
            if (w.inspectorView.count !== 4 || !w.inspectorView.itemAt(0) || w.inspectorView.itemAt(0).factCount !== 6) {
                fail("inspector sections " + w.inspectorView.count + " facts " + (w.inspectorView.itemAt(0) ? w.inspectorView.itemAt(0).factCount : "none")); return
            }
            selectionSignals = 0
            type("e")                                  // "bedok rese" still matches the selected 748B
            advance("keyboard-select"); break
        }
        case 4:
            if (Resales.searchText !== "bedok rese" || list.count !== 3) return
            // Typing a refinement keeps the selection, so it is neither signalled nor announced again.
            if (Resales.selectedMapKey !== "bedok-748b-bedok-reservoir-cres" || selectionSignals !== 0) {
                fail("refining the search re-signalled the selection " + selectionSignals + " times"); return
            }
            type(" 747")                               // the selected 748B no longer matches
            advance("refinement-keeps-selection"); break
        case 5:
            if (Resales.searchText !== "bedok rese 747" || list.count !== 1) return
            if (Resales.selectedMapKey !== "" || w.detailsView.visible || Resales.selectionMapStatus !== "") {
                fail("hidden selection kept: " + Resales.selectedMapKey); return
            }
            key(Qt.Key_Escape)
            advance("hidden-selection-cleared"); break
        case 6:
            if (search.text !== "" || Resales.searchText !== "" || list.count !== 6) return
            if (Resales.selectedMapKey !== "") { fail("clearing the search restored a selection"); return }
            type("4717")
            advance("escape-clears"); break
        case 7:
            if (Resales.searchText !== "4717" || list.count !== 2) return
            if (!same(keys(), ["bedok-748a-bedok-reservoir-cres", "bedok-747a-bedok-reservoir-cres"])) { fail("postal " + keys()); return }
            search.selectAll(); type("588")
            advance("postal-code"); break
        case 8:
            // 588B and 588C exist but lie above the default S$1,000,000 maximum.
            if (Resales.searchText !== "588" || list.count !== 0) return
            if (!w.emptyResultsView.visible || Resales.searchMatchesOutsideFilters !== 2) {
                fail("empty state: visible=" + w.emptyResultsView.visible + " hidden=" + Resales.searchMatchesOutsideFilters); return
            }
            w.clearSearch()
            advance("filtered-out-matches-explained"); break
        case 9:
            if (Resales.searchText !== "" || list.count !== 6) return
            list.forceActiveFocus(Qt.TabFocusReason)
            type("be")                                  // typing in the list continues in the search field
            advance("clear-search"); break
        case 10:
            if (!search.activeFocus || search.text !== "be" || Resales.searchText !== "be") return
            key(Qt.Key_Escape)
            remembered.focus = w.activeFocusItem
            w.showAbout()
            advance("type-to-search"); break
        case 11:
            if (!w.modalOpen) return
            key(Qt.Key_F, Qt.ControlModifier)
            if (search.activeFocus) { fail("find acted behind the About dialog"); return }
            key(Qt.Key_Escape)
            advance("modal-isolated"); break
        case 12:
            if (w.modalOpen) return
            if (w.activeFocusItem !== remembered.focus) { fail("focus not restored: " + w.activeFocusItem); return }
            remembered.row = list.itemAtIndex(0); remembered.height = remembered.row.height; remembered.frames = frames
            remembered.row.displayAddress = "A long address that must stay readable in a narrow list. ".repeat(4)
            advance("focus-restored"); break
        case 13: {
            const row = remembered.row
            if (row.height <= remembered.height) {
                if (frames === remembered.frames && Date.now() - started > 2500) { fail("no frames presented; layout unverifiable"); return }
                if (Date.now() - started < 2500) return
            }
            if (row.height <= remembered.height || row.addressLabel.truncated) { fail("long address clipped"); return }
            row.displayAddress = Qt.binding(() => row.address)
            w.width = 640; w.height = 600
            advance("long-address-wraps"); break
        }
        case 14:
            if (!w.compact || w.mapView.height < 250) return
            key(Qt.Key_F, Qt.ControlModifier)
            advance("compact-layout"); break
        case 15:
            if (w.viewSwitch.currentIndex !== 1 || !search.activeFocus) return
            key(Qt.Key_Down); key(Qt.Key_Return)
            advance("compact-find"); break
        case 16:
            if (Resales.selectedMapKey !== "ang-mo-kio-727-ang-mo-kio-ave-6") return
            // The compact details replaced the list that held focus; focus must move into them, not vanish.
            if (!w.activeFocusItem || w.activeFocusItem.text !== "‹ All addresses") {
                if (Date.now() - started < 1000) return
                fail("compact selection left focus on " + w.activeFocusItem); return
            }
            remembered.key = Resales.selectedMapKey
            w.width = 1360; w.height = 900
            advance("compact-select"); break
        case 17:
            if (w.compact) return
            if (Resales.selectedMapKey !== remembered.key) { fail("resize lost the selection"); return }
            // Pooled or not, every visible row reflects its own address and the current selection.
            for (let i = 0; i < list.count; i++) {
                const row = list.itemAtIndex(i)
                if (!row) continue
                if (row.displayAddress !== row.address || row.highlighted !== (row.addressKey === Resales.selectedMapKey)
                        || row.Accessible.name.indexOf(row.address) !== 0) { fail("stale row " + i + " " + row.addressKey); return }
            }
            advance("resize-keeps-selection"); break
        case 18: {
            // Tab visits the window's regions in visual order, stops only on visible controls and cycles without a trap;
            // Shift+Tab steps back. Offscreen, Tab reaches every control; macOS by default stops only at text fields and
            // lists, in the same order.
            const regions = [["filters", w.filtersView], ["map", w.mapView.parent], ["search", search], ["list", list], ["details", w.detailsView]]
            const owner = (item) => { for (let p = item; p; p = p.parent) for (const [name, region] of regions) if (p === region) return name; return "" }
            w.filtersView.townPicker.forceActiveFocus(Qt.TabFocusReason)
            const seen = [owner(w.activeFocusItem)]
            for (let i = 0; i < 40; i++) {
                key(Qt.Key_Tab)
                const item = w.activeFocusItem
                if (!item || !item.visible || item.width <= 0 || item.height <= 0) { fail("Tab reached an invisible item: " + item); return }
                const name = owner(item)
                if (name !== "" && name !== seen[seen.length - 1]) seen.push(name)
                if (name === "filters" && seen.length > 2) break
            }
            const order = seen.join(">")
            console.log("HDB_KEYBOARD_TABS " + order)
            let at = -1
            for (const stop of ["filters", "map", "search", "list", "details", "filters"]) {
                const next = seen.indexOf(stop, at + 1)
                if (next < 0) { fail("tab order missing or out of order at " + stop + ": " + order); return }
                at = next
            }
            list.forceActiveFocus(Qt.TabFocusReason)
            key(Qt.Key_Backtab)
            if (owner(w.activeFocusItem) !== "search") { fail("Shift+Tab from the list did not return to search: " + w.activeFocusItem); return }
            console.log("HDB_API_KEYBOARD_PASS steps=" + phase)
            timer.stop(); w.finishGate()
            break
        }
        }
    }
}
