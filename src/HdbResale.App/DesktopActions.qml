import QtQuick
import QtQuick.Controls
import QtPositioning

// One semantic command set for native menus, visible controls and shortcuts.
// Camera keys belong to the focused Map, never application-wide shortcuts.
Item {
    id: root
    required property var targetWindow
    required property var targetMap
    signal resetRequested()
    signal filtersRequested()
    signal mapRequested()
    signal resultsRequested()
    signal selectedRequested()
    signal aboutRequested()
    signal settingsRequested()
    property alias reset: resetAction
    property alias filters: filtersAction
    property alias map: mapAction
    property alias results: resultsAction
    property alias showSelected: selectedAction
    property alias zoomIn: zoomInAction
    property alias zoomOut: zoomOutAction
    property alias recenter: recenterAction
    property alias about: aboutAction
    property alias settings: settingsAction
    property alias quit: quitAction
    readonly property bool available: !targetWindow.modalOpen
    Action { id: resetAction; text: qsTr("Reset filters"); shortcut: "Ctrl+Shift+R"; enabled: root.available && targetWindow.filtersActive; onTriggered: resetRequested() }
    Action { id: filtersAction; text: qsTr("Focus filters"); shortcut: "Ctrl+L"; enabled: root.available; onTriggered: filtersRequested() }
    Action { id: mapAction; text: qsTr("Focus map"); shortcut: "Ctrl+1"; enabled: root.available; onTriggered: mapRequested() }
    Action { id: resultsAction; text: qsTr("Focus addresses"); shortcut: "Ctrl+2"; enabled: root.available; onTriggered: resultsRequested() }
    Action { id: selectedAction; text: qsTr("Show selected on map"); enabled: root.available && Resales.selectedLocated && targetMap.mapReady; onTriggered: selectedRequested() }
    Action { id: zoomInAction; text: qsTr("Zoom in"); enabled: root.available && targetMap.mapReady && targetMap.zoomLevel < targetMap.maximumZoomLevel; onTriggered: targetMap.zoomLevel = Math.min(targetMap.maximumZoomLevel, targetMap.zoomLevel + 1) }
    Action { id: zoomOutAction; text: qsTr("Zoom out"); enabled: root.available && targetMap.mapReady && targetMap.zoomLevel > targetMap.minimumZoomLevel; onTriggered: targetMap.zoomLevel = Math.max(targetMap.minimumZoomLevel, targetMap.zoomLevel - 1) }
    Action {
        id: recenterAction; text: qsTr("Return to Singapore"); enabled: root.available && targetMap.mapReady
        onTriggered: { targetMap.center = QtPositioning.coordinate(1.3521, 103.8198); targetMap.zoomLevel = 11 }
    }
    Action { id: aboutAction; text: qsTr("About HDB Resale Explorer"); enabled: root.available; onTriggered: aboutRequested() }
    Action { id: settingsAction; text: qsTr("Settings…"); shortcut: StandardKey.Preferences; enabled: root.available; onTriggered: settingsRequested() }
    Action { id: quitAction; text: qsTr("Quit HDB Resale Explorer"); shortcut: StandardKey.Quit; onTriggered: Qt.quit() }
}
