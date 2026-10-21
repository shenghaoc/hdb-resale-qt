import QtQuick
import QtTest
import HdbResale.Acceptance

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
    // What Qt tells a screen reader, in order and with its timing, since the last clearEvents(). How a screen reader then
    // speaks it is checked natively with VoiceOver's captions; this pins what Qt raises and when.
    function selectedAnnouncements() {
        return AccessibilityGate.events().filter(e => e.type === "Announcement" && e.message.indexOf("Selected ") === 0)
    }
    // Why a selection reached a screen reader other than intended, or "". `listFocused`: the list held keyboard focus when
    // it was made. On macOS the focused row says a selection itself (its name starts "Selected"), so nothing is announced.
    // Otherwise (the search field keeps focus, a compact window's list hides, other platforms) exactly one announcement
    // carries it, raised well after the last focus event, which is when a screen reader would drop it.
    function selectionFeedback(row, heading, listFocused) {
        const announced = selectedAnnouncements()
        if (w.macDesktop && !w.compact && listFocused) {
            if (announced.length !== 0) return "announced " + JSON.stringify(announced.map(e => e.message)) + " although the focused row says it"
            if (w.activeFocusItem !== row) return "the selected row does not hold focus: " + w.activeFocusItem
            if (!row.Accessible.selected || row.Accessible.name.indexOf("Selected ") !== 0)
                return "the selected row does not say so: " + row.Accessible.name + ", selected " + row.Accessible.selected
            return ""
        }
        if (announced.length !== 1 || announced[0].message !== "Selected " + heading)
            return "expected one 'Selected " + heading + "' announcement, got " + JSON.stringify(announced.map(e => e.message))
        const focused = AccessibilityGate.events().filter(e => e.type === "Focus")
        const sinceFocus = announced[0].ms - (focused.length > 0 ? focused[focused.length - 1].ms : 0)
        if (announced[0].ms < 300 || sinceFocus < 300)
            return "announced " + announced[0].ms.toFixed(0) + " ms after the action and " + sinceFocus.toFixed(0) + " ms after the last focus event"
        return ""
    }
    // macOS rows with button semantics say a selection themselves while the list has focus, so no announcement follows it.
    function selectionSpokenByRow() { return w.macDesktop && !w.compact }
    // Another, unrelated window takes activation, so this one becomes inactive; reactivate() gives it back.
    function deactivate() {
        remembered.helper = Qt.createQmlObject('import QtQuick; Window { transientParent: null; width: 160; height: 90; visible: true; title: "Keyboard gate: inactive-window check" }', gate)
        remembered.helper.requestActivate()
    }
    function reactivate() { remembered.helper.destroy(); remembered.helper = null; w.requestActivate() }
    // Shown as the list's cursor ring is drawn: a 2-point ring in the highlight colour with the theme's small radius.
    function ringStyled(ring) {
        return ring.border.width === 2 && Qt.colorEqual(ring.border.color, w.palette.highlight) && ring.radius === w.theme.xs
    }
    // Whether an item inside the details is shown whole in their view (or from its top, when taller than the view).
    function fullyShown(item) {
        const flick = w.detailsScrollView.contentItem
        const top = item.mapToItem(flick.contentItem, 0, 0).y
        if (item.height > flick.height) return Math.abs(top - flick.contentY) < 1
        return top >= flick.contentY - 0.5 && top + item.height <= flick.contentY + flick.height + 0.5
    }
    function insideDetails(item) { for (let p = item; p; p = p.parent) if (p === w.detailsView) return true; return false }
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
            AccessibilityGate.record()
            // Which branches run: macOS's, or (HDB_API_GATE=keyboard-other-desktop) the other desktops' on any platform.
            console.log("HDB_KEYBOARD_DESKTOP " + (w.macDesktop ? "macOS" : "other") + " tab-focus=" + Qt.styleHints.tabFocusBehavior)
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
            AccessibilityGate.clearEvents()
            key(Qt.Key_Return)
            advance("search-filters-list-and-map"); break
        }
        case 3: {
            // Hiding the details while they hold focus moves it to the list, not nowhere; then 748B is selected again.
            if (remembered.detailsStep === 1) {
                if (!list.activeFocus) {
                    if (Date.now() - remembered.hidAt > 1500) fail("hiding the details left focus on " + w.activeFocusItem)
                    return
                }
                // The list holds focus here. Where the focused row does not say the selection itself (everything but a
                // macOS window wide enough for button rows) the re-selection raises its own announcement 350 ms later.
                if (!selectionSpokenByRow()) AccessibilityGate.clearEvents()
                remembered.reselectedAt = Date.now()
                Resales.selectAddress("bedok-748b-bedok-reservoir-cres")
                remembered.detailsStep = 2
                return
            }
            if (Resales.selectedMapKey !== "bedok-748b-bedok-reservoir-cres" || !Resales.detailReady) return
            if (remembered.detailsStep === 2) {
                // Clearing the events before that announcement is raised would leave it to land in the refinement phase.
                if (!selectionSpokenByRow()) {
                    const raised = selectedAnnouncements().length
                    if (raised > 1) { fail("the re-selection was announced " + raised + " times"); return }
                    if (Date.now() - remembered.reselectedAt < 800) return     // long enough for a second one to show
                    if (raised !== 1) { fail("the re-selection was never announced"); return }
                }
                search.forceActiveFocus(Qt.OtherFocusReason)
                selectionSignals = 0
                AccessibilityGate.clearEvents()
                type("e")                                  // "bedok rese" still matches the selected 748B
                advance("keyboard-select"); break
            }
            const row = list.itemAtIndex(1)
            if (!w.detailsView.visible || !row.highlighted || !row.Accessible.selected || !search.activeFocus) {
                fail("selection not shown: details=" + w.detailsView.visible + " highlighted=" + row.highlighted); return
            }
            // Loading the details must not signal the selection again (it would re-announce it and move the list).
            if (selectionSignals !== 1) { fail("one selection signalled " + selectionSignals + " times"); return }
            // The search field keeps focus, so the one announcement carries the selection.
            if (Date.now() - started < 800) return
            {
                const why = selectionFeedback(row, "748B BEDOK RESERVOIR CRES", false)
                if (why !== "") { fail("Return from the search field: " + why); return }
            }
            // The details' middle half of sales (recorded priceIqr 705750–880000) reaches the inspector, whose four
            // sections are laid out as fact rows.
            if (shownFact("Middle half, all types") !== "S$705,750–S$880,000") {
                fail("details missing from the shown inspector: " + shownInspector); return
            }
            if (w.inspectorView.count !== 4 || !w.inspectorView.itemAt(0) || w.inspectorView.itemAt(0).factCount !== 6) {
                fail("inspector sections " + w.inspectorView.count + " facts " + (w.inspectorView.itemAt(0) ? w.inspectorView.itemAt(0).factCount : "none")); return
            }
            // A click gives the details keyboard focus; End, Home and Page Down then scroll them.
            const details = w.detailsScrollView, flick = details.contentItem
            events.mouseClick(details, details.width / 2, 2 * w.unit, Qt.LeftButton, Qt.NoModifier, -1)
            if (!details.activeFocus) { fail("a click in the details left focus on " + w.activeFocusItem); return }
            const bottom = flick.originY + flick.contentHeight - flick.height
            key(Qt.Key_End)
            if (bottom <= flick.originY || Math.abs(flick.contentY - bottom) > 1) {
                fail("End left the details at " + flick.contentY + " of " + bottom); return
            }
            key(Qt.Key_Home)
            if (Math.abs(flick.contentY - flick.originY) > 1) { fail("Home left the details at " + flick.contentY); return }
            key(Qt.Key_PageDown)
            if (flick.contentY - flick.originY < flick.height / 2) { fail("Page Down moved the details only to " + flick.contentY); return }
            key(Qt.Key_Home)
            Resales.selectAddress("")
            remembered.detailsStep = 1
            remembered.hidAt = Date.now()
            break
        }
        case 4:
            if (Resales.searchText !== "bedok rese" || list.count !== 3) return
            // Typing a refinement keeps the selection, so it is neither signalled nor announced again.
            if (Resales.selectedMapKey !== "bedok-748b-bedok-reservoir-cres" || selectionSignals !== 0) {
                fail("refining the search re-signalled the selection " + selectionSignals + " times"); return
            }
            // Long enough for a late announcement, and for the result count that follows typing.
            if (Date.now() - started < 1200) return
            if (selectedAnnouncements().length !== 0) {
                fail("refining the search announced the selection again: " + JSON.stringify(selectedAnnouncements().map(e => e.message))); return
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
            // End and Home reach the last and first rows, Page Down and Page Up move a page; only the cursor moves.
            key(Qt.Key_End)
            if (list.currentIndex !== 5) { fail("End left the cursor at " + list.currentIndex); return }
            key(Qt.Key_Home)
            if (list.currentIndex !== 0) { fail("Home left the cursor at " + list.currentIndex); return }
            key(Qt.Key_PageDown)
            if (list.currentIndex <= 0) { fail("Page Down left the cursor at " + list.currentIndex); return }
            key(Qt.Key_PageUp)
            if (list.currentIndex !== 0 || Resales.selectedMapKey !== "") {
                fail("Page Up left the cursor at " + list.currentIndex + ", selected " + Resales.selectedMapKey); return
            }
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
            key(Qt.Key_Down)
            AccessibilityGate.clearEvents()
            key(Qt.Key_Return)
            advance("compact-find"); break
        case 16:
            if (Resales.selectedMapKey !== "ang-mo-kio-727-ang-mo-kio-ave-6") return
            // The compact details replaced the list that held focus; focus must move into them, not vanish.
            if (!w.activeFocusItem || w.activeFocusItem.text !== "‹ All addresses") {
                if (Date.now() - started < 1000) return
                fail("compact selection left focus on " + w.activeFocusItem); return
            }
            // The list is hidden and focus moves to Back, so the announcement carries it, after that focus change.
            if (Date.now() - started < 1000) return
            {
                const why = selectionFeedback(list.itemAtIndex(0), "727 ANG MO KIO AVE 6", true)
                if (why !== "") { fail("compact selection: " + why); return }
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
                const prefix = w.macDesktop && row.highlighted ? "Selected " : ""
                if (row.displayAddress !== row.address || row.highlighted !== (row.addressKey === Resales.selectedMapKey)
                        || row.Accessible.name.indexOf(prefix + row.address) !== 0) { fail("stale row " + i + " " + row.addressKey); return }
                // macOS: a button, selected while the selection, with nothing spoken before its name (VoiceOver speaks a
                // row's description first, and the location is in the details), that an assistive tool cannot focus (the list
                // holds focus, and Return acts on its cursor row).
                if (w.macDesktop && (row.Accessible.role !== Accessible.Button || row.Accessible.focusable
                        || row.Accessible.selected !== row.highlighted || row.Accessible.description !== "")) {
                    fail("row " + i + " is not a button with its name alone: role " + row.Accessible.role + ", focusable "
                        + row.Accessible.focusable + ", selected " + row.Accessible.selected + " for highlighted " + row.highlighted
                        + ", description '" + row.Accessible.description + "'"); return
                }
            }
            advance("resize-keeps-selection"); break
        case 18: {
            // Tab visits the window's regions in visual order, stops only on visible controls and cycles without a trap;
            // Shift+Tab steps back. Where Tab reaches every control (offscreen, and macOS with keyboard navigation on) it
            // stops in every region. macOS by default stops only at text fields and lists, so the map and the details
            // drop out, but the price fields, the search field and the result list keep their order.
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
            const everyControl = Qt.styleHints.tabFocusBehavior === Qt.TabFocusAllControls
            console.log("HDB_KEYBOARD_TABS " + order + (everyControl ? "" : " (text fields and lists only)"))
            let at = -1
            for (const stop of everyControl ? ["filters", "map", "search", "list", "details", "filters"] : ["filters", "search", "list", "filters"]) {
                const next = seen.indexOf(stop, at + 1)
                if (next < 0) { fail("tab order missing or out of order at " + stop + ": " + order); return }
                at = next
            }
            list.forceActiveFocus(Qt.TabFocusReason)
            key(Qt.Key_Backtab)
            if (owner(w.activeFocusItem) !== "search") { fail("Shift+Tab from the list did not return to search: " + w.activeFocusItem); return }
            // Return in the list selects the row under the cursor; on macOS that row, which holds focus, says so itself.
            Resales.selectAddress("")
            list.forceActiveFocus(Qt.TabFocusReason)
            key(Qt.Key_Down); key(Qt.Key_Down); key(Qt.Key_Down)
            if (list.currentIndex !== 2) { fail("three Down presses left the cursor at " + list.currentIndex); return }
            selectionSignals = 0
            AccessibilityGate.clearEvents()
            key(Qt.Key_Return)
            advance("list-return-dispatched")
            break
        }
        case 19: {
            if (Resales.selectedMapKey !== "bedok-115-bedok-nth-rd" || !Resales.detailReady) return
            if (selectionSignals !== 1) { fail("Return in the list signalled the selection " + selectionSignals + " times"); return }
            if (Date.now() - started < 800) return
            const why = selectionFeedback(list.itemAtIndex(2), "115 BEDOK NTH RD", true)
            if (why !== "") { fail("Return in the list: " + why); return }
            // A screen reader presses a row while focus is elsewhere. Clear the selection so the native Press action must
            // cause a real change.
            Resales.selectAddress("")
            search.forceActiveFocus(Qt.OtherFocusReason)
            remembered.pressRow = list.itemAtIndex(1)
            selectionSignals = 0
            AccessibilityGate.clearEvents()
            if (!AccessibilityGate.press(remembered.pressRow)) { fail("row exposes no accessible Press action"); return }
            advance("accessible-press-dispatched")
            break
        }
        case 20:
            if (Resales.selectedMapKey !== remembered.pressRow.addressKey || !Resales.detailReady) return
            if (!w.detailsView.visible || !remembered.pressRow.highlighted || !remembered.pressRow.Accessible.selected
                    || list.currentIndex !== 1 || selectionSignals !== 1) {
                fail("accessible Press did not select exactly once in list, map model and details"); return
            }
            if (Date.now() - started < 800) return
            {
                const why = selectionFeedback(remembered.pressRow, "39 BEDOK STH RD", true)
                if (why !== "") { fail("accessible Press: " + why); return }
            }
            w.width = 640; w.height = 600
            advance("accessible-press-selects"); break
        case 21:
            if (!w.compact) return
            key(Qt.Key_1, Qt.ControlModifier)
            advance("compact-map-shortcut"); break
        case 22:
            if (w.viewSwitch.currentIndex !== 0 || !w.mapView.activeFocus) return
            key(Qt.Key_2, Qt.ControlModifier)
            advance("compact-map-has-focus"); break
        case 23:
            if (w.viewSwitch.currentIndex !== 1 || !w.backButton.activeFocus) return
            // The view switch takes focus by Tab only on every desktop, so a click does not focus it; switching must
            // then repair focus when it hides its previous owner (Back, then the map).
            events.mouseClick(w.mapToggle, w.mapToggle.width / 2, w.mapToggle.height / 2, Qt.LeftButton, Qt.NoModifier, -1)
            advance("compact-addresses-shortcut-focus"); break
        case 24:
            if (w.viewSwitch.currentIndex !== 0) return
            if (!w.activeFocusItem || !w.activeFocusItem.visible || w.activeFocusItem === w.contentItem) {
                if (Date.now() - started < 1000) return
                fail("Map click left focus hidden or lost: " + w.activeFocusItem); return
            }
            if (!w.mapView.activeFocus) { fail("Map click did not repair hidden Back focus: " + w.activeFocusItem); return }
            console.log("HDB_KEYBOARD_CLICK_MAP_FOCUS " + w.activeFocusItem)
            w.mapView.forceActiveFocus(Qt.TabFocusReason)
            events.mouseClick(w.addressesToggle, w.addressesToggle.width / 2, w.addressesToggle.height / 2, Qt.LeftButton, Qt.NoModifier, -1)
            advance("compact-map-click-focus-visible"); break
        case 25:
            if (w.viewSwitch.currentIndex !== 1) return
            if (!w.activeFocusItem || !w.activeFocusItem.visible || w.activeFocusItem === w.contentItem) {
                if (Date.now() - started < 1000) return
                fail("Addresses click left focus hidden or lost: " + w.activeFocusItem); return
            }
            if (!w.backButton.activeFocus) { fail("Addresses click did not repair hidden map focus: " + w.activeFocusItem); return }
            console.log("HDB_KEYBOARD_CLICK_ADDRESSES_FOCUS " + w.activeFocusItem)
            // A shared visible filter field retains focus across pointer switching.
            w.filtersView.expanded = true
            advance("compact-addresses-click-focus-visible"); break
        case 26:
            if (!w.filtersView.townPicker.visible) return
            w.filtersView.townPicker.forceActiveFocus(Qt.OtherFocusReason)
            remembered.focus = w.activeFocusItem
            events.mouseClick(w.mapToggle, w.mapToggle.width / 2, w.mapToggle.height / 2, Qt.LeftButton, Qt.NoModifier, -1)
            advance("compact-click-from-visible-filter"); break
        case 27:
            if (w.viewSwitch.currentIndex !== 0) return
            if (w.activeFocusItem !== remembered.focus) {
                fail("Map click stole focus from a visible shared control: " + w.activeFocusItem); return
            }
            advance("compact-click-keeps-visible-focus"); break
        case 28:
            // Other desktops: clicking the status bar's About button leaves focus on the list, so closing About gives it
            // back there and a further Escape clears the search. On macOS About is in the menu bar.
            if (w.macDesktop) { advance("about-in-menu-bar"); break }
            if (!remembered.aboutStep) {
                w.showView(1)                        // compact: the addresses, nothing selected, so the list
                Resales.selectAddress("")
                search.forceActiveFocus(Qt.OtherFocusReason); search.selectAll(); type("748")
                remembered.aboutStep = 1
                return
            }
            if (Resales.searchText !== "748" || list.count !== 2 || !w.aboutButton.visible) return
            list.forceActiveFocus(Qt.TabFocusReason)
            remembered.focus = w.activeFocusItem
            events.mouseClick(w.aboutButton, w.aboutButton.width / 2, w.aboutButton.height / 2, Qt.LeftButton, Qt.NoModifier, -1)
            advance("about-button-clicked"); break
        case 29:
            if (w.macDesktop) { advance("about-in-menu-bar"); break }
            if (!w.modalOpen) return
            key(Qt.Key_Escape)
            advance("about-closed-by-escape"); break
        case 30:
            if (!w.macDesktop) {
                if (w.modalOpen) return
                if (w.activeFocusItem !== remembered.focus) { fail("closing About left focus on " + w.activeFocusItem + ", not the list"); return }
                key(Qt.Key_Escape)
                if (search.text !== "" || Resales.searchText !== "") { fail("Escape after About left the search at " + JSON.stringify(Resales.searchText)); return }
            }
            // Every tool-style button takes focus by Tab only. Qt Quick already gives buttons Qt::TabFocus on macOS, so
            // there this holds by default; elsewhere its default is Qt::StrongFocus and the policy must be set.
            for (const button of w.toolStyleButtons) if (button.focusPolicy !== Qt.TabFocus) {
                fail("tool-style button '" + button.text + "' takes focus by click: policy " + button.focusPolicy); return
            }
            Resales.selectAddress("")
            key(Qt.Key_1, Qt.ControlModifier)                // the map, focused
            advance("tool-buttons-tab-focus"); break
        case 31: {
            // Focus on the map is visible while the window is active, and hidden while it is not.
            if (w.viewSwitch.currentIndex !== 0 || !w.mapView.activeFocus || !w.active) return
            const ring = w.mapFocusRing
            if (!ring || !ring.visible) { fail("the focused map shows no focus ring"); return }
            if (!ringStyled(ring)) { fail("the map's focus ring is not drawn like the cursor ring"); return }
            deactivate()
            advance("map-focus-ring-shown"); break
        }
        case 32:
            if (w.active) return
            if (w.mapFocusRing.visible) { fail("the map's focus ring shows in an inactive window"); return }
            reactivate()
            advance("map-focus-ring-hidden-when-inactive"); break
        case 33:
            if (!w.active) return
            if (!w.mapFocusRing.visible) { fail("the map's focus ring did not return with the window's activation"); return }
            list.currentIndex = -1                           // as when nothing is selected and the cursor has not moved
            key(Qt.Key_2, Qt.ControlModifier)                // the addresses: the list takes focus with no cursor row
            advance("map-focus-ring-back"); break
        case 34: {
            if (w.viewSwitch.currentIndex !== 1 || !list.activeFocus) return
            if (w.mapFocusRing.visible) { fail("the map's focus ring stayed after focus left the map"); return }
            if (list.currentItem !== null) { fail("the list took focus with a cursor row at " + list.currentIndex); return }
            const ring = w.listFocusRing
            if (!ring || !ring.visible) { fail("the focused list without a cursor row shows no focus ring"); return }
            if (!ringStyled(ring)) { fail("the list's focus ring is not drawn like the cursor ring"); return }
            // Once the cursor row is drawn, it shows the focus instead.
            key(Qt.Key_Down)
            if (ring.visible || !list.highlightItem || !list.highlightItem.visible) {
                fail("with a cursor row: frame ring " + ring.visible + ", cursor ring " + (list.highlightItem && list.highlightItem.visible)); return
            }
            list.currentIndex = -1                           // no cursor row again, for the inactive-window check
            if (!ring.visible) { fail("the list's frame ring did not return without a cursor row"); return }
            deactivate()
            advance("list-focus-ring-shown"); break
        }
        case 35:
            if (w.active) return
            if (w.listFocusRing.visible) { fail("the list's focus ring shows in an inactive window"); return }
            reactivate()
            advance("list-focus-ring-hidden-when-inactive"); break
        case 36:
            if (!w.active) return
            if (!w.listFocusRing.visible) { fail("the list's focus ring did not return with the window's activation"); return }
            key(Qt.Key_L, Qt.ControlModifier)                // compact: the filters expand and the town picker takes focus
            advance("list-focus-ring-back"); break
        case 37: {
            // Collapsing the filters while the town picker holds focus moves focus to the Filters button, so an arrow
            // key afterwards cannot change the hidden town filter.
            const filters = w.filtersView
            if (!filters.expanded || !filters.townPicker.activeFocus) return
            remembered.town = Resales.townIndex
            events.mouseClick(filters.filtersButton, filters.filtersButton.width / 2, filters.filtersButton.height / 2, Qt.LeftButton, Qt.NoModifier, -1)
            if (filters.expanded || filters.townPicker.visible) { fail("clicking Filters did not collapse them"); return }
            key(Qt.Key_Down)
            if (Resales.townIndex !== remembered.town) {
                fail("Down after collapsing the filters changed the hidden town filter to index " + Resales.townIndex); return
            }
            if (!filters.filtersButton.activeFocus) { fail("collapsing the filters left focus on " + w.activeFocusItem + ", not the Filters button"); return }
            advance("filters-collapse-moves-focus"); break
        }
        case 38: {
            // On Cocoa the menus are in the native menu bar, so the window's accessibility tree holds no menu bar of its own
            // (the in-window MenuBar's unseen items would be a second, dead one). Offscreen, Qt draws the MenuBar in the
            // window, so it stays exposed; other desktops have no in-window menu bar.
            const menuRoles = [Accessible.MenuBar, Accessible.MenuItem]
            const found = AccessibilityGate.elements(w).filter(e => menuRoles.indexOf(e.role) >= 0).map(e => e.name)
            console.log("HDB_KEYBOARD_MENU_ELEMENTS " + Qt.platform.pluginName + " " + JSON.stringify(found))
            if (w.macDesktop && Qt.platform.pluginName === "cocoa" && found.length !== 0) {
                fail("the window's accessibility tree holds the native menu bar's in-window duplicate: " + JSON.stringify(found)); return
            }
            if (w.macDesktop && Qt.platform.pluginName !== "cocoa" && found.length === 0) {
                fail("the menu bar drawn in the window is missing from the accessibility tree"); return
            }
            if (!w.macDesktop && found.length !== 0) { fail("an in-window menu bar on a desktop without one: " + JSON.stringify(found)); return }
            w.width = 1360; w.height = 900
            Resales.selectAddress("bedok-748b-bedok-reservoir-cres")
            advance("no-in-window-menu-bar-duplicate"); break
        }
        case 39: {
            // The details are reachable piece by piece: each fact group, the chart and the registrations list is a named
            // keyboard stop, scrolled whole into view when it takes focus, and the chart has a text alternative.
            const regs = w.registrationList
            if (w.compact || Resales.selectedMapKey !== "bedok-748b-bedok-reservoir-cres" || !Resales.detailReady) return
            if (!regs || regs.Accessible.role !== Accessible.List) { fail("the registrations are not an accessible list"); return }
            if (regs.count !== 20 || !w.trendView.item) return
            const groups = []
            for (let i = 0; i < w.inspectorView.count; i++) groups.push(w.inspectorView.itemAt(i))
            const chart = w.trendView.item
            for (const stop of [...groups, chart]) {
                if (stop.Accessible.role !== Accessible.Grouping || !stop.activeFocusOnTab || !stop.Accessible.focusable) {
                    fail("'" + stop.Accessible.name + "' is not a focusable, named group: role " + stop.Accessible.role + ", tab " + stop.activeFocusOnTab); return
                }
            }
            if (chart.Accessible.name !== "Monthly median price") { fail("the chart's group is named '" + chart.Accessible.name + "'"); return }
            const expected = "Highest S$1,060,000 in 2026-05, lowest S$653,000 in 2026-06, 10 months with sales of the 24 shown."
            if (!chart.summary || chart.summary.text !== expected || chart.summary.Accessible.role !== Accessible.StaticText
                    || chart.summary.Accessible.name !== expected) {
                fail("the chart's text alternative reads '" + (chart.summary ? chart.summary.text : "") + "'"); return
            }
            const first = regs.itemAtIndex(0)
            if (!first || first.Accessible.role !== Accessible.ListItem || first.Accessible.name.indexOf("2026-09 · 3 ROOM · S$663,000. ") !== 0) {
                fail("the first registration reads '" + (first ? first.Accessible.name : "") + "'"); return
            }
            // Tab from "Show on map", where Tab reaches every control (VoiceOver's full keyboard access, KDE).
            remembered.tabFocus = Qt.styleHints.tabFocusBehavior
            AccessibilityGate.setTabFocusBehavior(Qt.TabFocusAllControls)
            w.showOnMapButton.forceActiveFocus(Qt.TabFocusReason)
            const names = []
            // A list is a focus scope: holding focus, its current row is the focused item.
            const holds = (stop) => stop === regs ? regs.activeFocus && w.activeFocusItem === regs.currentItem : w.activeFocusItem === stop
            for (const stop of [...groups, chart, regs]) {
                key(Qt.Key_Tab)
                if (!holds(stop)) {
                    AccessibilityGate.setTabFocusBehavior(remembered.tabFocus)
                    fail("Tab after " + JSON.stringify(names) + " reached " + w.activeFocusItem + ", not '" + stop.Accessible.name + "'"); return
                }
                const shown = stop === regs ? regs.currentItem : stop
                if (!shown || !fullyShown(shown)) {
                    AccessibilityGate.setTabFocusBehavior(remembered.tabFocus)
                    fail("'" + stop.Accessible.name + "' took focus out of view"); return
                }
                names.push(stop.Accessible.name)
            }
            if (regs.currentIndex !== 0) { AccessibilityGate.setTabFocusBehavior(remembered.tabFocus); fail("the registrations took focus at row " + regs.currentIndex); return }
            key(Qt.Key_Tab)
            const after = w.activeFocusItem
            key(Qt.Key_Backtab)
            const back = w.activeFocusItem
            AccessibilityGate.setTabFocusBehavior(remembered.tabFocus)
            if (!after || insideDetails(after)) { fail("Tab after the registrations stayed in the details: " + after); return }
            if (!holds(regs)) { fail("Shift+Tab from " + after + " reached " + back + ", not the registrations"); return }
            console.log("HDB_KEYBOARD_DETAILS_STOPS " + JSON.stringify(names) + " then " + after)
            // The arrows move the cursor through all 20 registrations, each kept in view; Home and End still scroll.
            for (let i = 1; i < 20; i++) {
                key(Qt.Key_Down)
                if (regs.currentIndex !== i || !fullyShown(regs.currentItem)) {
                    fail("Down to registration " + (i + 1) + ": cursor " + regs.currentIndex + ", shown " + fullyShown(regs.currentItem)); return
                }
            }
            key(Qt.Key_Down)
            if (regs.currentIndex !== 19) { fail("Down past the last registration moved the cursor to " + regs.currentIndex); return }
            key(Qt.Key_Up)
            if (regs.currentIndex !== 18 || !fullyShown(regs.currentItem)) { fail("Up from the last registration: cursor " + regs.currentIndex); return }
            const flick = w.detailsScrollView.contentItem
            key(Qt.Key_Home)
            if (Math.abs(flick.contentY - flick.originY) > 1) { fail("Home in the registrations left the details at " + flick.contentY); return }
            key(Qt.Key_End)
            if (Math.abs(flick.contentY - (flick.originY + flick.contentHeight - flick.height)) > 1) {
                fail("End in the registrations left the details at " + flick.contentY); return
            }
            advance("details-reachable-by-keyboard"); break
        }
        case 40: {
            // The first and last months' dots are drawn whole, clear of the y axis line and inside the plot, while the
            // month labels stay in the band below the plot, from its left edge to its right.
            const chart = w.trendView.item, graph = chart.graphView, area = graph.plotArea, axis = graph.axisX
            const xAt = (x) => area.x + (x - axis.min) / (axis.max - axis.min) * area.width
            const radius = 2.5, first = xAt(0), last = xAt(chart.trend.Points.length - 1)
            if (first - radius < area.x + 1.5 || last + radius > area.x + area.width) {
                fail("edge dots clipped: first at " + (first - area.x).toFixed(1) + " and last at " + (area.x + area.width - last).toFixed(1)
                    + " points from the plot's edges, radius " + radius); return
            }
            const below = area.y + area.height - 1
            for (const label of [chart.startLabel, chart.endLabel]) {
                if (label.y < below || label.y + label.height > graph.height + 1) { fail("month label '" + label.text + "' left the axis band"); return }
            }
            if (chart.startLabel.x < area.x - 0.5 || chart.endLabel.x + chart.endLabel.width > area.x + area.width + 0.5) {
                fail("month labels past the plot's edges"); return
            }
            console.log("HDB_API_KEYBOARD_PASS steps=" + phase)
            timer.stop(); w.finishGate()
            break
        }
        }
    }
}
