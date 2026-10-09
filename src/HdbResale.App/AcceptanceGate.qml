// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick
import QtTest

// Acceptance instrumentation only. Input is QtTest delivery inside the app,
// never compositor input; captures exclude native frames and platform overlays.
Item {
    id: gate
    required property var targetWindow
    readonly property var w: targetWindow
    property int phase: 0
    property double started: Date.now()
    property bool dispatching: false
    property real beforeScroll: 0
    TestEvent { id: events }
    TestResult { id: result }
    function item(name) {
        const found = result.findChild(w, name)
        if (!found) throw new Error("missing objectName=" + name)
        return found
    }
    function fail(message) {
        console.error("HDB_ACCEPTANCE_FAIL phase=" + phase + " " + message)
        timer.stop(); Qt.quit()
    }
    function step() {
        if (Date.now() - started > 20000) throw new Error("deadline")
        switch (phase) {
        case 0:
            if (!w.active) w.requestActivate()
            if (!w.active || item("transactionsList").count !== 6) return
            if (Qt.platform.pluginName !== "wayland") throw new Error("actual QPA=" + Qt.platform.pluginName)
            console.log("HDB_ACCEPTANCE_PLATFORM " + Qt.platform.pluginName)
            events.keyClick(Qt.Key_F, Qt.ControlModifier, -1)
            for (const c of "bedok res") events.keyClickChar(c, Qt.NoModifier, -1)
            phase++; break
        case 1:
            if (item("transactionsList").count !== 3) return
            events.keyClick(Qt.Key_Down, Qt.NoModifier, -1)
            events.keyClick(Qt.Key_Down, Qt.NoModifier, -1)
            events.keyClick(Qt.Key_Return, Qt.NoModifier, -1)
            phase++; break
        case 2:
            if (Resales.selectedMapKey !== "bedok-748b-bedok-reservoir-cres" || !Resales.detailReady) return
            const details = item("detailsScroll")
            beforeScroll = details.contentItem.contentY
            if (details.contentItem.contentHeight <= details.contentItem.height) throw new Error("inspector not scrollable")
            if (!events.mouseWheel(details, details.width / 2, details.height / 2, Qt.NoButton, Qt.NoModifier, 0, -480, -1))
                throw new Error("QtTest wheel delivery failed")
            phase++; break
        case 3:
            if (item("detailsScroll").contentItem.contentY <= beforeScroll + 20) return
            console.log("HDB_ACCEPTANCE_SCROLL before=" + beforeScroll + " after=" + item("detailsScroll").contentItem.contentY)
            phase++; started = Date.now(); break
        case 4:
            if (Date.now() - started < 800) return
            phase = 6 // asynchronous item render; no second grab while pending
            if (!item("inspector").grabToImage(function(image) {
                if (!image.saveToFile("acceptance-poc.png")) { fail("Qt item capture could not be saved"); return }
                console.log("HDB_ACCEPTANCE_CAPTURE acceptance-poc.png inspector-subtree")
                console.log("HDB_ACCEPTANCE_READY external AT-SPI checkpoint")
                phase = 5; started = Date.now()
            })) throw new Error("Qt item capture did not start")
            break
        case 5:
            if (Date.now() - started < 15000) return
            console.log("HDB_ACCEPTANCE_PASS poc")
            timer.stop(); w.finishGate(); break
        }
    }
    Timer {
        id: timer; interval: 50; repeat: true; running: true
        onTriggered: {
            if (gate.dispatching) return
            gate.dispatching = true
            try { gate.step() } catch (error) { gate.fail(String(error)) } finally { gate.dispatching = false }
        }
    }
}
