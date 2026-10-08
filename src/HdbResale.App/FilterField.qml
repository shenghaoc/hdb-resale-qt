import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// Caption above one filter control, in the caption role so values stay dominant.
ColumnLayout {
    id: root
    property string caption
    property string problem        // non-empty shows a warning under the control
    default property alias control: slot.data
    spacing: theme.xs
    readonly property var theme: Window.window.theme
    Label {
        text: root.caption
        font.pointSize: Window.window.font.pointSize * theme.captionScale
        color: theme.secondaryText
        Layout.fillWidth: true; elide: Text.ElideRight
    }
    RowLayout { id: slot; spacing: theme.xs; Layout.fillWidth: true }
    Label {
        visible: root.problem !== ""
        text: root.problem !== "" ? "⚠ " + root.problem : ""
        Accessible.ignored: root.problem === ""      // no empty alert node while there is no problem
        font.pointSize: Window.window.font.pointSize * theme.captionScale; font.bold: true
        wrapMode: Text.WordWrap; Layout.fillWidth: true
        Accessible.role: Accessible.AlertMessage
    }
}
