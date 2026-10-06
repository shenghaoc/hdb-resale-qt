import QtQuick

// Floating map chrome: one corner radius, one edge, window-coloured fill.
Rectangle {
    readonly property var theme: Window.window.theme
    color: theme.overlay
    radius: theme.radius
    border.width: 1
    border.color: theme.separator
}
