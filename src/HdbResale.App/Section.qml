import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// Pane heading: a title role label, optional trailing caption, one hairline.
ColumnLayout {
    id: root
    property string title
    property string caption
    spacing: 0
    readonly property var theme: Window.window.theme
    RowLayout {
        Layout.fillWidth: true
        Layout.topMargin: theme.m; Layout.bottomMargin: theme.s
        Layout.leftMargin: theme.m; Layout.rightMargin: theme.m
        Label {
            text: root.title; font.weight: Font.DemiBold; font.pointSize: Window.window.font.pointSize * theme.titleScale
            Layout.fillWidth: true; elide: Text.ElideRight
            Accessible.role: Accessible.Heading
        }
        Label {
            visible: root.caption !== ""; text: root.caption
            font.pointSize: Window.window.font.pointSize * theme.captionScale; color: theme.secondaryText
        }
    }
    Rectangle { Layout.fillWidth: true; Layout.preferredHeight: 1; color: theme.separator }
}
