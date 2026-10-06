import QtQuick

// Opt-in visual-QA harness: HDB_SCREENSHOT_DIR=<dir> renders named UI states
// to PNG files and quits. Never loaded in normal use; asserts nothing.
Item {
    required property var targetWindow
    required property var townControl
    required property var typeControl
    required property var priceControl
    property int step: 0
    property bool busy: false
    readonly property real baseFont: 0
    property real originalFont: targetWindow.font.pointSize
    readonly property var scenarios: [
        { name: "wide-startup", w: 1360, h: 900, settle: 4000 },
        { name: "default", w: 1100, h: 760, settle: 1500 },
        { name: "selected", w: 1100, h: 760, settle: 2500, run: () => Resales.selectAddressAt(0) },
        { name: "selected-compact", w: 700, h: 700, settle: 2000 },
        { name: "compact-map", w: 640, h: 600, settle: 1500, run: () => { targetWindow.viewTabs.currentIndex = 0 } },
        { name: "filtered", w: 1100, h: 760, settle: 2000, run: () => { Resales.setTown("ANG MO KIO"); Resales.setFlatType("3 ROOM") } },
        { name: "empty", w: 1100, h: 760, settle: 1500, run: () => { Resales.setTown("ANG MO KIO"); Resales.setFlatType("2 ROOM"); Resales.setMinimumPrice(900000) } },
        { name: "reset", w: 1100, h: 760, settle: 1500, run: () => Resales.resetFilters() },
        { name: "large-text", w: 1000, h: 760, settle: 1500, run: () => { targetWindow.font.pointSize = originalFont * 1.4 } },
        { name: "settings", w: 1000, h: 760, settle: 800, run: () => { targetWindow.font.pointSize = originalFont; targetWindow.commands.settings.trigger() } },
        { name: "about", w: 1000, h: 760, settle: 800, run: () => { targetWindow.settingsPopup.close(); targetWindow.commands.about.trigger() } },
        { name: "data-dialog", w: 1000, h: 760, settle: 800, run: () => { targetWindow.aboutPopup.close(); targetWindow.showDialog(targetWindow.dataPopup) } }
    ]
    function shoot(name) {
        const dialog = [targetWindow.settingsPopup, targetWindow.aboutPopup, targetWindow.dataPopup].find(d => d.visible)
        const item = dialog ? dialog.contentItem : targetWindow.grabRoot
        console.log("HDB_SHOT_GRAB " + name)
        item.grabToImage(function(result) {
            const path = Resales.screenshotDirectory + "/" + name + ".png"
            console.log("HDB_SHOT " + name + " " + result.saveToFile(path))
            next()
        })
    }
    function next() { step++; busy = false }
    Timer {
        id: timer; interval: 100; repeat: true; running: true
        onTriggered: {
            if (busy) return
            if (step >= scenarios.length) { stop(); console.log("HDB_SHOTS_DONE"); Qt.quit(); return }
            const s = scenarios[step]
            if (!targetWindow.mapView.mapReady && step === 0 && Date.now() - startedAt < 8000) return
            targetWindow.raise(); targetWindow.requestActivate()
            busy = true; console.log("HDB_SHOT_BEGIN " + s.name)
            targetWindow.width = s.w; targetWindow.height = s.h
            if (s.run) s.run()
            settle.interval = s.settle; settle.shotName = s.name; settle.restart()
        }
    }
    property double startedAt: Date.now()
    Timer { id: settle; property string shotName; onTriggered: shoot(shotName) }
}
