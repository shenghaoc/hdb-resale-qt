import QtQuick
import QtQuick.Controls

// Product presentation vocabulary. Everything derives from the active Qt style
// palette and font so platform appearance, dark mode and text scaling flow
// through; only the rhythm and roles below are product decisions.
Item {
    id: theme
    visible: false
    required property real unit          // text height of the platform font
    // Tokens read a Control's inherited palette: unlike a copied palette value it is
    // refreshed when the style, system scheme or an application override changes.
    Control { id: sample }

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
    readonly property color text: sample.palette.windowText
    // 70 % text / 30 % window: ≥ 5.3:1 against the window colour in the light, dark and
    // high-contrast palettes checked (AA for small text needs 4.5:1). Do not lighten.
    readonly property color secondaryText: Qt.color(Qt.rgba(
        sample.palette.windowText.r * 0.7 + sample.palette.window.r * 0.3,
        sample.palette.windowText.g * 0.7 + sample.palette.window.g * 0.3,
        sample.palette.windowText.b * 0.7 + sample.palette.window.b * 0.3, 1))
    readonly property color chrome: sample.palette.window
    readonly property color panel: sample.palette.base
    readonly property color separator: Qt.rgba(sample.palette.windowText.r, sample.palette.windowText.g, sample.palette.windowText.b, 0.2)
    readonly property color accent: sample.palette.highlight
    readonly property color accentText: sample.palette.highlightedText
    // Selection must not rely on the palette's saturated highlight: white text on Breeze's
    // #3daee9 is 2.4:1. A light tint keeps text contrast; a leading bar and bold type mark it.
    readonly property bool dark: sample.palette.window.hslLightness < 0.5
    readonly property color selectedFill: Qt.tint(sample.palette.base, Qt.rgba(sample.palette.highlight.r, sample.palette.highlight.g, sample.palette.highlight.b, 0.2))
    readonly property color hoverFill: Qt.tint(sample.palette.base, Qt.rgba(sample.palette.highlight.r, sample.palette.highlight.g, sample.palette.highlight.b, 0.08))
    readonly property color accentBar: dark ? sample.palette.highlight : Qt.darker(sample.palette.highlight, 1.7)
    // Warning text must stay legible on the chrome colour in light and dark appearances.
    readonly property color warning: sample.palette.window.hslLightness > 0.5 ? "#8a3200" : "#ffb070"
    // Floating map overlays: mostly opaque window colour with a hairline edge.
    readonly property color overlay: Qt.rgba(sample.palette.window.r, sample.palette.window.g, sample.palette.window.b, 0.94)
}
