import QtQuick
import QtQuick.Controls

// Opt-in visual-QA harness (HDB_SCREENSHOT_DIR=<dir>); never loaded in normal use.
// It drives named UI states, verifies each state's preconditions within a bounded
// wait, and either grabs PNGs (default) or, in HDB_SCREENSHOT_MODE=native, announces
// each ready state so tools/ui_screenshots.py can take a real compositor capture.
// Producing an image proves only that the state was reached; it is not acceptance.
Item {
    id: gate
    required property var targetWindow
    property real baseFont: -1      // captured once at start; never bound
    property int step: -1
    property string stage: "start"  // start → restore → wait → settle → capturing/acking
    property double stageStarted: Date.now()
    property double startedAt: Date.now()
    property var scenario
    readonly property bool nativeMode: Resales.screenshotMode === "native"
    readonly property var win: targetWindow
    readonly property var popups: [win.settingsPopup, win.aboutPopup, win.dataPopup]
    Button { id: styleProbe; visible: false }
    // Native captures need the compositor to present frames; otherwise it would hand back a stale image.
    property int frames: 0
    property int framesAtApply: 0
    Connections { target: targetWindow; function onFrameSwapped() { frames++ } }
    // "stress" runs against the synthetic UI fixture (tools/make_ui_fixture.py): many results,
    // long addresses, ambiguous/unmatched rows. Never a data or coverage claim.
    readonly property bool stress: Resales.screenshotProfile === "stress"
    readonly property string fTown: stress ? "TOA PAYOH" : "ANG MO KIO"
    readonly property string fType: stress ? "4 ROOM" : "3 ROOM"

    // Each scenario fully specifies window size, font scale, pane, dialog and filters
    // so that every state is independent of the previous one. `expect` returns an
    // empty string when its preconditions hold, otherwise the reason they do not.
    readonly property var allScenarios: [
        { name: "default-wide", w: 1360, h: 900, settle: 2500, expect: () => selected(false) || compact(false) || filters("All towns", "All flat types") || countAll() },
        { name: "default", w: 1100, h: 760, settle: 1200, expect: () => selected(false) || compact(false) || filters("All towns", "All flat types") || countAll() },
        { name: "compact-map", w: 640, h: 600, settle: 1200, tab: 0, expect: () => compact(true) || tab(0) || selected(false) },
        { name: "filtered", w: 1100, h: 760, settle: 1500,
          setup: () => { Resales.setTown(fTown); Resales.setFlatType(fType) },
          expect: () => selected(false) || filters(fTown, fType) || countFiltered() },
        { name: "empty", w: 1100, h: 760, settle: 1200,
          setup: () => { Resales.setMinimumPrice(1000000) },
          expect: () => selected(false) || count(0) },
        { name: "reset", w: 1100, h: 760, settle: 1200,
          setup: () => Resales.resetFilters(),
          expect: () => selected(false) || filters("All towns", "All flat types") || countAll() },
        { name: "price-invalid", w: 1100, h: 760, settle: 1200,
          setup: () => { Resales.setMinimumPrice(600000); Resales.setMaximumPrice(300000) },
          expect: () => selected(false) || (win.filterBar.priceInvalid ? "" : "price range not invalid") || count(0) },
        { name: "compact-filters-open", w: 640, h: 600, settle: 1200, tab: 0,
          setup: () => { Resales.setTown(fTown); win.filterBar.expanded = true },
          expect: () => compact(true) || tab(0) || (win.filterBar.showFields ? "" : "filters not shown") || filters(fTown, "All flat types") },
        { name: "compact-filters-active", w: 640, h: 600, settle: 1200, tab: 0,
          setup: () => { Resales.setTown(fTown); Resales.setFlatType(fType) },
          expect: () => compact(true) || (win.filterBar.showFields ? "filters shown" : "") || filters(fTown, fType) },
        { name: "selected", w: 1100, h: 760, settle: 2200,
          setup: () => Resales.selectAddressAt(0),
          expect: () => selected(true) || compact(false) || countAll() },
        { name: "selected-ambiguous", stressOnly: true, w: 1100, h: 760, settle: 1800,
          setup: () => Resales.selectAddress("PASIR RIS|107|TEST PASIR STREET 8"),
          expect: () => selected(true) || (Resales.selectedMapKey === "PASIR RIS|107|TEST PASIR STREET 8" ? "" : "wrong selection") },
        { name: "selected-unmatched", stressOnly: true, w: 1100, h: 760, settle: 1800,
          setup: () => Resales.selectAddress("CLEMENTI|105|TEST CLEMENTI STREET 6"),
          expect: () => selected(true) || (Resales.selectedMapKey === "CLEMENTI|105|TEST CLEMENTI STREET 6" ? "" : "wrong selection") },
        { name: "map-loading-card", w: 1100, h: 760, settle: 900, setup: () => { win.mapStatusCard.forced = "loading" },
          expect: () => (win.mapStatusCard.visible ? "" : "status card hidden") },
        { name: "map-unavailable-card", w: 1100, h: 760, settle: 900, setup: () => { win.mapStatusCard.forced = "error" },
          expect: () => (win.mapStatusCard.visible ? "" : "status card hidden") },
        { name: "selected-compact-details", w: 700, h: 700, settle: 1500, tab: 1,
          setup: () => Resales.selectAddressAt(0),
          expect: () => selected(true) || compact(true) || tab(1) },
        { name: "large-text", w: 1100, h: 760, settle: 1500, fontScale: 1.4,
          setup: () => Resales.selectAddressAt(0),
          expect: () => selected(true) },
        { name: "settings", w: 1000, h: 760, settle: 900, dialog: "settings", expect: () => selected(true) },
        { name: "about", w: 1000, h: 760, settle: 900, dialog: "about", expect: () => selected(true) },
        { name: "data-dialog", w: 1000, h: 760, settle: 900, dialog: "data", expect: () => selected(true) },
        { name: "dialog-compact-large-text", w: 640, h: 600, settle: 900, dialog: "settings", fontScale: 1.4,
          expect: () => selected(true) }
    ]

    // HDB_SCREENSHOT_ONLY=a,b limits the run (e.g. scaled runs on a screen too small for every size).
    readonly property var only: Resales.screenshotOnly === "" ? [] : Resales.screenshotOnly.split(",")
    readonly property var scenarios: allScenarios.filter(s => (!s.stressOnly || stress) && (only.length === 0 || only.includes(s.name)))
    function selected(want) { return (Resales.selectedMapKey !== "") === want ? "" : "selection is " + (want ? "empty" : "set") }
    function compact(want) { return win.compact === want ? "" : "compact is " + win.compact }
    function countAll() { return stress ? (win.resultView.count > 100 ? "" : "stress list too short: " + win.resultView.count) : count(6) }
    function countFiltered() { return stress ? (win.resultView.count > 0 && win.resultView.count < 100 ? "" : "filtered stress list " + win.resultView.count) : count(1) }
    function count(n) { return win.resultView.count === n ? "" : "list count " + win.resultView.count + " != " + n }
    function tab(i) { return win.viewTabs.currentIndex === i ? "" : "tab index " + win.viewTabs.currentIndex }
    function filters(town, type) {
        return win.townControl.currentText === town && win.typeControl.currentText === type ? "" : "filters " + win.townControl.currentText + "/" + win.typeControl.currentText
    }
    function dialogFor(name) { return name === "settings" ? win.settingsPopup : name === "about" ? win.aboutPopup : win.dataPopup }
    function fail(message) {
        console.error("HDB_SHOT_FAIL " + (scenario ? scenario.name : "init") + ": " + message)
        timer.stop(); guard.stop(); ackTimer.stop(); Qt.exit(1)
    }
    function enter(name) { stage = name; stageStarted = Date.now() }
    function elapsed() { return Date.now() - stageStarted }
    function meta(name) {
        const screen = win.screen
        return JSON.stringify({
            name, mode: nativeMode ? "native-window" : "grab",
            window: [win.width, win.height], dpr: win.devicePixelRatio, screenDpr: screen ? screen.devicePixelRatio : 0,
            font: win.font.pointSize, baseFont, platform: Qt.platform.pluginName,
            style: String(styleProbe.background).replace(/_QMLTYPE.*|\(.*/, ""),
            colorScheme: Qt.styleHints.colorScheme, windowColor: String(win.palette.window), textColor: String(win.palette.windowText),
            synthetic: Resales.syntheticBasemapHost !== "", base: String(win.palette.base), themePanel: String(win.theme.panel), themeChrome: String(win.theme.chrome)
        })
    }
    Component.onCompleted: {
        baseFont = win.font.pointSize
        // Process-local appearance request. Whether it changes the effective palette is
        // recorded per scenario (colorScheme/windowColor), not assumed.
        if (Resales.screenshotColorScheme !== "") {
            console.log("HDB_SCHEME before=" + Qt.styleHints.colorScheme + " window=" + win.palette.window)
            Qt.styleHints.colorScheme = Resales.screenshotColorScheme === "dark" ? Qt.Dark : Qt.Light
            console.log("HDB_SCHEME after=" + Qt.styleHints.colorScheme + " window=" + win.palette.window)
        }
    }

    Timer {
        id: timer; interval: 100; repeat: true; running: true
        onTriggered: {
            if (gate.stage === "capturing" || gate.stage === "acking") return
            if (gate.step < 0) {
                // Wait for the map and list before the first state.
                if (!win.mapView.mapReady || win.resultView.count === 0) {
                    if (Date.now() - gate.startedAt > 15000) gate.fail("application not ready")
                    return
                }
                gate.step = 0
            }
            if (gate.step >= gate.scenarios.length) {
                timer.stop(); console.log("HDB_SHOTS_DONE " + gate.scenarios.length); Qt.quit(); return
            }
            gate.scenario = gate.scenarios[gate.step]
            const s = gate.scenario
            switch (gate.stage) {
            case "start":
                // Restore the baseline before applying anything and verify it took effect.
                for (const p of gate.popups) if (p.visible) p.close()
                win.font.pointSize = gate.baseFont
                Resales.resetFilters(); win.filterBar.expanded = false; win.mapStatusCard.forced = ""
                gate.enter("restore"); break
            case "restore":
                if (gate.popups.some(p => p.visible) || Math.abs(win.font.pointSize - gate.baseFont) > 0.01) {
                    if (gate.elapsed() > 4000) gate.fail("baseline not restored (font " + win.font.pointSize + " vs " + gate.baseFont + ")")
                    return
                }
                win.width = s.w; win.height = s.h
                if (s.fontScale) win.font.pointSize = gate.baseFont * s.fontScale
                if (s.tab !== undefined) win.viewTabs.currentIndex = s.tab
                if (s.setup) s.setup()
                if (s.dialog) win.showDialog(gate.dialogFor(s.dialog))
                win.raise(); win.requestActivate()
                gate.framesAtApply = gate.frames
                gate.enter("wait"); break
            case "wait": {
                const want = s.fontScale || 1
                let reason = ""
                if (win.width !== s.w || win.height !== s.h) reason = "window " + win.width + "x" + win.height
                else if (Math.abs(win.font.pointSize - gate.baseFont * want) > 0.01) reason = "font " + win.font.pointSize
                else if (s.dialog && !gate.dialogFor(s.dialog).opened) reason = "dialog not open"
                else if (!s.dialog && gate.popups.some(p => p.visible)) reason = "unexpected dialog"
                // `spectacle -a` captures the *active* window: never capture unless it is this one.
                else if (nativeMode && !win.active) { win.requestActivate(); reason = "window not active; a native capture would show another window" }
                else if (nativeMode && gate.frames === gate.framesAtApply) { win.update(); reason = "no frames presented since the state was applied (display not presenting)" }
                else if (!win.mapView.mapReady) reason = "map not ready"
                else if (Resales.screenshotColorScheme !== "" && Qt.styleHints.colorScheme !== (Resales.screenshotColorScheme === "dark" ? Qt.Dark : Qt.Light)) reason = "requested colour scheme not applied (" + Qt.styleHints.colorScheme + ")"
                else reason = s.expect ? s.expect() : ""
                if (reason !== "") {
                    if (gate.elapsed() > 8000) gate.fail("precondition not met: " + reason)
                    return
                }
                gate.enter("settle"); break
            }
            case "settle":
                // Tiles and layout need a few frames; the preconditions are re-checked at capture.
                if (gate.elapsed() < s.settle) return
                gate.capture(); break
            }
        }
    }
    function capture() {
        const s = scenario
        const reason = (nativeMode && !win.active) ? "window lost focus" : s.expect ? s.expect() : ""
        if (reason !== "") { fail("state changed before capture: " + reason); return }
        const dir = Resales.screenshotDirectory
        console.log("HDB_SHOT_META " + meta(s.name))
        if (nativeMode) {
            enter("acking")
            console.log("HDB_SHOT_READY " + s.name)
            ackTimer.ack = dir + "/" + s.name + ".ack"; ackTimer.begin = Date.now(); ackTimer.restart()
            return
        }
        enter("capturing")
        grab(win.grabRoot, dir + "/" + s.name + ".shell.png", () => {
            const open = popups.find(p => p.visible)
            if (!open) { done(); return }
            // The popup item carries the frame, title and buttons; its window position
            // is recorded so the driver composites it where the user would see it.
            const item = open.contentItem.parent
            const origin = item.mapToItem(win.grabRoot, 0, 0)
            console.log("HDB_SHOT_POPUP " + JSON.stringify({ name: s.name, x: origin.x, y: origin.y, w: item.width, h: item.height }))
            grab(item, dir + "/" + s.name + ".overlay.png", done)
        })
    }
    function grab(item, path, then) {
        guard.restart()
        const queued = item.grabToImage(function(result) {
            guard.stop()
            if (!result.saveToFile(path)) { fail("could not save " + path); return }
            console.log("HDB_SHOT_SAVED " + path)
            then()
        })
        if (!queued) fail("grabToImage could not start for " + path)
    }
    function done() { step++; enter("start") }
    Timer { id: guard; interval: 6000; onTriggered: gate.fail("grabToImage callback timed out") }
    Timer {
        id: ackTimer; interval: 150; repeat: true
        property string ack; property double begin
        onTriggered: {
            if (Date.now() - begin > 30000) { stop(); gate.fail("capture driver did not acknowledge"); return }
            const xhr = new XMLHttpRequest()
            xhr.open("GET", "file://" + ack)
            xhr.onreadystatechange = () => {
                if (xhr.readyState !== XMLHttpRequest.DONE || !ackTimer.running) return
                if (xhr.status === 200 || (xhr.status === 0 && xhr.responseText !== "")) { ackTimer.stop(); gate.done() }
            }
            xhr.send()
        }
    }
}
