import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

// Filter surface. Wide windows show every control in one wrapping row; compact windows show a summary
// row with a disclosure button that expands the same controls in place, so nothing is removed and
// keyboard order is unchanged. Filtering itself is unchanged: the same Resales calls and defaults.
ToolBar {
    id: root
    readonly property var theme: Window.window.theme
    readonly property bool compact: Window.window.compact
    property bool expanded: false
    readonly property bool showFields: !compact || expanded
    readonly property alias townPicker: townCombo
    readonly property alias typePicker: typeCombo
    readonly property alias minimumPicker: minimumSpin
    readonly property alias pricePicker: maximumSpin
    readonly property alias recencyPicker: recencyCombo
    readonly property alias resetButton: resetButton
    FontMetrics { id: captionMetrics; font.pointSize: root.font.pointSize * root.theme.captionScale }
    readonly property var recencyLabels: [qsTr("Any time"), qsTr("Latest 12 months"), qsTr("Latest 24 months")]
    readonly property bool priceInvalid: Resales.minimumPrice > Resales.maximumPrice
    // Counts the filters that differ from the defaults (every town and flat type, medians up to S$1,000,000).
    readonly property int activeCount: (Resales.townIndex > 0 ? 1 : 0) + (Resales.flatTypeIndex > 0 ? 1 : 0)
        + (Resales.minimumPrice > 0 || Resales.maximumPrice !== 1000000 ? 1 : 0) + (Resales.recencyMonths > 0 ? 1 : 0)
    readonly property string summary: {
        if (activeCount === 0) return qsTr("No filters · every town and flat type, medians up to S$1,000,000")
        const parts = []
        if (Resales.townIndex > 0) parts.push(townCombo.currentText)
        if (Resales.flatTypeIndex > 0) parts.push(typeCombo.currentText)
        if (Resales.minimumPrice > 0 || Resales.maximumPrice !== 1000000)
            parts.push("S$" + Resales.minimumPrice.toLocaleString(Qt.locale(), "f", 0) + " – " + Resales.maximumPrice.toLocaleString(Qt.locale(), "f", 0))
        if (Resales.recencyMonths > 0) parts.push(recencyLabels[Resales.recencyMonths === 12 ? 1 : 2])
        return parts.join(" · ")
    }
    leftPadding: theme.m; rightPadding: theme.m; topPadding: theme.s; bottomPadding: theme.s
    contentItem: ColumnLayout {
        spacing: theme.s
        RowLayout {
            visible: root.compact
            Layout.fillWidth: true
            spacing: theme.s
            Button {
                text: root.activeCount > 0 ? qsTr("Filters (%1)").arg(root.activeCount) : qsTr("Filters")
                checkable: true; checked: root.expanded
                onToggled: root.expanded = checked
                Accessible.name: qsTr("Filters, %1 active, %2").arg(root.activeCount).arg(root.expanded ? qsTr("expanded") : qsTr("collapsed"))
            }
            Label {
                text: root.summary; elide: Text.ElideRight; Layout.fillWidth: true; Layout.minimumWidth: 0
                color: theme.secondaryText; Accessible.ignored: true
            }
            Button {
                text: qsTr("Reset"); enabled: root.activeCount > 0
                onClicked: Resales.resetFilters(); Accessible.name: qsTr("Reset all filters")
            }
        }
        RowLayout {
            visible: root.showFields
            Layout.fillWidth: true
            spacing: theme.m
            Flow {
                Layout.fillWidth: true
                Layout.alignment: Qt.AlignTop
                spacing: theme.m
                FilterField {
                    caption: qsTr("Town")
                    ComboBox {
                        id: townCombo; Layout.preferredWidth: 12 * theme.unit; Layout.fillWidth: true
                        model: JSON.parse(Resales.townsJson); currentIndex: Resales.townIndex
                        onActivated: Resales.setTown(currentText); Accessible.name: "Town filter"
                    }
                }
                FilterField {
                    caption: qsTr("Flat type")
                    ComboBox {
                        id: typeCombo; Layout.preferredWidth: 10 * theme.unit; Layout.fillWidth: true
                        model: JSON.parse(Resales.flatTypesJson); currentIndex: Resales.flatTypeIndex
                        onActivated: Resales.setFlatType(currentText); Accessible.name: "Flat type filter"
                    }
                }
                FilterField {
                    caption: qsTr("Median price (S$)")
                    problem: root.priceInvalid ? qsTr("Minimum is above maximum, so nothing matches.") : ""
                    SpinBox {
                        id: minimumSpin; Layout.preferredWidth: 8.5 * theme.unit; Layout.fillWidth: true
                        from: 0; to: Resales.maximumAvailablePrice; stepSize: 50000; value: Resales.minimumPrice; editable: true
                        onValueModified: Resales.setMinimumPrice(value); Accessible.name: "Minimum median resale price"
                    }
                    Label { text: "–"; color: theme.secondaryText; Accessible.ignored: true }
                    SpinBox {
                        id: maximumSpin; Layout.preferredWidth: 8.5 * theme.unit; Layout.fillWidth: true
                        from: 0; to: Resales.maximumAvailablePrice; stepSize: 50000; value: Resales.maximumPrice; editable: true
                        onValueModified: Resales.setMaximumPrice(value); Accessible.name: "Maximum median resale price"
                    }
                }
                FilterField {
                    caption: qsTr("Latest registration")
                    ComboBox {
                        id: recencyCombo; Layout.preferredWidth: 10 * theme.unit; Layout.fillWidth: true
                        model: root.recencyLabels
                        currentIndex: Resales.recencyMonths === 12 ? 1 : Resales.recencyMonths === 24 ? 2 : 0
                        onActivated: Resales.setRecencyMonths(currentIndex === 1 ? 12 : currentIndex === 2 ? 24 : 0)
                        Accessible.name: "Latest registration window"
                    }
                }
            }
            Button {
                id: resetButton
                visible: !root.compact       // compact windows keep Reset in the summary row
                Layout.alignment: Qt.AlignTop
                Layout.topMargin: captionMetrics.height + theme.xs   // aligns with the controls, not their captions
                enabled: root.activeCount > 0
                text: root.activeCount > 0 ? qsTr("Reset filters (%1)").arg(root.activeCount) : qsTr("Reset filters")
                onClicked: Resales.resetFilters()
                Accessible.name: qsTr("Reset all filters")
            }
        }
    }
}
