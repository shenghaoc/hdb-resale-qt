import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// Selected-address inspector. Order answers the buyer's questions in turn: what is it,
// what do flats here typically cost, how sure are we where it is, how old is the lease,
// how have prices moved, and what exactly sold. Raw evidence is one disclosure away.
ColumnLayout {
    id: root
    required property var theme
    required property real basePoint     // the platform font size; the pane has no font of its own
    readonly property var facts: { try { return JSON.parse(Resales.selectedFactsJson) } catch (e) { return ({}) } }
    readonly property alias scroll: detailsScroll
    readonly property alias trendLoader: trendLoader
    readonly property alias backButton: backButton
    property bool evidenceShown: false
    signal backRequested()
    spacing: 0

    ColumnLayout {
        Layout.fillWidth: true
        Layout.margins: theme.m
        spacing: theme.xs
        RowLayout {
            Layout.fillWidth: true
            Button {
                id: backButton
                text: qsTr("‹ All addresses"); flat: true
                onClicked: root.backRequested()
                Accessible.name: qsTr("Back to all addresses")
            }
            Item { Layout.fillWidth: true }
            Button { action: Window.window.commands.showSelected; text: qsTr("Show on map"); Accessible.name: text }
        }
        Label {
            text: root.facts.address || Resales.selectedHeading
            font.bold: true; font.pointSize: root.basePoint * theme.titleScale
            wrapMode: Text.WordWrap; Layout.fillWidth: true; Accessible.role: Accessible.Heading
        }
        Label {
            text: (root.facts.town || "") + " · " + (root.facts.flatTypes || "")
            color: theme.secondaryText; Layout.fillWidth: true; wrapMode: Text.WordWrap
        }
        Label {
            text: root.facts.locationShort || ""
            font.bold: root.facts.locationState !== "approximate"; font.pointSize: root.basePoint * theme.captionScale
            Layout.fillWidth: true; wrapMode: Text.WordWrap
        }
    }
    Rectangle { Layout.fillWidth: true; Layout.preferredHeight: 1; color: theme.separator }

    ScrollView {
        id: detailsScroll
        Layout.fillWidth: true; Layout.fillHeight: true; clip: true
        leftPadding: theme.m; rightPadding: theme.m; topPadding: theme.m; bottomPadding: theme.m
        contentWidth: availableWidth; activeFocusOnTab: true
        Accessible.name: "Selected address details and recent transactions"
        Keys.onEscapePressed: root.backRequested()
        Column {
            width: detailsScroll.availableWidth
            spacing: theme.m

            Column {
                width: parent.width; spacing: 0
                Label { text: qsTr("Median price"); color: theme.secondaryText; font.pointSize: root.basePoint * theme.captionScale }
                Label { text: root.facts.median || ""; font.bold: true; font.pointSize: root.basePoint * 1.6; font.features: { "tnum": 1 } }
                Label { text: (root.facts.sales || "") + " · latest " + (root.facts.latest || ""); color: theme.secondaryText }
            }
            GridLayout {
                width: parent.width; columns: 2; columnSpacing: theme.m; rowSpacing: theme.xs
                Repeater {
                    model: [
                        { k: qsTr("Price range"), v: root.facts.range },
                        { k: qsTr("Per m²"), v: root.facts.perSqm },
                        { k: qsTr("Floor area"), v: root.facts.area },
                        { k: qsTr("Lease started"), v: root.facts.leaseYears }
                    ]
                    delegate: RowLayout {
                        required property var modelData
                        Layout.columnSpan: 2; Layout.fillWidth: true; spacing: theme.m
                        Label { text: modelData.k; color: theme.secondaryText; Layout.preferredWidth: 7 * theme.unit; Layout.alignment: Qt.AlignTop }
                        Label { text: modelData.v || ""; Layout.fillWidth: true; wrapMode: Text.WordWrap }
                    }
                }
            }

            Rectangle { width: parent.width; height: 1; color: theme.separator }
            Label { text: qsTr("Location"); font.bold: true; Accessible.role: Accessible.Heading }
            Label { width: parent.width; wrapMode: Text.WordWrap; text: root.facts.locationDetail || "" }
            Label { width: parent.width; wrapMode: Text.WordWrap; text: Resales.selectionMapStatus; color: theme.secondaryText }
            Button {
                text: root.evidenceShown ? qsTr("Hide technical evidence") : qsTr("Show technical evidence"); flat: true
                onClicked: root.evidenceShown = !root.evidenceShown
                Accessible.name: text
            }
            Label { visible: root.evidenceShown; width: parent.width; wrapMode: Text.WordWrap; text: Resales.selectedEvidence; color: theme.secondaryText }

            Rectangle { width: parent.width; height: 1; color: theme.separator }
            Label { text: qsTr("Remaining lease"); font.bold: true; Accessible.role: Accessible.Heading }
            Label { width: parent.width; wrapMode: Text.WordWrap; text: Resales.selectedLease }

            Rectangle { width: parent.width; height: 1; color: theme.separator }
            Loader {
                id: trendLoader; width: parent.width
                active: Resales.selectedMapKey !== ""
                sourceComponent: Component { BuyerTrendChart {} }
            }

            Rectangle { width: parent.width; height: 1; color: theme.separator }
            Label { text: qsTr("Recent matching transactions (up to 15)"); font.bold: true; Accessible.role: Accessible.Heading }
            Repeater {
                model: JSON.parse(Resales.recentTransactionsJson)
                delegate: Column {
                    required property var modelData
                    width: detailsScroll.availableWidth; spacing: theme.xs
                    Label { width: parent.width; text: modelData.heading; font.bold: true; wrapMode: Text.WordWrap }
                    Label { width: parent.width; text: modelData.details; wrapMode: Text.WordWrap; color: theme.secondaryText }
                }
            }
        }
    }
}
