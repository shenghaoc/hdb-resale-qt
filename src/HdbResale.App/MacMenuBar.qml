import QtQuick
import Qt.labs.platform as Platform

// macOS only: explicit OS roles place these in the application menu.
// Other desktops use Qt Quick Controls menus, without a QWidget fallback.
Platform.MenuBar {
    required property var commands
    Platform.Menu {
        title: qsTr("HDB Resale Explorer")
        Platform.MenuItem { text: commands.about.text; enabled: commands.about.enabled; role: Platform.MenuItem.AboutRole; onTriggered: commands.about.trigger() }
        Platform.MenuItem { text: commands.settings.text; enabled: commands.settings.enabled; role: Platform.MenuItem.PreferencesRole; shortcut: commands.settings.shortcut; onTriggered: commands.settings.trigger() }
        Platform.MenuItem { text: commands.quit.text; role: Platform.MenuItem.QuitRole; shortcut: commands.quit.shortcut; onTriggered: commands.quit.trigger() }
    }
    Platform.Menu {
        title: qsTr("View")
        Platform.MenuItem { text: commands.filters.text; enabled: commands.filters.enabled; shortcut: commands.filters.shortcut; onTriggered: commands.filters.trigger() }
        Platform.MenuItem { text: commands.results.text; enabled: commands.results.enabled; shortcut: commands.results.shortcut; onTriggered: commands.results.trigger() }
        Platform.MenuItem { text: commands.map.text; enabled: commands.map.enabled; shortcut: commands.map.shortcut; onTriggered: commands.map.trigger() }
        Platform.MenuItem { text: commands.showSelected.text; enabled: commands.showSelected.enabled; onTriggered: commands.showSelected.trigger() }
        Platform.MenuItem { text: commands.recenter.text; enabled: commands.recenter.enabled; onTriggered: commands.recenter.trigger() }
        Platform.MenuSeparator {}
        Platform.MenuItem { text: commands.reset.text; enabled: commands.reset.enabled; shortcut: commands.reset.shortcut; onTriggered: commands.reset.trigger() }
    }
}
