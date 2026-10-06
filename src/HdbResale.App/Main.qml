import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtCore
import QtLocation
import QtPositioning

ApplicationWindow {
    id: window
    visible: true
    width: 1360
    height: 900
    minimumWidth: 640
    minimumHeight: 600
    title: "HDB Resale Explorer · 0.1.0 RC"
    Component.onCompleted: {
        // Match the existing macOS bundle identity on portable Qt desktops.
        Qt.application.name = "io.github.shenghaoc.hdb-resale-qt"
        Qt.application.displayName = "HDB Resale Explorer"
        Qt.application.organization = "shenghaoc"
        Qt.application.domain = "io.github.shenghaoc"
    }

    readonly property real unit: textMetrics.height
    // A usable map (22 text heights) beside a usable inspector (24) plus margins.
    readonly property bool compact: width < 50 * unit
    readonly property alias theme: theme
    Theme { id: theme; unit: window.unit }
    property var dialogFocusItem
    property alias mapView: map
    property alias resultView: transactionsList
    readonly property var detailsView: details.scroll
    readonly property var trendLoader: details.trendLoader
    readonly property var detailsPane: details
    property alias filterBar: filterBar
    readonly property bool filtersActive: filterBar.activeCount > 0
    readonly property var townPicker: filterBar.townPicker
    readonly property var typePicker: filterBar.typePicker
    readonly property var minimumPicker: filterBar.minimumPicker
    readonly property var pricePicker: filterBar.pricePicker
    readonly property var recencyPicker: filterBar.recencyPicker
    property alias viewTabs: viewTabs
    property alias commands: commands
    property alias aboutPopup: aboutDialog
    property alias settingsPopup: settingsDialog
    property alias dataPopup: dataDialog
    property alias grabRoot: shellRoot
    readonly property var townControl: townPicker
    readonly property var typeControl: typePicker
    readonly property bool modalOpen: aboutDialog.visible || settingsDialog.visible || dataDialog.visible
    FontMetrics { id: textMetrics; font: window.font }
    Settings {
        id: preferences
        location: Resales.uiSettingsFile
        property bool showMapCoordinates: false
    }
    function showDialog(dialog) {
        dialogFocusItem = window.activeFocusItem
        dialog.open()
    }
    function restoreDialogFocus() {
        if (dialogFocusItem && dialogFocusItem.visible && dialogFocusItem.enabled)
            dialogFocusItem.forceActiveFocus(Qt.OtherFocusReason)
        else townPicker.forceActiveFocus(Qt.OtherFocusReason)
    }
    // Native menu dismissal can restore old focus after onTriggered returns.
    // Focus the newly visible pane on the following event turn.
    function focusMap() { viewTabs.currentIndex = 0; Qt.callLater(() => map.forceActiveFocus(Qt.ShortcutFocusReason)) }
    function focusResults() { viewTabs.currentIndex = 1; Qt.callLater(() => transactionsList.forceActiveFocus(Qt.ShortcutFocusReason)) }
    function focusWithin(item) {
        for (let focused = window.activeFocusItem; focused; focused = focused.parent)
            if (focused === item) return true
        return false
    }
    onCompactChanged: {
        if (compact && focusWithin(addressesPane)) viewTabs.currentIndex = 1
    }
    DesktopActions {
        id: commands
        targetWindow: window; targetMap: map
        onResetRequested: Resales.resetFilters()
        onFiltersRequested: { filterBar.expanded = true; Qt.callLater(() => townPicker.forceActiveFocus(Qt.ShortcutFocusReason)) }
        onMapRequested: window.focusMap()
        onResultsRequested: window.focusResults()
        onSelectedRequested: {
            viewTabs.currentIndex = 0
            map.center = QtPositioning.coordinate(Resales.selectedLatitude, Resales.selectedLongitude)
            map.zoomLevel = 16
            window.focusMap()
        }
        onAboutRequested: window.showDialog(aboutDialog)
        onSettingsRequested: window.showDialog(settingsDialog)
    }
    Loader {
        active: Qt.platform.os === "osx"
        sourceComponent: Component { MacMenuBar { commands: window.commands } }
    }
    menuBar: Loader {
        active: Qt.platform.os !== "osx"
        height: item ? item.implicitHeight : 0
        sourceComponent: Component { MenuBar {
        Menu {
            title: qsTr("&File")
            MenuItem { action: commands.settings }
            MenuSeparator {}
            MenuItem { action: commands.quit }
        }
        Menu {
            title: qsTr("&View")
            MenuItem { action: commands.filters }
            MenuItem { action: commands.results }
            MenuItem { action: commands.map }
            MenuItem { action: commands.showSelected }
            MenuItem { action: commands.recenter }
            MenuSeparator {}
            MenuItem { action: commands.reset }
        }
        Menu { title: qsTr("&Help"); MenuItem { action: commands.about } }
        } }
    }
    Loader {
        active: Resales.screenshotDirectory !== ""
        sourceComponent: Component { ScreenshotGate { targetWindow: window } }
    }
    Loader {
        active: Resales.desktopUiGate
        sourceComponent: Component { DesktopUiGate { targetWindow: window; townControl: townPicker; typeControl: typePicker; minimumControl: minimumPicker; priceControl: pricePicker; recencyControl: recencyPicker } }
    }

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
        PluginParameter { name: "osm.mapping.custom.host"; value: Resales.syntheticBasemapHost !== "" ? Resales.syntheticBasemapHost : "https://www.onemap.gov.sg/maps/tiles/Default/" }
        PluginParameter { name: "osm.mapping.custom.datacopyright"; value: "Singapore Land Authority" }
        PluginParameter { name: "osm.mapping.custom.mapcopyright"; value: "OneMap" }
        PluginParameter { name: "osm.useragent"; value: "HdbResaleExplorer/0.1.0 (independent resale research)" }
        PluginParameter { name: "osm.mapping.providersrepository.disabled"; value: true }
        PluginParameter { name: "osm.mapping.prefetching_style"; value: "NoPrefetching" }
        PluginParameter { name: "osm.mapping.cache.directory"; value: Resales.basemapCacheDirectory }
    }

    Item {
    id: shellRoot
    anchors.fill: parent
    Rectangle { anchors.fill: parent; color: theme.chrome }
    ColumnLayout {
        id: shell
        anchors.fill: parent
        spacing: 0
        FilterBar { id: filterBar; Layout.fillWidth: true }
        Rectangle { Layout.fillWidth: true; Layout.preferredHeight: 1; color: theme.separator }
        TabBar {
            id: viewTabs; visible: window.compact; Layout.fillWidth: true
            TabButton { text: qsTr("Map") }
            TabButton { text: qsTr("Addresses and details") }
        }
        SplitView {
            id: workspace
            Layout.fillWidth: true
            Layout.fillHeight: true
            Item {
                visible: !window.compact || viewTabs.currentIndex === 0
                SplitView.fillWidth: true
                SplitView.minimumWidth: window.compact ? 0 : 24 * window.unit
                SplitView.preferredWidth: 800
                Map {
                    id: map
                    focusPolicy: Qt.StrongFocus
                    Accessible.role: Accessible.Pane
                    Accessible.name: qsTr("Resale map. Arrow keys pan, plus and minus zoom, Home returns to Singapore. Address results provide keyboard access to every address.")
                    Keys.onPressed: (event) => {
                        const shiftedPlus = event.key === Qt.Key_Plus && event.modifiers === Qt.ShiftModifier
                        if (!activeFocus || (event.modifiers !== Qt.NoModifier && !shiftedPlus)) return
                        if (event.key === Qt.Key_Left) pan(-80, 0)
                        else if (event.key === Qt.Key_Right) pan(80, 0)
                        else if (event.key === Qt.Key_Up) pan(0, -80)
                        else if (event.key === Qt.Key_Down) pan(0, 80)
                        else if (event.key === Qt.Key_Plus || event.key === Qt.Key_Equal) commands.zoomIn.trigger()
                        else if (event.key === Qt.Key_Minus) commands.zoomOut.trigger()
                        else if (event.key === Qt.Key_Home) commands.recenter.trigger()
                        else return
                        event.accepted = true
                    }
                    TapHandler { onTapped: map.forceActiveFocus(Qt.MouseFocusReason) }
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
                                width: (selected ? 28 : cluster ? 38 : 24) * Math.max(1, window.unit / 16)
                                height: width; radius: width / 2
                                color: selected ? "#e35b19" : cluster ? "#17574f" : "#1565c0"
                                border.color: "white"; border.width: 2
                                Accessible.role: Accessible.Button
                                Accessible.name: address + ", " + priceLabel
                                Accessible.onPressAction: pin.activateMarker()
                                Text { anchors.centerIn: parent; text: cluster ? addressCount : transactionCount > 1 ? transactionCount : ""; color: "white"; font.pointSize: window.font.pointSize * 0.85 }
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
                    id: attributionPanel
                    anchors.left: parent.left; anchors.bottom: parent.bottom; anchors.margins: theme.s
                    width: Math.min(parent.width - theme.s * 2, attributionRow.implicitWidth + 12); height: attributionRow.implicitHeight + 8
                    color: "#f2ffffff"; radius: theme.radius / 2; border.width: 1; border.color: theme.separator
                    RowLayout {
                        id: attributionRow
                        width: parent.width - 12
                        anchors.centerIn: parent
                        spacing: 5
                        Image {
                            id: oneMapLogo
                            Layout.preferredWidth: 24; Layout.preferredHeight: 24; sourceSize: Qt.size(24, 24); fillMode: Image.PreserveAspectFit
                            source: "qrc:/hdb-resale/onemap-logo.png"
                            Accessible.name: "OneMap logo"
                        }
                        Label {
                            Layout.fillWidth: true; wrapMode: Text.WordWrap
                            text: '<a href="https://www.onemap.gov.sg/">OneMap</a> © contributors | <a href="https://www.sla.gov.sg/">Singapore Land Authority</a>'
                            color: "#111111"; linkColor: "#0645ad"
                            onLinkActivated: (link) => Qt.openUrlExternally(link)
                        }
                    }
                }
                MapOverlay {
                    anchors.top: parent.top; anchors.right: parent.right; anchors.margins: theme.s
                    implicitWidth: mapControls.implicitWidth + theme.xs * 2
                    implicitHeight: mapControls.implicitHeight + theme.xs * 2
                    width: implicitWidth; height: implicitHeight
                    Column {
                        id: mapControls
                        anchors.centerIn: parent
                        spacing: 0
                        ToolButton { id: zoomIn; action: commands.zoomIn; text: "+"; width: theme.target; height: theme.target; Accessible.name: qsTr("Zoom in"); ToolTip.visible: hovered; ToolTip.text: qsTr("Zoom in"); ToolTip.delay: 600 }
                        ToolButton { action: commands.zoomOut; text: "−"; width: theme.target; height: theme.target; Accessible.name: qsTr("Zoom out"); ToolTip.visible: hovered; ToolTip.text: qsTr("Zoom out"); ToolTip.delay: 600 }
                        ToolButton { id: recenter; action: commands.recenter; text: qsTr("SG"); width: theme.target; height: theme.target; Accessible.name: qsTr("Return to Singapore view"); ToolTip.visible: hovered; ToolTip.text: qsTr("Return to Singapore"); ToolTip.delay: 600 }
                    }
                }
                MapOverlay {
                    anchors.left: parent.left; anchors.top: parent.top; anchors.margins: theme.s
                    visible: preferences.showMapCoordinates
                    width: coordinateLabel.implicitWidth + theme.m; height: coordinateLabel.implicitHeight + theme.s
                    Label {
                        id: coordinateLabel; anchors.centerIn: parent
                        text: "Zoom " + map.zoomLevel.toFixed(1) + " · " + map.center.latitude.toFixed(4) + ", " + map.center.longitude.toFixed(4)
                        font.pointSize: window.font.pointSize * theme.captionScale
                    }
                }
                MapOverlay {
                    anchors.left: parent.left; anchors.bottom: attributionPanel.top; anchors.margins: theme.s
                    width: Math.min(30 * window.unit, parent.width - theme.s * 2)
                    height: summaryColumn.implicitHeight + theme.s * 2
                    Column {
                        id: summaryColumn
                        x: theme.m; y: theme.s; width: parent.width - theme.m * 2
                        Label { width: parent.width; wrapMode: Text.WordWrap; text: Resales.presentationSummary; font.pointSize: window.font.pointSize * theme.captionScale }
                        Label { width: parent.width; wrapMode: Text.WordWrap; text: qsTr("Groups count addresses · pins count matching sales"); font.pointSize: window.font.pointSize * theme.captionScale; color: theme.secondaryText }
                    }
                }
                Label {
                    visible: Resales.syntheticBasemapHost !== ""
                    anchors.horizontalCenter: parent.horizontalCenter; anchors.top: parent.top; anchors.margins: theme.s
                    text: qsTr("SYNTHETIC BASEMAP · UI EVIDENCE ONLY")
                    font.pointSize: window.font.pointSize * theme.captionScale; font.bold: true; padding: theme.xs
                    color: "#7a1f00"; background: Rectangle { color: "#ffe9d6"; radius: 3 }
                    Accessible.ignored: true
                }
                Rectangle {
                    anchors.fill: parent; color: "transparent"
                    border.width: 2; border.color: window.palette.highlight
                    visible: map.activeFocus; Accessible.ignored: true
                }
                Label {
                    anchors.centerIn: parent
                    visible: !map.mapReady || map.error !== Map.NoError
                    text: map.error !== Map.NoError ? qsTr("Map error: %1").arg(map.errorString) : qsTr("Preparing map…")
                    width: Math.min(implicitWidth, parent.width - 24); wrapMode: Text.WordWrap
                    padding: theme.m; color: window.palette.windowText
                    background: MapOverlay {}
                }
            }
            Pane {
                id: addressesPane
                padding: 0
                background: Rectangle { color: theme.panel }
                visible: !window.compact || viewTabs.currentIndex === 1
                SplitView.fillWidth: window.compact
                SplitView.minimumWidth: window.compact ? 0 : 24 * window.unit
                SplitView.preferredWidth: 30 * window.unit
                contentItem: ColumnLayout {
                spacing: 0
                Section {
                    Layout.fillWidth: true
                    title: qsTr("Addresses")
                    caption: Resales.addressCount.toLocaleString()
                }
                SplitView {
                    id: paneSplit
                    orientation: Qt.Vertical
                    Layout.fillWidth: true; Layout.fillHeight: true
                    Item {
                        id: listArea
                        SplitView.fillHeight: true
                        SplitView.minimumHeight: 7 * window.unit
                ListView {
                    id: transactionsList
                    anchors.fill: parent
                    clip: true; spacing: 0; model: Resales
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
                    delegate: AddressRow { theme: window.theme }
                }
                        ColumnLayout {
                            visible: Resales.addressCount === 0
                            anchors.centerIn: parent
                            width: Math.min(parent.width - theme.l * 2, 26 * window.unit)
                            spacing: theme.s
                            Label { text: qsTr("No matching addresses"); font.bold: true; font.pointSize: window.font.pointSize * theme.titleScale; Layout.fillWidth: true; horizontalAlignment: Text.AlignHCenter; wrapMode: Text.WordWrap }
                            Label { text: filterBar.priceInvalid ? qsTr("The minimum price is above the maximum. Adjust either price to see results.") : qsTr("No registrations match these filters. Widen the price range, choose another town or flat type, or reset the filters."); color: theme.secondaryText; Layout.fillWidth: true; horizontalAlignment: Text.AlignHCenter; wrapMode: Text.WordWrap }
                            Button { action: commands.reset; text: qsTr("Reset filters"); Layout.alignment: Qt.AlignHCenter; Accessible.name: qsTr("Reset all filters") }
                        }
                    }
                    DetailsPane {
                        id: details
                        theme: window.theme; basePoint: window.font.pointSize
                        visible: Resales.selectedMapKey !== ""
                        SplitView.preferredHeight: paneSplit.height * (window.compact ? 0.74 : 0.6)
                        SplitView.minimumHeight: 12 * window.unit
                        onBackRequested: { Resales.selectAddress(""); transactionsList.forceActiveFocus(Qt.ShortcutFocusReason) }
                    }
                }
                }
            }
        }
        Rectangle { Layout.fillWidth: true; Layout.preferredHeight: 1; color: theme.separator }
        ToolBar {
            id: statusBar
            Layout.fillWidth: true
            leftPadding: theme.m; rightPadding: theme.m; topPadding: theme.xs; bottomPadding: theme.xs
            contentItem: ColumnLayout {
                spacing: 0
                readonly property real caption: window.font.pointSize * theme.captionScale
                RowLayout {
                    Layout.fillWidth: true
                    spacing: theme.m
                    Label { id: summaryLabel; text: Resales.filterSummary; font.pointSize: parent.parent.caption; elide: Text.ElideRight; Layout.fillWidth: true; Layout.minimumWidth: 0; Accessible.name: text }
                    ToolButton {
                        text: qsTr("Data and import…"); font.pointSize: parent.parent.caption; flat: true
                        onClicked: window.showDialog(dataDialog); Accessible.name: text
                    }
                }
                Label {
                    Layout.fillWidth: true; wrapMode: Text.WordWrap
                    font.pointSize: parent.caption; color: theme.secondaryText; linkColor: window.palette.link
                    text: Resales.dataModeLabel + ' · Independent research tool · HDB / ACRA via data.gov.sg · <a href="https://data.gov.sg/open-data-licence">Singapore Open Data Licence</a>'
                    onLinkActivated: (link) => Qt.openUrlExternally(link)
                }
            }
        }
    }
    }
    Dialog {
        id: dataDialog; title: qsTr("Data and import"); modal: true; anchors.centerIn: parent
        width: Math.min(window.width - 32, 38 * window.unit); height: Math.min(implicitHeight, window.height - 40)
        standardButtons: Dialog.Close; onClosed: window.restoreDialogFocus()
        contentItem: ScrollView {
            id: dataScroll
            contentWidth: availableWidth; clip: true
            Column {
                width: dataScroll.availableWidth; spacing: 12
                Label { width: parent.width; text: Resales.dataModeLabel; wrapMode: Text.WordWrap; font.bold: true }
                Label { width: parent.width; text: Resales.importSummary; wrapMode: Text.WordWrap }
                Label { width: parent.width; text: qsTr("Inclusive price bounds. All statistics use matching transactions. Time windows end at source month %1 (may be partial). Approximate block coordinates are separate from address match quality. Unmapped addresses remain in the list.").arg(Resales.datasetLatestMonth); wrapMode: Text.WordWrap }
                Label { width: parent.width; text: qsTr("Historical registrations are not current listings, valuations or eligibility decisions."); wrapMode: Text.WordWrap }
            }
        }
    }
    Dialog {
        id: settingsDialog; title: qsTr("Settings"); modal: true; anchors.centerIn: parent
        width: Math.min(window.width - 32, 32 * window.unit); standardButtons: Dialog.Close
        onClosed: window.restoreDialogFocus()
        contentItem: ColumnLayout {
            Label { text: qsTr("Appearance and text use your system preferences."); Layout.fillWidth: true; wrapMode: Text.WordWrap }
            CheckBox { text: qsTr("Show map coordinates and zoom"); checked: preferences.showMapCoordinates; onToggled: preferences.showMapCoordinates = checked; Accessible.name: text }
        }
    }
    Dialog {
        id: aboutDialog; title: "About HDB Resale Explorer"; modal: true; anchors.centerIn: parent
        width: Math.min(window.width - 32, 38 * window.unit); height: Math.min(implicitHeight, window.height - 40); standardButtons: Dialog.Close
        onClosed: window.restoreDialogFocus()
        contentItem: ScrollView {
            id: aboutScroll; contentWidth: availableWidth; clip: true
            Column {
            width: aboutScroll.availableWidth
            spacing: 12
            Label { width: parent.width; text: Resales.aboutText; wrapMode: Text.WordWrap }
            Label { width: parent.width; text: Resales.importSummary; wrapMode: Text.WordWrap }
            Label { text: '<a href="https://github.com/shenghaoc/hdb-resale-qt">Source and licence notices</a>'; linkColor: window.palette.link; onLinkActivated: (link) => Qt.openUrlExternally(link) }
            }
        }
    }
}
