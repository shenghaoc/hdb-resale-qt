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
    minimumWidth: 640
    minimumHeight: 600
    title: "HDB Resale Explorer"

    // One text height of the platform font: the unit for spacing and the compact breakpoint.
    readonly property real unit: textMetrics.height
    // A usable map (22 text heights) beside a usable inspector (22) plus margins; below that, tabs.
    readonly property bool compact: width < 50 * unit
    readonly property alias theme: theme
    FontMetrics { id: textMetrics; font: window.font }
    Theme { id: theme; unit: window.unit }

    // Commands shared by the macOS menu bar and the in-window controls.
    readonly property int activeFilterCount: filterBar.activeCount
    function showAbout() { aboutDialog.open() }
    function showView(index) {
        if (compact) viewTabs.currentIndex = index
        if (index === 1) transactionsList.forceActiveFocus(Qt.ShortcutFocusReason)
    }
    function showSelectedOnMap() {
        if (compact) viewTabs.currentIndex = 0
        map.center = QtPositioning.coordinate(Resales.selectedLatitude, Resales.selectedLongitude)
        map.zoomLevel = 16
    }
    function recenterMap() { map.center = QtPositioning.coordinate(1.3521, 103.8198); map.zoomLevel = 11 }
    function toggleZoom() { visibility = visibility === Window.Maximized ? Window.Windowed : Window.Maximized }
    // macOS has one menu bar per application; other desktops keep About in the status bar. Loaded by URL so
    // that other desktops never resolve Qt.labs.platform.
    Loader {
        Component.onCompleted: if (Qt.platform.os === "osx") setSource("MacMenuBar.qml", { appWindow: window })
    }
    // When a compact selection hides the focused list, the details' back button takes the keyboard focus.
    function keepFocusInCompactDetails() {
        const item = activeFocusItem
        if (compact && Resales.selectedMapKey !== "" && (!item || !item.visible || item === contentItem || item === contentItem.parent))
            detailsBack.forceActiveFocus(Qt.OtherFocusReason)
    }

    // WCAG relative luminance and contrast ratio, for text drawn on a palette colour.
    function luminance(c) {
        function linear(v) { return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4) }
        return 0.2126 * linear(c.r) + 0.7152 * linear(c.g) + 0.0722 * linear(c.b)
    }
    function contrastRatio(a, b) { const x = luminance(a), y = luminance(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05) }
    // A colour alpha-composited over an opaque one, as the scene graph draws it.
    function over(top, bottom) {
        return Qt.rgba(top.r * top.a + bottom.r * (1 - top.a), top.g * top.a + bottom.g * (1 - top.a),
                       top.b * top.a + bottom.b * (1 - top.a), 1)
    }
    // Text for a surface drawn over the window: the preferred palette colour where it reaches 4.5:1 as both
    // actually render (translucent colours composited), as macOS selections do; otherwise opaque black or white,
    // whichever reads better (one always reaches 4.58:1). Qt's generic palette pairs white with its #308cc6
    // highlight at only 3.69:1.
    function readableOn(surface, preferred) {
        const background = over(surface, over(window.color, Qt.rgba(1, 1, 1, 1)))
        if (contrastRatio(over(preferred, background), background) >= 4.5) return preferred
        const black = Qt.rgba(0, 0, 0, 1), white = Qt.rgba(1, 1, 1, 1)
        return contrastRatio(black, background) >= contrastRatio(white, background) ? black : white
    }

    // The pinned Bridge creates a parentless, JavaScript-owned model wrapper.
    // Keep its JS reference alive across QML GC, including all filter updates.
    readonly property var locatedMapModel: Resales.mapPoints

    // API responses arrive off the UI thread; apply them here while any request is outstanding.
    Timer { interval: 50; repeat: true; running: Resales.busy; onTriggered: Resales.pump() }

    Timer {
        interval: 50; repeat: true; running: Resales.packageSmoke
        property int ticks: 0
        onTriggered: {
            ticks++
            if (Resales.selectedMapKey === "" && Resales.addressCount > 0) Resales.selectAddress(Resales.firstAddressKey)
            if (map.mapReady && map.error === Map.NoError && Resales.addressCount > 0 && transactionsList.count === Resales.addressCount
                    && Resales.detailReady && oneMapLogo.status === Image.Ready && trendLoader.item && trendLoader.item.pointsAgree()) {
                console.log("HDB_PACKAGE_SHELL")
                console.log("HDB_PACKAGE_DATA")
                console.log("HDB_PACKAGE_MAP_READY")
                console.log("HDB_PACKAGE_CHART_READY")
                stop(); packageExit.start()
            } else if (ticks > 600) { console.error("HDB_PACKAGE_FAIL readiness timeout"); stop(); Qt.quit() }
        }
    }
    // Opt-in native regression checks over the recorded API; no screenshot or tile publication.
    Timer {
        interval: 50; repeat: true; running: Resales.apiGate.length > 0
        property int ticks: 0
        property bool configured: false
        function contrast(text, surface) {
            function luminance(c) {
                function linear(v) { return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4) }
                return 0.2126 * linear(c.r) + 0.7152 * linear(c.g) + 0.0722 * linear(c.b)
            }
            const a = luminance(text), b = luminance(surface)
            return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
        }
        function legible(text, surface) { return text.a === 1 && surface.a === 1 && contrast(text, surface) >= 4.5 }
        onTriggered: {
            ticks++
            const row = transactionsList.currentItem
            if (Resales.apiGate === "unreachable" && Resales.canRetry && !Resales.busy
                    && Resales.addressCount === 0 && !Resales.detailReady) {
                console.log("HDB_API_UNREACHABLE_PASS " + Resales.statusText)
                stop(); packageExit.start()
            } else if (Resales.apiGate === "tile-failure" && Resales.addressCount > 0) {
                // Every tile answers 503: the notice must appear without a map error, while the
                // API's addresses, selection and details stay usable and legible.
                if (Resales.selectedMapKey === "") Resales.selectAddress(Resales.firstAddressKey)
                else if (Resales.tileFailuresRepeated && tileFailureLabel.visible && map.error === Map.NoError
                        && Resales.detailReady && transactionsList.count === Resales.addressCount
                        && row && row.highlighted && !Qt.colorEqual(row.background.color, row.palette.base)
                        && legible(row.contentItem.color, row.background.color)
                        && legible(tileFailureLabel.color, tileFailureLabel.background.color)) {
                    console.log("HDB_API_TILE_NOTICE_PASS selection-contrast="
                        + contrast(row.contentItem.color, row.background.color).toFixed(2)
                        + " notice-contrast=" + contrast(tileFailureLabel.color, tileFailureLabel.background.color).toFixed(2))
                    stop(); packageExit.start()
                }
            } else if (Resales.apiGate === "high-zoom" && Resales.addressCount > 0 && map.mapReady) {
                if (!configured) {
                    configured = true
                    Resales.setTown("BEDOK")
                    Resales.setMinimumPrice(448444)
                    map.center = QtPositioning.coordinate(1.334, 103.929)
                    map.zoomLevel = 15
                } else if (map.zoomLevel === 15 && Resales.addressCount === 4
                        && transactionsList.count === 4 && Resales.inViewAddressCount === 4
                        && Resales.presentationCount === 4 && Resales.clusterCount === 0
                        && map.mapItems.length === 4 && oneMapLogo.status === Image.Ready) {
                    var keys = []
                    for (var i = 0; i < map.mapItems.length; i++) keys.push(map.mapItems[i].mapKey)
                    keys.sort()
                    var expected = ["bedok-115-bedok-nth-rd", "bedok-747a-bedok-reservoir-cres",
                        "bedok-748a-bedok-reservoir-cres", "bedok-748b-bedok-reservoir-cres"]
                    if (JSON.stringify(keys) === JSON.stringify(expected)) {
                        console.log("HDB_API_HIGH_ZOOM_PASS zoom=" + map.zoomLevel + " keys=" + JSON.stringify(keys))
                        stop(); packageExit.start()
                    }
                }
            }
            if (ticks > 600) { console.error("HDB_API_GATE_FAIL " + Resales.apiGate); stop(); Qt.quit() }
        }
    }
    Timer { id: packageExit; interval: 350; onTriggered: Qt.quit() }
    Timer { interval: 250; repeat: true; running: true; onTriggered: Resales.refreshTileStatus() }
    Plugin {
        id: osm
        name: "osm"
        // OneMap's public 256px XYZ basemap. Qt appends %z/%x/%y.png to this prefix.
        // Search/geocoding evidence and authentication are separate and unchanged.
        PluginParameter { name: "osm.mapping.custom.host"; value: Resales.basemapTileEndpoint }
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
        ColumnLayout {
            anchors.fill: parent
            spacing: 0
            FilterBar { id: filterBar; Layout.fillWidth: true }
            // Compact windows show the map or the addresses, switched under the filters.
            Item {
                visible: window.compact
                Layout.fillWidth: true
                implicitHeight: viewTabs.implicitHeight + theme.s * 2
                SegmentedControl {
                    id: viewTabs
                    anchors.centerIn: parent
                    model: [qsTr("Map"), qsTr("Addresses")]
                    Accessible.name: qsTr("View")
                }
            }
            Rectangle { Layout.fillWidth: true; Layout.preferredHeight: 1; color: theme.separator }
            SplitView {
                id: workspace
                Layout.fillWidth: true
                Layout.fillHeight: true
                Item {
                    id: mapArea
                    visible: !window.compact || viewTabs.currentIndex === 0
                    clip: true
                    SplitView.fillWidth: true
                    // Never narrower than the one-line OneMap and SLA attribution, which must stay visible.
                    SplitView.minimumWidth: window.compact ? 0 : Math.max(22 * window.unit, attributionSurface.width)
                    Map {
                        id: map
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
                                        else {
                                            Resales.selectAddress(mapKey)
                                            // Compact windows show the map or the details; a chosen address opens its details.
                                            if (window.compact) viewTabs.currentIndex = 1
                                        }
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
                        id: attributionSurface
                        anchors.left: parent.left; anchors.bottom: parent.bottom
                        width: attributionRow.implicitWidth + 12; height: 30
                        color: window.palette.window
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
                                id: attributionLabel
                                anchors.verticalCenter: parent.verticalCenter
                                color: palette.windowText; linkColor: palette.link
                                text: '<a href="https://www.onemap.gov.sg/">OneMap</a> © contributors | <a href="https://www.sla.gov.sg/">Singapore Land Authority</a>'
                                font.pixelSize: 11
                                onLinkActivated: (link) => Qt.openUrlExternally(link)
                            }
                        }
                    }
                    // What the map shows, kept with the map rather than in a header band.
                    Rectangle {
                        id: presentationOverlay
                        visible: Resales.addressCount > 0
                        anchors.left: parent.left; anchors.bottom: attributionSurface.top; anchors.margins: theme.s
                        width: presentationColumn.width + theme.m * 2
                        height: presentationColumn.implicitHeight + theme.s * 2
                        color: theme.overlay; border.color: theme.separator; radius: theme.xs
                        Column {
                            id: presentationColumn
                            anchors.left: parent.left; anchors.leftMargin: theme.m
                            anchors.verticalCenter: parent.verticalCenter
                            // The labels' natural single-line widths, wrapping only when the map is narrower.
                            width: Math.min(Math.max(presentationSummary.implicitWidth, presentationLegend.implicitWidth),
                                            mapArea.width - theme.s * 2 - theme.m * 2)
                            Label { id: presentationSummary; width: parent.width; text: Resales.presentationSummary; wrapMode: Text.WordWrap; font.pointSize: window.font.pointSize * theme.captionScale }
                            Label { id: presentationLegend; width: parent.width; text: qsTr("Groups count addresses · individual pins count sales"); wrapMode: Text.WordWrap; color: theme.secondaryText; font.pointSize: window.font.pointSize * theme.captionScale }
                        }
                    }
                    // Native map buttons may be translucent; keep a matching opaque surface behind them.
                    Rectangle { anchors.fill: mapControls; color: window.palette.window }
                    Row {
                        id: mapControls
                        anchors.top: parent.top; anchors.right: parent.right; anchors.margins: 8; spacing: 6
                        Button { id: zoomIn; text: "+"; Accessible.name: "Zoom in"; onClicked: map.zoomLevel += 1 }
                        Button { text: "−"; Accessible.name: "Zoom out"; onClicked: map.zoomLevel -= 1 }
                        Button {
                            id: recenter
                            text: "Singapore"
                            onClicked: window.recenterMap()
                        }
                    }
                    // Compact windows keep the list on the other view; show loading and errors over the map.
                    Rectangle {
                        visible: window.compact && (Resales.loading || Resales.canRetry)
                        anchors.centerIn: parent
                        width: Math.min(parent.width - theme.l * 2, 22 * window.unit)
                        height: compactLoadStatus.implicitHeight + theme.l * 2
                        color: theme.overlay; border.color: theme.separator; radius: theme.s
                        LoadStatus { id: compactLoadStatus; anchors.fill: parent; anchors.margins: theme.l }
                    }
                    Label {
                        id: zoomLabel; color: palette.windowText
                        anchors.left: parent.left; anchors.top: parent.top; anchors.margins: 8
                        text: "Zoom " + map.zoomLevel.toFixed(1) + " · " + map.center.latitude.toFixed(4) + ", " + map.center.longitude.toFixed(4)
                        padding: 5
                        background: Rectangle { color: window.palette.window }
                    }
                    Label {
                        id: tileFailureLabel
                        visible: Resales.tileFailuresRepeated
                        anchors.left: parent.left; anchors.right: parent.right
                        anchors.top: zoomLabel.bottom; anchors.margins: 8
                        text: "Some map tiles failed to load. Address results and details are not affected."
                        Accessible.name: text
                        color: palette.windowText; font.pixelSize: 12
                        wrapMode: Text.WordWrap; padding: 6
                        background: Rectangle { color: window.palette.window }
                    }
                    Label {
                        id: mapErrorLabel; color: palette.windowText
                        anchors.centerIn: parent
                        visible: map.error !== Map.NoError
                        text: "Map error: " + map.errorString
                        padding: 12
                        background: Rectangle { color: window.palette.window }
                    }
                }
                Pane {
                    id: inspector
                    padding: 0
                    visible: !window.compact || viewTabs.currentIndex === 1
                    SplitView.fillWidth: window.compact
                    SplitView.minimumWidth: window.compact ? 0 : 22 * window.unit
                    SplitView.preferredWidth: 26 * window.unit
                    contentItem: ColumnLayout {
                        spacing: 0
                        Section {
                            Layout.fillWidth: true
                            visible: !(window.compact && Resales.selectedMapKey !== "")
                            title: qsTr("Addresses")
                            // No count until the addresses have loaded.
                            caption: Resales.loading || Resales.canRetry ? "" : Resales.addressCount.toLocaleString(Qt.locale(), "f", 0)
                        }
                        SplitView {
                            id: paneSplit
                            orientation: Qt.Vertical
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            Item {
                                id: listArea
                                // Compact windows are master/detail: a selection gives the whole pane to its details,
                                // and "‹ All addresses" brings the list back. Wide windows show both, resizable.
                                visible: !(window.compact && Resales.selectedMapKey !== "")
                                SplitView.fillHeight: true
                                SplitView.minimumHeight: 7 * window.unit
                                ListView {
                                    id: transactionsList
                                    anchors.fill: parent; anchors.margins: theme.xs
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
                                        // A compact selection hides the list; keep the keyboard position by moving into the details.
                                        function onSelectedMapKeyChanged() {
                                            if (window.compact && Resales.selectedMapKey !== "") Qt.callLater(window.keepFocusInCompactDetails)
                                        }
                                    }
                                    Accessible.name: "Matching address results"
                                    Accessible.description: "Arrow keys move through the addresses; Enter selects one."
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
                                    id: addressDelegate
                                    required property int index
                                    required property string addressKey
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
                                    contentItem: Label {
                                        text: addressDelegate.text; font: addressDelegate.font
                                        color: window.readableOn(addressDelegate.background.color,
                                                addressDelegate.highlighted ? addressDelegate.palette.highlightedText : addressDelegate.palette.text)
                                    }
                                    background: Rectangle {
                                        readonly property color accent: addressDelegate.palette.highlight
                                        // Selection, with the style's hover and pressed feedback kept as tints of the row surface.
                                        color: addressDelegate.highlighted ? (addressDelegate.down ? Qt.darker(accent, 1.15) : accent)
                                            : addressDelegate.down ? Qt.tint(addressDelegate.palette.base, Qt.rgba(accent.r, accent.g, accent.b, 0.2))
                                            : addressDelegate.hovered ? Qt.tint(addressDelegate.palette.base, Qt.rgba(accent.r, accent.g, accent.b, 0.1))
                                            : addressDelegate.palette.base
                                        radius: 3
                                        border.width: transactionsList.activeFocus && transactionsList.currentIndex === index ? 2 : 0
                                        border.color: addressDelegate.highlighted ? addressDelegate.contentItem.color : addressDelegate.palette.highlight
                                    }
                                }
                                }
                                ColumnLayout {
                                    visible: Resales.addressCount === 0 && !Resales.loading && !Resales.canRetry
                                    anchors.centerIn: parent
                                    width: Math.min(parent.width - theme.l * 2, 26 * window.unit)
                                    spacing: theme.s
                                    Label { text: qsTr("No matching addresses"); font.bold: true; Layout.fillWidth: true; horizontalAlignment: Text.AlignHCenter; wrapMode: Text.WordWrap }
                                    Label {
                                        text: filterBar.priceInvalid ? qsTr("The minimum median is above the maximum. Adjust either bound to see results.")
                                                                      : qsTr("No address matches these filters. Widen the price range, choose another town or flat type, or reset the filters.")
                                        color: theme.secondaryText; Layout.fillWidth: true; horizontalAlignment: Text.AlignHCenter; wrapMode: Text.WordWrap
                                    }
                                    Button { text: qsTr("Reset filters"); Layout.alignment: Qt.AlignHCenter; onClicked: Resales.resetFilters(); Accessible.name: qsTr("Reset all filters") }
                                }
                                LoadStatus {
                                    anchors.centerIn: parent
                                    width: Math.min(parent.width - theme.l * 2, 22 * window.unit)
                                }
                            }
                            Item {
                                id: detailsPane
                                visible: Resales.selectedMapKey !== ""
                                SplitView.fillHeight: window.compact
                                SplitView.preferredHeight: paneSplit.height * 0.6
                                SplitView.minimumHeight: 12 * window.unit
                                Rectangle { anchors.top: parent.top; width: parent.width; height: 1; color: theme.separator }
                                ColumnLayout {
                                    anchors.fill: parent
                                    anchors.leftMargin: theme.m; anchors.rightMargin: theme.m; anchors.topMargin: theme.s
                                    spacing: theme.xs
                                    RowLayout {
                                        Layout.fillWidth: true
                                        Button {
                                            id: detailsBack
                                            text: qsTr("‹ All addresses"); flat: true
                                            Accessible.name: qsTr("Back to all addresses")
                                            onClicked: { Resales.selectAddress(""); transactionsList.forceActiveFocus(Qt.OtherFocusReason) }
                                        }
                                        Item { Layout.fillWidth: true }
                                        Button {
                                            text: qsTr("Show on map"); flat: true; visible: Resales.selectedLocated
                                            Accessible.name: qsTr("Show selected address on the map")
                                            onClicked: window.showSelectedOnMap()
                                        }
                                    }
                                    Label {
                                        text: Resales.selectedHeading; font.bold: true; font.pointSize: window.font.pointSize * theme.titleScale
                                        wrapMode: Text.WordWrap; Layout.fillWidth: true; Accessible.role: Accessible.Heading
                                    }
                                    Label {
                                        text: Resales.selectionMapStatus; color: theme.secondaryText
                                        font.pointSize: window.font.pointSize * theme.captionScale; wrapMode: Text.WordWrap; Layout.fillWidth: true
                                    }
                                    ScrollView {
                                        id: detailsScroll; Layout.fillWidth: true; Layout.fillHeight: true; clip: true
                                        contentWidth: availableWidth; activeFocusOnTab: true; Accessible.name: "Selected address details and recent transactions"
                                        function resetPosition() {
                                            contentItem.cancelFlick()
                                            contentItem.contentY = contentItem.originY
                                        }
                                        readonly property string selectionKey: Resales.selectedMapKey
                                        // Defer until the new detail text and column layout have updated.
                                        onSelectionKeyChanged: Qt.callLater(resetPosition)
                                        Column {
                                            width: detailsScroll.availableWidth; spacing: 8
                                            Label { width: parent.width; text: Resales.selectedMetrics; wrapMode: Text.WordWrap; font.pixelSize: 13 }
                                            Label { id: leaseLabel; width: parent.width; text: Resales.selectedLease; wrapMode: Text.WordWrap; font.pixelSize: 12; color: palette.windowText }
                                            Label { width: parent.width; text: Resales.detailStatus; visible: text.length > 0; wrapMode: Text.WordWrap; font.pixelSize: 12; font.italic: true }
                                            Button { text: "Retry registrations"; visible: Resales.canRetryDetail; onClicked: Resales.retryDetail() }
                                            Loader {
                                                id: trendLoader; width: parent.width
                                                active: Resales.detailReady
                                                sourceComponent: Component { BuyerTrendChart {} }
                                            }
                                            Label { width: parent.width; text: "Latest registrations at this address (up to 20, every flat type)"; visible: Resales.detailReady; font.bold: true; wrapMode: Text.WordWrap }
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
                                            Label { width: parent.width; text: "Location"; visible: Resales.selectedMapKey !== ""; font.bold: true }
                                            Label { width: parent.width; text: Resales.selectedLocation; wrapMode: Text.WordWrap; font.pixelSize: 11 }
                                        }
                                    }
                                }
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
                // One quiet line: the result count, provenance and the licence. The data service and the filter
                // rules are in About.
                contentItem: RowLayout {
                    spacing: theme.m
                    Label {
                        visible: text.length > 0
                        text: Resales.filterSummary; font.pointSize: window.font.pointSize * theme.captionScale; Accessible.name: text
                    }
                    // Compact windows keep the count and the licence; About has the rest.
                    Item { visible: window.compact; Layout.fillWidth: true }
                    Label {
                        visible: !window.compact
                        Layout.fillWidth: true; Layout.minimumWidth: 0; elide: Text.ElideRight; maximumLineCount: 1
                        font.pointSize: window.font.pointSize * theme.captionScale; color: theme.secondaryText
                        text: qsTr("Independent research tool · resale data from data.gov.sg")
                    }
                    Label {
                        font.pointSize: window.font.pointSize * theme.captionScale; linkColor: window.palette.link
                        text: '<a href="https://data.gov.sg/open-data-licence">Singapore Open Data Licence</a>'
                        onLinkActivated: (link) => Qt.openUrlExternally(link)
                    }
                    ToolButton {
                        visible: Qt.platform.os !== "osx"      // in the application menu on macOS
                        text: qsTr("About"); Accessible.name: "About HDB Resale Explorer"; onClicked: window.showAbout()
                    }
                }
            }
        }
    }
    Dialog {
        id: aboutDialog; title: "About HDB Resale Explorer"; modal: true; anchors.centerIn: parent
        width: Math.min(590, window.width - 32); standardButtons: Dialog.Close
        contentItem: Column {
            spacing: 12
            Label { width: parent.width; text: Resales.aboutText; wrapMode: Text.WordWrap }
            Label { width: parent.width; text: Resales.datasetSummary; wrapMode: Text.WordWrap }
            Label {
                width: parent.width; wrapMode: Text.WordWrap
                text: "How the filters work: price bounds apply to each address's median (the selected flat type's, when one is chosen); figures cover the latest 24 source months; registration windows keep addresses with a registration since their start and end at source month " + Resales.datasetLatestMonth + " (may be partial). Approximate block locations; not current listings."
            }
            Label { text: '<a href="https://github.com/shenghaoc/hdb-resale-qt">Source and licence notices</a>'; onLinkActivated: (link) => Qt.openUrlExternally(link) }
        }
    }
}
