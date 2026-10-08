import QtQuick
import Qt.labs.platform as Platform

// macOS only: the application's native menu bar. Roles move About and Quit into the application menu, and the
// shortcuts are the platform's standard key equivalents. Other desktops keep their in-window controls.
Platform.MenuBar {
    id: root
    required property var appWindow
    // The focused text field, for the Edit menu; every other control lacks these functions.
    readonly property var editor: appWindow.activeFocusItem
    function editorCan(action) { return !!editor && typeof editor[action] === "function" }
    readonly property bool editorHasSelection: editorCan("copy") && (editor.selectedText || "").length > 0

    Platform.Menu {
        title: qsTr("File")
        Platform.MenuItem { text: qsTr("About HDB Resale Explorer"); role: Platform.MenuItem.AboutRole; onTriggered: root.appWindow.showAbout() }
        Platform.MenuItem { text: qsTr("Close Window"); shortcut: StandardKey.Close; onTriggered: root.appWindow.close() }
        Platform.MenuItem { text: qsTr("Quit HDB Resale Explorer"); role: Platform.MenuItem.QuitRole; shortcut: StandardKey.Quit; onTriggered: Qt.quit() }
    }
    Platform.Menu {
        title: qsTr("Edit")
        Platform.MenuItem { text: qsTr("Undo"); shortcut: StandardKey.Undo; enabled: root.editorCan("undo") && root.editor.canUndo === true; onTriggered: root.editor.undo() }
        Platform.MenuItem { text: qsTr("Redo"); shortcut: StandardKey.Redo; enabled: root.editorCan("redo") && root.editor.canRedo === true; onTriggered: root.editor.redo() }
        Platform.MenuSeparator {}
        Platform.MenuItem { text: qsTr("Cut"); shortcut: StandardKey.Cut; enabled: root.editorHasSelection && root.editor.readOnly === false; onTriggered: root.editor.cut() }
        Platform.MenuItem { text: qsTr("Copy"); shortcut: StandardKey.Copy; enabled: root.editorHasSelection; onTriggered: root.editor.copy() }
        Platform.MenuItem { text: qsTr("Paste"); shortcut: StandardKey.Paste; enabled: root.editorCan("paste") && root.editor.canPaste === true; onTriggered: root.editor.paste() }
        Platform.MenuItem { text: qsTr("Select All"); shortcut: StandardKey.SelectAll; enabled: root.editorCan("selectAll"); onTriggered: root.editor.selectAll() }
    }
    Platform.Menu {
        title: qsTr("View")
        Platform.MenuItem { text: qsTr("Map"); shortcut: "Ctrl+1"; onTriggered: root.appWindow.showView(0) }
        Platform.MenuItem { text: qsTr("Addresses"); shortcut: "Ctrl+2"; onTriggered: root.appWindow.showView(1) }
        Platform.MenuSeparator {}
        Platform.MenuItem {
            text: qsTr("Show Selected Address on Map")
            enabled: Resales.selectedMapKey !== "" && Resales.selectedLocated
            onTriggered: root.appWindow.showSelectedOnMap()
        }
        Platform.MenuItem { text: qsTr("Return to Singapore"); onTriggered: root.appWindow.recenterMap() }
        Platform.MenuSeparator {}
        Platform.MenuItem { text: qsTr("Reset Filters"); shortcut: "Ctrl+Shift+R"; enabled: root.appWindow.activeFilterCount > 0; onTriggered: Resales.resetFilters() }
        // AppKit adds Enter Full Screen to a menu titled View by itself.
    }
    Platform.Menu {
        title: qsTr("Window")
        Platform.MenuItem { text: qsTr("Minimize"); shortcut: "Ctrl+M"; onTriggered: root.appWindow.showMinimized() }
        Platform.MenuItem { text: qsTr("Zoom"); onTriggered: root.appWindow.toggleZoom() }
    }
}
