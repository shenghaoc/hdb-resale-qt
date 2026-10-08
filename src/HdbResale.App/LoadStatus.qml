import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// Loading progress or a load error with Retry, shown where the addresses would be.
ColumnLayout {
    id: root
    readonly property var theme: Window.window.theme
    visible: Resales.loading || Resales.canRetry
    spacing: theme.m
    ProgressBar {
        visible: Resales.loading
        indeterminate: true
        Layout.fillWidth: true
        Accessible.name: qsTr("Loading addresses")
    }
    Label {
        text: Resales.statusText
        color: theme.secondaryText
        wrapMode: Text.WordWrap
        horizontalAlignment: Text.AlignHCenter
        Layout.fillWidth: true
        Accessible.name: text
    }
    Button {
        visible: Resales.canRetry
        text: qsTr("Retry")
        Layout.alignment: Qt.AlignHCenter
        Accessible.name: qsTr("Retry loading addresses")
        onClicked: Resales.retry()
    }
}
