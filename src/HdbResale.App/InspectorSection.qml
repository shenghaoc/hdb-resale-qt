import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// One titled group of label/value facts in the details inspector, laid out as a form: labels in a quiet
// right-aligned column, values beside them in tabular figures. Every figure comes from C#; this only places it.
// Each fact is one accessible static text ("Label: value"), as a form row reads to a screen reader.
// The group is a keyboard stop (where Tab reaches every control: with VoiceOver, on KDE) and can take focus from a
// screen reader, so its facts can be reached however far the details have scrolled; the details scroll it into view
// when it takes focus.
Item {
    id: root
    property string title
    property string note
    property var facts: []
    readonly property alias factCount: rows.count
    readonly property var theme: Window.window.theme
    implicitWidth: column.implicitWidth
    implicitHeight: column.implicitHeight
    activeFocusOnTab: true
    Accessible.role: Accessible.Grouping
    Accessible.name: title
    Accessible.focusable: true
    ColumnLayout {
        id: column
        anchors.left: parent.left; anchors.right: parent.right
        spacing: root.theme.xs
        Label {
            text: root.title; font.weight: Font.DemiBold; Layout.fillWidth: true; elide: Text.ElideRight
            Accessible.role: Accessible.Heading
        }
        ColumnLayout {
            Layout.fillWidth: true
            spacing: root.theme.xs
            Repeater {
                id: rows
                model: root.facts
                delegate: Item {
                    id: row
                    required property var modelData
                    Layout.fillWidth: true
                    implicitHeight: Math.max(factLabel.implicitHeight, factValue.implicitHeight)
                    Accessible.role: Accessible.StaticText
                    Accessible.name: modelData.label + ": " + modelData.value
                    Label {
                        id: factLabel
                        anchors.left: parent.left; anchors.top: parent.top
                        width: root.labelWidth
                        text: row.modelData.label; color: root.theme.secondaryText
                        horizontalAlignment: Text.AlignRight; wrapMode: Text.WordWrap
                        Accessible.ignored: true
                    }
                    Label {
                        id: factValue
                        anchors.left: factLabel.right; anchors.leftMargin: root.theme.s; anchors.right: parent.right; anchors.top: parent.top
                        text: row.modelData.value; wrapMode: Text.WordWrap
                        font.features: { "tnum": 1 }
                        Accessible.ignored: true
                    }
                }
            }
        }
        Label {
            visible: root.note !== ""; text: root.note; color: root.theme.secondaryText
            font.pointSize: Window.window.font.pointSize * root.theme.captionScale
            wrapMode: Text.WordWrap; Layout.fillWidth: true
        }
    }
    // Keyboard focus on the group, drawn as the address list's cursor ring is, only in the active window.
    Rectangle {
        anchors.fill: parent; anchors.margins: -root.theme.xs / 2
        visible: root.activeFocus && Window.window.active
        color: "transparent"; radius: root.theme.xs; border.width: 2; border.color: Window.window.palette.highlight
    }
    // The label column is as wide as the widest label measured in the platform font (character counts mislead with
    // proportional fonts), capped at two fifths of the section so values keep room.
    TextMetrics { id: labels; font: root.Window.window.font; onFontChanged: root.measureLabels() }
    property int widestLabel: 0
    readonly property int labelWidth: Math.min(widestLabel, Math.floor(root.width * 0.4))
    // Measured imperatively: a binding that set the metrics' text would read its own result back.
    function measureLabels() {
        let widest = 0
        for (const fact of root.facts) { labels.text = fact.label; widest = Math.max(widest, labels.advanceWidth) }
        widestLabel = Math.ceil(widest)
    }
    onFactsChanged: measureLabels()
    Component.onCompleted: measureLabels()
}
