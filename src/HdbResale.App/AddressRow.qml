import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// One address in the results list. Hierarchy: address and median price first, then
// town and flat types, then sales count/recency and how reliably it is placed.
// Selection is shown by a tinted fill AND a leading bar AND bold type (not colour
// alone); keyboard focus by a separate ring that keeps contrast on either fill.
ItemDelegate {
    id: root
    required property int index
    required property string addressKey
    required property string transactionId
    required property string address
    required property string priceLabel
    required property string townName
    required property string locationLabel
    required property string summaryLabel
    required property int saleCount
    required property string flatTypes
    required property string latestMonth
    required property string locationState
    required property string locationShort
    required property var theme
    readonly property bool current: ListView.isCurrentItem && ListView.view.activeFocus
    // Overridable so a gate can substitute a very long address without changing the model.
    property string displayAddress: address
    readonly property alias addressLabel: addressText
    readonly property string salesText: saleCount === 1 ? qsTr("1 sale") : qsTr("%1 sales").arg(saleCount.toLocaleString())

    width: ListView.view.width
    leftPadding: theme.m + 4; rightPadding: theme.m; topPadding: theme.s; bottomPadding: theme.s
    highlighted: Resales.selectedMapKey === addressKey
    Accessible.role: Accessible.Button
    Accessible.name: displayAddress + ", " + townName + ", median " + priceLabel + ", " + salesText + ", latest " + latestMonth + ". " + locationShort
    Accessible.description: locationLabel
    Accessible.selected: highlighted
    Accessible.onPressAction: { ListView.view.forceActiveFocus(); Resales.selectAddress(addressKey) }
    onClicked: { ListView.view.forceActiveFocus(); Resales.selectAddress(addressKey) }

    background: Rectangle {
        color: root.highlighted ? theme.selectedFill : root.hovered ? theme.hoverFill : "transparent"
        Rectangle { visible: root.highlighted; width: 4; height: parent.height; color: theme.accentBar }
        Rectangle {
            anchors.fill: parent; anchors.margins: 1; color: "transparent"; radius: 2
            border.width: 2; border.color: theme.text; visible: root.current
        }
        Rectangle { anchors.bottom: parent.bottom; width: parent.width; height: 1; color: theme.separator; visible: !root.highlighted && !root.current }
    }
    contentItem: GridLayout {
        columns: 2; columnSpacing: theme.m; rowSpacing: 2
        Label {
            Accessible.ignored: true
            id: addressText
            text: root.displayAddress; font.bold: true; wrapMode: Text.WordWrap
            Layout.fillWidth: true; Layout.minimumWidth: 0
        }
        Label {
            Accessible.ignored: true
            text: root.priceLabel; font.bold: true; Layout.alignment: Qt.AlignRight | Qt.AlignTop
            font.features: { "tnum": 1 }
        }
        Label {
            Accessible.ignored: true
            text: root.townName + " · " + root.flatTypes
            color: theme.secondaryText; elide: Text.ElideRight; Layout.fillWidth: true; Layout.columnSpan: 2
            font.pointSize: root.font.pointSize * theme.captionScale
        }
        Label {
            Accessible.ignored: true
            text: root.salesText + " · latest " + root.latestMonth
            color: theme.secondaryText; font.pointSize: root.font.pointSize * theme.captionScale
            Layout.fillWidth: true; elide: Text.ElideRight
        }
        Label {
            Accessible.ignored: true
            text: root.locationShort
            font.pointSize: root.font.pointSize * theme.captionScale
            font.italic: root.locationState !== "approximate"
            color: root.locationState === "approximate" ? theme.secondaryText : theme.text
            Layout.alignment: Qt.AlignRight; Layout.maximumWidth: root.width * 0.5; elide: Text.ElideRight
        }
    }
}
