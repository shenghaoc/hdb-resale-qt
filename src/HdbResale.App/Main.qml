import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtLocation
import QtPositioning

ApplicationWindow {
    id: window
    visible: true
    width: 1120
    height: 760
    minimumWidth: 820
    minimumHeight: 600
    title: "HDB Resale · Native fixture explorer"

    Loader {
        active: Resales.runtimeGate
        sourceComponent: Component {
            RuntimeGate {
                targetMap: map; targetList: transactionsList
                townControl: townPicker; priceControl: pricePicker
                zoomControl: zoomIn; recenterControl: recenter; attributionImage: oneMapLogo
            }
        }
    }

    Loader {
        active: Resales.scaleGate
        sourceComponent: Component { ScaleGate { targetMap: map; targetList: transactionsList; townControl: townPicker; priceControl: pricePicker; attributionImage: oneMapLogo } }
    }

    Loader {
        active: Resales.startupProbe
        sourceComponent: Component {
            Timer {
                interval: 25; repeat: true; running: true
                property double started: Date.now()
                onTriggered: {
                    const expected = Resales.startupView === "full" ? Resales.mappedCount : 0
                    const mapReady = Resales.startupView === "qml-shell" || map.mapReady
                    if (mapReady && map.mapItems.length === expected && transactionsList.count === Resales.visibleCount
                            && oneMapLogo.status === Image.Ready) {
                        stop(); Resales.startupReady(); Qt.quit()
                    } else if (Date.now() - started > 10000) {
                        console.error("HDB_STARTUP_TIMEOUT"); stop(); Qt.quit()
                    }
                }
            }
        }
    }

    Plugin {
        id: osm
        name: "osm"
        // OneMap's public 256px XYZ basemap. Qt appends %z/%x/%y.png to this prefix.
        // Search/geocoding evidence and authentication are separate and unchanged.
        PluginParameter { name: "osm.mapping.custom.host"; value: "https://www.onemap.gov.sg/maps/tiles/Default/" }
        PluginParameter { name: "osm.mapping.custom.datacopyright"; value: "Singapore Land Authority" }
        PluginParameter { name: "osm.mapping.custom.mapcopyright"; value: "OneMap" }
        PluginParameter { name: "osm.useragent"; value: "HdbResaleQt/0.1 (native fixture explorer)" }
        PluginParameter { name: "osm.mapping.providersrepository.disabled"; value: true }
        PluginParameter { name: "osm.mapping.prefetching_style"; value: "NoPrefetching" }
        PluginParameter { name: "osm.mapping.cache.directory"; value: Resales.basemapCacheDirectory }
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 12
        spacing: 10
        Label { text: "Singapore HDB resale explorer"; font.pixelSize: 24; font.bold: true }
        Label { text: "HDB / ACRA via data.gov.sg · corroborated addresses · approximate footprint points" }
        RowLayout {
            Label { text: "Town" }
            ComboBox {
                id: townPicker
                model: JSON.parse(Resales.townsJson)
                currentIndex: Resales.townIndex
                onActivated: Resales.setTown(currentText)
                Accessible.name: "Town filter"
            }
            Label { text: "Maximum price (S$)" }
            SpinBox {
                id: pricePicker
                from: 0; to: Resales.maximumAvailablePrice; stepSize: 50000
                value: Resales.maximumPrice
                editable: true
                onValueModified: Resales.setMaximumPrice(value)
                Accessible.name: "Maximum resale price"
            }
            Button { text: "Reset filters"; onClicked: Resales.resetFilters() }
            Item { Layout.fillWidth: true }
        }
        Label { text: Resales.filterSummary; font.bold: true }
        Label {
            text: Resales.importSummary + ' <a href="https://data.gov.sg/open-data-licence">Singapore Open Data Licence</a>'
            onLinkActivated: (link) => Qt.openUrlExternally(link)
        }
        Flickable {
            id: diagnosticScroll
            contentWidth: width
            contentHeight: diagnosticText.implicitHeight
            ScrollBar.vertical: ScrollBar {}
            visible: Resales.importDiagnostics.length > 0
            Layout.fillWidth: true
            Layout.preferredHeight: 72
            Layout.maximumHeight: 72
            clip: true
            Label { id: diagnosticText; width: diagnosticScroll.width; text: Resales.importDiagnostics; wrapMode: Text.WordWrap }
        }
        RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            Item {
                Layout.fillWidth: true
                Layout.fillHeight: true
                Layout.minimumWidth: 480
                Layout.preferredWidth: 800
                Map {
                    id: map
                    property int createdDelegates: 0
                    property int destroyedDelegates: 0
                    property double lastDelegateCreatedMs: 0
                    property double lastDelegateDestroyedMs: 0
                    property bool traceDelegates: Resales.scaleLifecycle
                    anchors.fill: parent
                    plugin: osm
                    activeMapType: supportedMapTypes[supportedMapTypes.length - 1]
                    center: QtPositioning.coordinate(1.3521, 103.8198)
                    zoomLevel: 11
                    minimumZoomLevel: 11
                    maximumZoomLevel: 19
                    // Exact official logo/text is provided by the always-visible overlay below.
                    copyrightsVisible: false
                    MapItemView {
                        model: Resales.startupProbe && Resales.startupView !== "full" ? null : Resales.mapPoints
                        delegate: MapQuickItem {
                            Component.onCompleted: if (map.traceDelegates) { map.createdDelegates++; map.lastDelegateCreatedMs = Date.now() }
                            Component.onDestruction: if (map.traceDelegates) { map.destroyedDelegates++; map.lastDelegateDestroyedMs = Date.now() }
                            required property string transactionId
                            required property double latitude
                            required property double longitude
                            required property string priceLabel
                            required property string address
                            required property string mapKey
                            required property int transactionCount
                            property bool selected: Resales.selectedMapKey === mapKey
                            z: selected ? 1 : 0
                            coordinate: QtPositioning.coordinate(latitude, longitude)
                            anchorPoint.x: pin.width / 2
                            anchorPoint.y: pin.height / 2
                            sourceItem: Rectangle {
                                id: pin
                                // Keep every address marker. Compact low-zoom pins reduce
                                // overlap; selection is always larger, labelled and raised.
                                width: selected ? 28 : map.zoomLevel < 13 ? 14 : 24
                                height: width; radius: width / 2
                                color: selected ? "#e35b19" : "#1565c0"
                                border.color: "white"; border.width: 2
                                Accessible.role: Accessible.Button
                                Accessible.name: address + ", " + priceLabel
                                Accessible.onPressAction: Resales.selectTransaction(transactionId)
                                Text { anchors.centerIn: parent; visible: selected || map.zoomLevel >= 13; text: transactionCount > 1 ? transactionCount : ""; color: "white"; font.pixelSize: 10 }
                                TapHandler { onTapped: Resales.selectTransaction(transactionId) }
                            }
                        }
                    }
                    DragHandler {
                        target: null
                        // Include the movement that crossed the drag threshold. This also
                        // supports a press/move/release delivered as a single move event.
                        onActiveChanged: if (active) map.pan(
                            centroid.pressPosition.x - centroid.position.x,
                            centroid.pressPosition.y - centroid.position.y)
                        onTranslationChanged: (delta) => map.pan(-delta.x, -delta.y)
                    }
                    PinchHandler {
                        id: pinch
                        target: null
                        onActiveChanged: if (active) map.startCentroid = map.toCoordinate(pinch.centroid.position, false)
                        onScaleChanged: (delta) => {
                            map.zoomLevel += Math.log2(delta)
                            map.alignCoordinateToPoint(map.startCentroid, pinch.centroid.position)
                        }
                    }
                    property var startCentroid
                    WheelHandler {
                        acceptedDevices: PointerDevice.Mouse | PointerDevice.TouchPad
                        onWheel: (event) => {
                            const point = Qt.point(event.x, event.y)
                            const coordinate = map.toCoordinate(point)
                            map.zoomLevel += event.angleDelta.y !== 0
                                ? event.angleDelta.y / 960 : event.pixelDelta.y / 240
                            map.alignCoordinateToPoint(coordinate, point)
                        }
                    }
                }
                Rectangle {
                    anchors.left: parent.left; anchors.bottom: parent.bottom
                    width: attributionRow.implicitWidth + 12; height: 30
                    color: "#f2ffffff"
                    Row {
                        id: attributionRow
                        anchors.centerIn: parent
                        spacing: 5
                        Image {
                            id: oneMapLogo
                            width: 24; height: 24; fillMode: Image.PreserveAspectFit
                            source: "qrc:/hdb-resale/onemap-logo.png"
                            Accessible.name: "OneMap logo"
                        }
                        Label {
                            anchors.verticalCenter: parent.verticalCenter
                            text: '<a href="https://www.onemap.gov.sg/">OneMap</a> © contributors | <a href="https://www.sla.gov.sg/">Singapore Land Authority</a>'
                            font.pixelSize: 11
                            onLinkActivated: (link) => Qt.openUrlExternally(link)
                        }
                    }
                }
                Row {
                    anchors.top: parent.top; anchors.right: parent.right; anchors.margins: 8; spacing: 6
                    Button { id: zoomIn; text: "+"; Accessible.name: "Zoom in"; onClicked: map.zoomLevel += 1 }
                    Button { text: "−"; Accessible.name: "Zoom out"; onClicked: map.zoomLevel -= 1 }
                    Button {
                        id: recenter
                        text: "Singapore"
                        onClicked: { map.center = QtPositioning.coordinate(1.3521, 103.8198); map.zoomLevel = 11 }
                    }
                }
                Label {
                    anchors.left: parent.left; anchors.top: parent.top; anchors.margins: 8
                    text: "Zoom " + map.zoomLevel.toFixed(1) + " · " + map.center.latitude.toFixed(4) + ", " + map.center.longitude.toFixed(4)
                    padding: 5
                    background: Rectangle { color: "white"; opacity: 0.85 }
                }
                Label {
                    anchors.centerIn: parent
                    visible: map.error !== Map.NoError
                    text: "Map error: " + map.errorString
                    padding: 12
                    background: Rectangle { color: "white" }
                }
            }
            ColumnLayout {
                Layout.minimumWidth: 280
                Layout.preferredWidth: 280
                Layout.maximumWidth: 280
                Layout.fillHeight: true
                Label { text: "Selection"; font.bold: true }
                Flickable {
                    id: selectionScroll
                    contentWidth: width
                    contentHeight: selectionText.implicitHeight
                    ScrollBar.vertical: ScrollBar {}
                    Layout.fillWidth: true
                    Layout.preferredHeight: 160
                    Layout.maximumHeight: 160
                    clip: true
                    Label { id: selectionText; width: selectionScroll.width; text: Resales.selectionDetails; wrapMode: Text.WordWrap }
                }
                Label { text: "Visible transactions"; font.bold: true }
                ListView {
                    id: transactionsList
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    clip: true
                    spacing: 4
                    model: Resales
                    delegate: ItemDelegate {
                        required property string transactionId
                        required property string address
                        required property string priceLabel
                        required property string townName
                        required property string locationLabel
                        width: ListView.view.width
                        text: address + "\n" + townName + " · " + priceLabel + "\n" + locationLabel
                        highlighted: Resales.selectedId === transactionId
                        onClicked: Resales.selectTransaction(transactionId)
                    }
                }
                Label { visible: Resales.visibleCount === 0; text: "No matching transactions." }
            }
        }
    }
}
