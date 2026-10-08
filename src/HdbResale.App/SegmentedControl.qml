import QtQuick
import QtQuick.Templates as T

// Mutually exclusive segments drawn from the active palette. Qt's macOS style has no segmented control and
// draws TabBar with its Fusion fallback, so this builds on the unstyled templates instead. Each segment is a
// radio button for assistive technology; Left and Right move the selection while the control has focus.
T.Control {
    id: root
    property var model: []
    property int currentIndex: 0
    readonly property var theme: Window.window.theme
    readonly property bool dark: palette.window.hslLightness < 0.5
    readonly property real segmentWidth: {
        let widest = 0
        for (const label of model) widest = Math.max(widest, metrics.advanceWidth(label))
        return Math.ceil(widest) + 2 * theme.l
    }
    FontMetrics { id: metrics; font: root.font }
    focusPolicy: Qt.TabFocus
    padding: 2
    implicitWidth: leftPadding + rightPadding + segmentWidth * model.length
    implicitHeight: topPadding + bottomPadding + Math.ceil(metrics.height) + 2 * theme.xs
    Accessible.role: Accessible.PageTabList
    Keys.onLeftPressed: currentIndex = Math.max(0, currentIndex - 1)
    Keys.onRightPressed: currentIndex = Math.min(model.length - 1, currentIndex + 1)

    background: Rectangle {
        radius: theme.s
        // A recessed track; the window text tint keeps it visible in light, dark and high-contrast palettes.
        color: Qt.rgba(root.palette.windowText.r, root.palette.windowText.g, root.palette.windowText.b, root.dark ? 0.12 : 0.07)
        border.width: root.visualFocus ? 2 : 0
        border.color: root.palette.accent
    }
    contentItem: Row {
        Repeater {
            model: root.model
            delegate: T.AbstractButton {
                id: segment
                required property int index
                required property string modelData
                width: root.segmentWidth
                height: root.availableHeight
                text: modelData
                checked: root.currentIndex === index
                focusPolicy: Qt.NoFocus
                onClicked: root.currentIndex = index
                Accessible.role: Accessible.RadioButton
                Accessible.name: text
                Accessible.checkable: true
                Accessible.checked: checked
                Accessible.onPressAction: root.currentIndex = index
                background: Rectangle {
                    visible: segment.checked
                    radius: root.theme.s - root.padding
                    // The raised segment: the content surface in light appearances, a lifted grey in dark ones.
                    color: root.dark ? Qt.tint(root.palette.window, Qt.rgba(1, 1, 1, 0.22)) : root.palette.base
                    border.width: root.dark ? 0 : 1
                    border.color: Qt.rgba(root.palette.windowText.r, root.palette.windowText.g, root.palette.windowText.b, 0.12)
                }
                contentItem: Text {
                    text: segment.text
                    font: root.font
                    color: segment.checked ? root.palette.windowText : root.theme.secondaryText
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                    elide: Text.ElideRight
                }
            }
        }
    }
}
