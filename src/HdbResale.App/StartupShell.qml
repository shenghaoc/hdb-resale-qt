import QtQuick
import QtQuick.Controls

// Diagnostic-only minimal QML/control shell. It deliberately has no Map or
// tile provider; compare it only with the same probe on another source revision.
ApplicationWindow {
    visible: true
    width: 1120; height: 760
    title: "HDB startup shell diagnostic"
    Label {
        anchors.centerIn: parent
        text: Resales.visibleCount + " transactions · " + Resales.mappedCount + " address summaries"
    }
    Timer {
        interval: 25; running: true; repeat: false
        onTriggered: { Resales.startupReady(); Qt.quit() }
    }
}
