import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtLocation
import QtPositioning

ApplicationWindow {
    id: window
    visible: true
    width: 1360
    height: 900
    minimumWidth: 1120
    minimumHeight: 760
    title: "HDB Resale Explorer · 0.1.0 RC"

    // The pinned Bridge creates a parentless, JavaScript-owned model wrapper.
    // Keep its JS reference alive across QML GC, including all filter updates.
    readonly property var locatedMapModel: Resales.startupProbe && Resales.startupView !== "full" ? null : Resales.mapPoints

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
        active: Resales.scaleGate && !Resales.presentationGate && !Resales.buyerGate
        sourceComponent: Component { ScaleGate { targetMap: map; targetList: transactionsList; townControl: townPicker; priceControl: pricePicker; attributionImage: oneMapLogo } }
    }

    Loader {
        active: Resales.presentationGate
        sourceComponent: Component { PresentationGate { targetMap: map; targetList: transactionsList; townControl: townPicker; priceControl: pricePicker; attributionImage: oneMapLogo } }
    }

    Loader {
        active: Resales.buyerGate
        sourceComponent: Component { BuyerGate { targetMap: map; targetList: transactionsList; townControl: townPicker; typeControl: typePicker; minimumControl: minimumPicker; priceControl: pricePicker; recencyControl: recencyPicker; attributionImage: oneMapLogo; targetTrendLoader: trendLoader } }
    }

    Loader {
        active: Resales.startupProbe
        sourceComponent: Component {
            Timer {
                interval: 25; repeat: true; running: true
                property double started: Date.now()
                onTriggered: {
                    const expected = Resales.startupView === "full" ? Resales.presentationCount : 0
                    const mapReady = Resales.startupView === "qml-shell" || map.mapReady
                    if (mapReady && (Resales.startupView !== "full" || Resales.mapViewportReady) && map.mapItems.length === expected && transactionsList.count === Resales.addressCount
                            && oneMapLogo.status === Image.Ready) {
                        stop(); Resales.startupReady(); Qt.quit()
                    } else if (Date.now() - started > 10000) {
                        console.error("HDB_STARTUP_TIMEOUT"); stop(); Qt.quit()
                    }
                }
            }
        }
    }

    Timer {
        interval: 50; repeat: true; running: Resales.packageSmoke
        property int ticks: 0
        onTriggered: {
            ticks++
            if (Resales.selectedMapKey === "") Resales.selectAddress(Resales.firstAddressKey)
            if (map.mapReady && map.error === Map.NoError && transactionsList.count === Resales.addressCount && Resales.visibleCount === 6 && oneMapLogo.status === Image.Ready
                    && trendLoader.item && trendLoader.item.pointCount === 24 && trendLoader.item.pointsAgree()) {
                console.log("HDB_PACKAGE_SHELL")
                console.log("HDB_PACKAGE_DATA")
                console.log("HDB_PACKAGE_MAP_READY")
                console.log("HDB_PACKAGE_CHART_READY")
                stop(); packageExit.start()
            } else if (ticks > 100) { console.error("HDB_PACKAGE_FAIL readiness timeout"); stop(); Qt.quit() }
        }
    }
    Timer { id: packageExit; interval: 350; onTriggered: Qt.quit() }
    Plugin {
        id: osm
        name: "osm"
        // OneMap's public 256px XYZ basemap. Qt appends %z/%x/%y.png to this prefix.
        // Search/geocoding evidence and authentication are separate and unchanged.
        PluginParameter { name: "osm.mapping.custom.host"; value: "https://www.onemap.gov.sg/maps/tiles/Default/" }
        PluginParameter { name: "osm.mapping.custom.datacopyright"; value: "Singapore Land Authority" }
        PluginParameter { name: "osm.mapping.custom.mapcopyright"; value: "OneMap" }
        PluginParameter { name: "osm.useragent"; value: "HdbResaleExplorer/0.1.0 (independent resale research)" }
        PluginParameter { name: "osm.mapping.providersrepository.disabled"; value: true }
        PluginParameter { name: "osm.mapping.prefetching_style"; value: "NoPrefetching" }
        PluginParameter { name: "osm.mapping.cache.directory"; value: Resales.basemapCacheDirectory }
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 12
        spacing: 10
        RowLayout {
            Label { text: "HDB Resale Explorer"; font.pixelSize: 25; font.bold: true }
            Label { text: "0.1.0 RC"; color: "#536775" }
            Item { Layout.fillWidth: true }
            Button { text: "About"; Accessible.name: "About HDB Resale Explorer"; onClicked: aboutDialog.open() }
        }
        Label { text: "Explore historical resale records by address. Approximate block locations; not current listings."; color: "#455a64" }
        RowLayout {
            spacing: 12
            ColumnLayout {
                spacing: 2
                Label { text: "Town" }
                ComboBox {
                    id: townPicker; Layout.preferredWidth: 210
                    model: JSON.parse(Resales.townsJson); currentIndex: Resales.townIndex
                    onActivated: Resales.setTown(currentText); Accessible.name: "Town filter"
                }
            }
            ColumnLayout {
                spacing: 2
                Label { text: "Flat type" }
                ComboBox {
                    id: typePicker; Layout.preferredWidth: 180
                    model: JSON.parse(Resales.flatTypesJson); currentIndex: Resales.flatTypeIndex
                    onActivated: Resales.setFlatType(currentText); Accessible.name: "Flat type filter"
                }
            }
            ColumnLayout {
                spacing: 2
                Label { text: "Minimum price (S$)" }
                SpinBox {
                    id: minimumPicker; from: 0; to: Resales.maximumAvailablePrice; stepSize: 50000
                    value: Resales.minimumPrice; editable: true
                    onValueModified: Resales.setMinimumPrice(value); Accessible.name: "Minimum resale price"
                }
            }
            ColumnLayout {
                spacing: 2
                Label { text: "Maximum price (S$)" }
                SpinBox {
                    id: pricePicker; from: 0; to: Resales.maximumAvailablePrice; stepSize: 50000
                    value: Resales.maximumPrice; editable: true
                    onValueModified: Resales.setMaximumPrice(value); Accessible.name: "Maximum resale price"
                }
            }
            ColumnLayout {
                spacing: 2
                Label { text: "Registration window" }
                ComboBox {
                    id: recencyPicker; Layout.minimumWidth: 174; model: ["All months", "Latest 12 months", "Latest 24 months"]
                    currentIndex: Resales.recencyMonths === 12 ? 1 : Resales.recencyMonths === 24 ? 2 : 0
                    onActivated: Resales.setRecencyMonths(currentIndex === 1 ? 12 : currentIndex === 2 ? 24 : 0)
                    Accessible.name: "Registration month window"
                }
            }
            Button { text: "Reset"; Layout.alignment: Qt.AlignBottom; Accessible.name: "Reset all filters"; onClicked: Resales.resetFilters() }
            Item { Layout.fillWidth: true }
        }
        Label { text: "Inclusive price bounds · all statistics use matching transactions · time windows end at source month " + Resales.datasetLatestMonth + " (may be partial)"; font.pixelSize: 12; color: "#455a64" }
        Label { visible: Resales.minimumPrice > Resales.maximumPrice; text: "Minimum exceeds maximum. Adjust either bound to show results."; color: "#9b3b00" }
        Label { text: Resales.filterSummary; font.bold: true; Accessible.name: text }
        RowLayout {
            Label { text: Resales.presentationSummary; Layout.fillWidth: true; wrapMode: Text.WordWrap }
            Label { text: "Map groups count addresses. Individual pins count sales."; font.pixelSize: 12 }
        }
        Flickable {
            id: diagnosticScroll; contentWidth: width; contentHeight: diagnosticText.implicitHeight
            ScrollBar.vertical: ScrollBar {}
            visible: Resales.importDiagnostics.length > 0
            Layout.fillWidth: true; Layout.preferredHeight: 60; Layout.maximumHeight: 60; clip: true
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
                    property bool viewportPending: false
                    function flushViewport() {
                        viewportPending = false
                        if (mapReady) Resales.setMapViewport(center.latitude, center.longitude, zoomLevel, width, height)
                    }
                    function scheduleViewport() {
                        // At most one pending event-turn delivery. Read the latest
                        // camera at flush time; no debounce sleeps or lost final input.
                        if (!viewportPending) { viewportPending = true; Qt.callLater(flushViewport) }
                    }
                    onCenterChanged: scheduleViewport()
                    onZoomLevelChanged: scheduleViewport()
                    onWidthChanged: scheduleViewport()
                    onHeightChanged: scheduleViewport()
                    onMapReadyChanged: scheduleViewport()
                    Component.onCompleted: scheduleViewport()
                    // Exact official logo/text is provided by the always-visible overlay below.
                    copyrightsVisible: false
                    MapItemView {
                        // Public default incubation: viewport population is bounded.
                        // No undocumented incubateDelegates setting is used.
                        model: window.locatedMapModel
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
                            required property int addressCount
                            property bool cluster: addressCount > 1
                            property bool selected: !cluster && Resales.selectedMapKey === mapKey
                            z: selected ? 1 : 0
                            coordinate: QtPositioning.coordinate(latitude, longitude)
                            anchorPoint.x: pin.width / 2
                            anchorPoint.y: pin.height / 2
                            sourceItem: Rectangle {
                                id: pin
                                // Groups count addresses; individual pins count transactions.
                                // C# owns grouping and exact viewport membership.
                                width: selected ? 28 : cluster ? 38 : 24
                                height: width; radius: width / 2
                                color: selected ? "#e35b19" : cluster ? "#17574f" : "#1565c0"
                                border.color: "white"; border.width: 2
                                Accessible.role: Accessible.Button
                                Accessible.name: address + ", " + priceLabel
                                Accessible.onPressAction: pin.activateMarker()
                                Text { anchors.centerIn: parent; text: cluster ? addressCount : transactionCount > 1 ? transactionCount : ""; color: "white"; font.pixelSize: 10 }
                                function activateMarker() {
                                    if (cluster) { map.center = QtPositioning.coordinate(latitude, longitude); map.zoomLevel = Math.min(19, map.zoomLevel + 2) }
                                    else Resales.selectAddress(mapKey)
                                }
                                TapHandler { onTapped: pin.activateMarker() }
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
                Layout.minimumWidth: 410; Layout.preferredWidth: 410; Layout.maximumWidth: 410
                Layout.fillHeight: true; spacing: 6
                RowLayout {
                    Label { text: "Addresses"; font.bold: true; font.pixelSize: 17 }
                    Item { Layout.fillWidth: true }
                    Label { text: "↑ ↓ navigate · Enter select"; font.pixelSize: 11; color: "#455a64" }
                }
                ListView {
                    id: transactionsList
                    Layout.fillWidth: true; Layout.preferredHeight: Resales.selectedMapKey !== "" ? 140 : 220; Layout.minimumHeight: 120
                    clip: true; spacing: 3; model: Resales
                    activeFocusOnTab: true; keyNavigationEnabled: true
                    currentIndex: -1
                    Connections {
                        target: Resales
                        function onSelectedAddressIndexChanged() {
                            transactionsList.currentIndex = Resales.selectedAddressIndex
                            if (transactionsList.currentIndex >= 0)
                                transactionsList.positionViewAtIndex(transactionsList.currentIndex, ListView.Contain)
                        }
                    }
                    Accessible.name: "Matching address results"
                    ScrollBar.vertical: ScrollBar {}
                    Keys.onDownPressed: (event) => {
                        if (count > 0) { currentIndex = Math.min(count - 1, currentIndex + 1); positionViewAtIndex(currentIndex, ListView.Contain) }
                        event.accepted = true
                    }
                    Keys.onUpPressed: (event) => {
                        if (count > 0) { currentIndex = Math.max(0, currentIndex - 1); positionViewAtIndex(currentIndex, ListView.Contain) }
                        event.accepted = true
                    }
                    Keys.onReturnPressed: Resales.selectAddressAt(currentIndex)
                    Keys.onEnterPressed: Resales.selectAddressAt(currentIndex)
                    Keys.onSpacePressed: Resales.selectAddressAt(currentIndex)
                    delegate: ItemDelegate {
                        required property int index
                        required property string addressKey
                        required property string transactionId
                        required property string address
                        required property string priceLabel
                        required property string townName
                        required property string locationLabel
                        required property string summaryLabel
                        width: ListView.view.width
                        text: address + "\n" + townName + " · " + summaryLabel
                        font.pixelSize: 12
                        highlighted: Resales.selectedMapKey === addressKey
                        Accessible.name: text + ". " + locationLabel
                        onClicked: { transactionsList.forceActiveFocus(); Resales.selectAddress(addressKey) }
                        Rectangle {
                            anchors.fill: parent; color: "transparent"; radius: 3
                            border.width: 2; border.color: "#1565c0"
                            visible: transactionsList.activeFocus && transactionsList.currentIndex === index
                        }
                    }
                }
                Label { visible: Resales.addressCount === 0; text: "No matching addresses. Adjust the filters or reset."; wrapMode: Text.WordWrap; Layout.fillWidth: true }
                Rectangle { Layout.fillWidth: true; height: 1; color: "#ccd5da" }
                Label { text: Resales.selectedHeading; font.bold: true; font.pixelSize: 17; wrapMode: Text.WordWrap; Layout.fillWidth: true }
                Label { text: Resales.selectionMapStatus; font.pixelSize: 12; wrapMode: Text.WordWrap; Layout.fillWidth: true }
                Button { text: "Show selected address"; visible: Resales.selectedLocated; Accessible.name: text
                    onClicked: { map.center = QtPositioning.coordinate(Resales.selectedLatitude, Resales.selectedLongitude); map.zoomLevel = 16 } }
                ScrollView {
                    id: detailsScroll; Layout.fillWidth: true; Layout.fillHeight: true; clip: true
                    contentWidth: availableWidth; activeFocusOnTab: true; Accessible.name: "Selected address details and recent transactions"
                    Column {
                        width: detailsScroll.availableWidth; spacing: 8
                        Label { width: parent.width; text: Resales.selectedMetrics; wrapMode: Text.WordWrap; font.pixelSize: 13 }
                        Label { width: parent.width; text: Resales.selectedLease; wrapMode: Text.WordWrap; font.pixelSize: 12; color: "#455a64" }
                        Loader {
                            id: trendLoader; width: parent.width
                            active: Resales.selectedMapKey !== ""
                            sourceComponent: Component { BuyerTrendChart {} }
                        }
                        Label { width: parent.width; text: "Recent matching transactions (up to 15)"; visible: Resales.selectedMapKey !== ""; font.bold: true }
                        Repeater {
                            model: JSON.parse(Resales.recentTransactionsJson)
                            delegate: Column {
                                required property var modelData
                                width: detailsScroll.availableWidth; spacing: 3
                                Label { width: parent.width; text: modelData.heading; font.bold: true; wrapMode: Text.WordWrap; font.pixelSize: 12 }
                                Label { width: parent.width; text: modelData.details; wrapMode: Text.WordWrap; font.pixelSize: 12 }
                                Rectangle { width: parent.width; height: 1; color: "#e3e8eb" }
                            }
                        }
                        Label { width: parent.width; text: "Address evidence"; visible: Resales.selectedMapKey !== ""; font.bold: true }
                        Label { width: parent.width; text: Resales.selectedEvidence; wrapMode: Text.WordWrap; font.pixelSize: 11 }
                    }
                }
            }
        }
        Label {
            text: 'Independent research tool · HDB / ACRA via data.gov.sg · <a href="https://data.gov.sg/open-data-licence">Singapore Open Data Licence</a>'
            font.pixelSize: 11; onLinkActivated: (link) => Qt.openUrlExternally(link)
        }
    }
    Dialog {
        id: aboutDialog; title: "About HDB Resale Explorer"; modal: true; anchors.centerIn: parent
        width: 590; standardButtons: Dialog.Close
        contentItem: Column {
            spacing: 12
            Label { width: parent.width; text: Resales.aboutText; wrapMode: Text.WordWrap }
            Label { width: parent.width; text: Resales.importSummary; wrapMode: Text.WordWrap }
            Label { text: '<a href="https://github.com/shenghaoc/hdb-resale-qt">Source and licence notices</a>'; onLinkActivated: (link) => Qt.openUrlExternally(link) }
        }
    }
}
