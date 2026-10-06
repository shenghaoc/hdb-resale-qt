import QtQuick

// Product presentation vocabulary. Everything derives from the active Qt style
// palette and font so platform appearance, dark mode and text scaling flow
// through; only the rhythm and roles below are product decisions.
QtObject {
    id: theme
    required property var palette
    required property real unit          // text height of the platform font

    // Spacing rhythm (quarter / half / three-quarter / one text height).
    readonly property real xs: Math.round(unit * 0.25)
    readonly property real s: Math.round(unit * 0.5)
    readonly property real m: Math.round(unit * 0.75)
    readonly property real l: Math.round(unit)
    // Smallest comfortable pointer target on a desktop.
    readonly property real target: Math.max(28, Math.round(unit * 1.8))
    readonly property real radius: Math.round(unit * 0.375)

    // Typography roles are scale factors on the platform font.
    readonly property real titleScale: 1.15
    readonly property real captionScale: 0.9

    // Semantic colours.
    readonly property color text: palette.windowText
    readonly property color secondaryText: Qt.color(Qt.rgba(
        (palette.windowText.r + palette.window.r * 0.9) / 1.9,
        (palette.windowText.g + palette.window.g * 0.9) / 1.9,
        (palette.windowText.b + palette.window.b * 0.9) / 1.9, 1))
    readonly property color chrome: palette.window
    readonly property color panel: palette.base
    readonly property color separator: Qt.rgba(palette.windowText.r, palette.windowText.g, palette.windowText.b, 0.2)
    readonly property color accent: palette.highlight
    readonly property color accentText: palette.highlightedText
    // Floating map overlays: mostly opaque window colour with a hairline edge.
    readonly property color overlay: Qt.rgba(palette.window.r, palette.window.g, palette.window.b, 0.94)
}
