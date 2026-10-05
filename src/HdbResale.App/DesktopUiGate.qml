import QtQuick
import QtTest

// Opt-in native-window event regression check; never loaded in normal use.
Item {
    required property var targetWindow
    required property var townControl
    required property var typeControl
    required property var minimumControl
    required property var priceControl
    required property var recencyControl
    property int phase: 0
    property bool dispatching: false
    property double started: Date.now()
    property real savedZoom
    property real savedLongitude
    property real originalFont
    property var probedRow
    property string originalRowText
    property real originalRowHeight
    property string selectedKey
    TestEvent { id: events }
    function key(value, modifiers = Qt.NoModifier) { events.keyClick(value, modifiers, -1) }
    function advance(name) { console.log("HDB_DESKTOP_STEP " + name); phase++; started = Date.now() }
    function fail(message) { console.error("HDB_DESKTOP_FAIL " + message + " phase=" + phase); timer.stop(); Qt.quit() }
    Timer {
        id: timer; interval: 50; repeat: true; running: true
        onTriggered: {
            // QtTest event delivery pumps Qt events; do not re-enter a phase.
            if (dispatching) return
            dispatching = true
            try {
            if (Date.now() - started > 5000) { fail("deadline"); return }
            const map = targetWindow.mapView, list = targetWindow.resultView, commands = targetWindow.commands
            switch (phase) {
            case 0:
                if (!map.mapReady || !Resales.mapViewportReady || list.count !== 6 || !targetWindow.active) return
                originalFont = targetWindow.font.pointSize
                targetWindow.width = 640; targetWindow.height = 600
                advance("loaded"); break
            case 1:
                if (!targetWindow.compact || targetWindow.filterGrid.columns !== 2 || map.width < 600 || map.height < 120) return
                // Explicit visual-inspection mode holds a real native window.
                // It emits no pass; close it through the normal Quit command.
                if (Resales.desktopUiFault === "inspect-narrow") { stop(); return }
                key(Qt.Key_2, Qt.ControlModifier); advance("minimum-layout"); break
            case 2:
                if (!list.activeFocus || !list.visible || targetWindow.viewTabs.currentIndex !== 1) return
                probedRow = list.itemAtIndex(0)
                if (!probedRow) return
                originalRowText = probedRow.text; originalRowHeight = probedRow.height
                probedRow.text = "A long address and town label that must remain readable in a narrow native window. ".repeat(4)
                advance("long-label"); break
            case 3:
                if (probedRow.height <= originalRowHeight || probedRow.width > list.width || probedRow.contentItem.truncated) { fail("long row clipped"); return }
                probedRow.text = originalRowText
                key(Qt.Key_Down); key(Qt.Key_Return); advance("list-keyboard"); break
            case 4:
                if (Resales.selectedMapKey === "") return
                if (targetWindow.detailsView.height < 3 * targetWindow.unit) { fail("compact details clipped"); return }
                selectedKey = Resales.selectedMapKey
                commands.map.trigger(); savedZoom = map.zoomLevel
                Qt.callLater(() => key(Qt.Key_Plus, Qt.ShiftModifier)); advance("selected"); break
            case 5:
                if (map.zoomLevel !== savedZoom + 1 || !map.activeFocus) return
                savedZoom = map.zoomLevel; savedLongitude = map.center.longitude
                for (const control of [minimumControl, priceControl, townControl, typeControl, recencyControl, list]) {
                    control.forceActiveFocus(); key(Qt.Key_Minus); key(Qt.Key_Home)
                }
                priceControl.forceActiveFocus()
                advance("map-keyboard"); break
            case 6:
                if (map.zoomLevel !== savedZoom || map.center.longitude !== savedLongitude) { fail("editing moved map"); return }
                commands.about.trigger(); advance("editing-isolated"); break
            case 7:
                if (!targetWindow.aboutPopup.opened) return
                if (commands.map.enabled || commands.reset.enabled || commands.zoomIn.enabled) { fail("modal commands enabled"); return }
                key(Qt.Key_Equal); key(Qt.Key_Escape); advance("modal-isolated"); break
            case 8:
                if (targetWindow.aboutPopup.visible || !priceControl.activeFocus) return
                if (map.zoomLevel !== savedZoom) { fail("dialog moved map"); return }
                commands.reset.trigger(); targetWindow.width = 1000; targetWindow.height = 700
                advance("focus-restored"); break
            case 9:
                if (Resales.visibleCount !== 6 || priceControl.value !== 1000000 || targetWindow.filterGrid.columns !== 3) return
                targetWindow.font.pointSize = originalFont * 1.5
                targetWindow.width = 640; targetWindow.height = 760
                advance("reset-medium"); break
            case 10:
                if (targetWindow.filterGrid.columns !== 2 || map.height < 120 || townControl.width < 150 || priceControl.width < 150) return
                commands.settings.trigger(); advance("system-font-scaling"); break
            case 11:
                if (!targetWindow.settingsPopup.opened) return
                if (targetWindow.settingsPopup.width > targetWindow.width || targetWindow.settingsPopup.height > targetWindow.height) { fail("settings clipped"); return }
                key(Qt.Key_Escape); targetWindow.font.pointSize = originalFont
                targetWindow.width = 1360; targetWindow.height = 900
                commands.results.trigger(); advance("settings"); break
            case 12:
                if (targetWindow.compact || map.width < 300 || list.width < 250 || list.count !== 6) return
                if (Resales.selectedMapKey !== selectedKey) { fail("resize lost selection"); return }
                if (Resales.desktopUiFault === "skip-pass") { fail("negative control"); return }
                console.log("HDB_DESKTOP_PASS"); stop(); Qt.quit(); break
            }
            } finally { dispatching = false }
        }
    }
}
