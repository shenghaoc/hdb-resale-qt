import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtLocation
import QtPositioning

// Opt-in native buyer acceptance over the recorded Worker API (HDB_API_GATE=acceptance), run by
// tools/api_acceptance.py. Its oracle derives every expected state independently of this app's C#;
// this gate performs each step through the app's own model, list rows, map markers and buttons, then
// compares what the window shows. A step that does not match within its budget fails with the last
// difference observed.
Item {
    id: gate
    required property var targetMap
    required property var targetList
    required property var townControl
    required property var typeControl
    required property var minimumControl
    required property var priceControl
    required property var recencyControl
    required property var resetControl
    required property var retryControl
    required property var emptyNotice
    required property var attributionImage
    required property var targetTrendLoader
    required property var targetDetailsScroll
    required property var textSurfacePairs
    readonly property var plan: JSON.parse(Resales.apiGateExpectationJson)
    property int phase: 0
    property int cursor: 0
    property bool centered: false
    property bool scrolled: false
    property bool reentryArmed: false
    property var reentryInject: []
    property string difference: ""
    property double started: Date.now()
    visible: false

    Component.onCompleted: {
        // Create every row so each one is compared, and keep the details pane short so a changed
        // selection must return a genuinely scrolled pane to its top.
        targetList.cacheBuffer = 100000
        targetDetailsScroll.Layout.maximumHeight = 100
    }

    Connections {
        target: Resales
        function onModelAboutToBeReset() {
            if (!gate.reentryArmed) return
            gate.reentryArmed = false
            // Re-enter while the list model is resetting: the app must queue these intents in order.
            for (const action of gate.reentryInject) gate.perform(action)
            console.log("HDB_API_REENTRY injected=" + gate.reentryInject.length + " queued=" + Resales.gateMaximumQueuedMutations)
        }
    }

    function fail(reason) {
        console.error("HDB_API_GATE_FAIL acceptance step=" + plan.steps[phase].name + " " + reason)
        timer.stop()
        Qt.quit()
        return false
    }

    // Performs one action; false means it cannot happen yet and is retried on the next tick.
    function perform(action) {
        switch (action.do) {
        case "town": Resales.setTown(action.value); return true
        case "type": Resales.setFlatType(action.value); return true
        case "minimum": Resales.setMinimumPrice(action.value); return true
        case "maximum": Resales.setMaximumPrice(action.value); return true
        case "months": Resales.setRecencyMonths(action.value); return true
        case "reset": Resales.resetFilters(); return true
        case "select": Resales.selectAddress(action.key); return true
        case "select-index": Resales.selectAddressAt(action.index); return true
        case "reset-button": resetControl.clicked(); return true
        case "retry-button":
            if (!retryControl.visible) return fail("Retry registrations is not offered")
            retryControl.clicked()
            return true
        case "click-row": {
            const item = targetList.itemAtIndex(action.index)
            if (!item) return false
            if (item.addressKey !== action.key || item.highlighted)
                return fail("row " + action.index + " is not the unselected " + action.key)
            item.clicked()
            return true
        }
        case "marker": {
            if (!centered) {
                centered = true
                targetMap.zoomLevel = action.zoom
                targetMap.center = QtPositioning.coordinate(action.latitude, action.longitude)
                return false
            }
            for (const item of targetMap.mapItems)
                if (item.mapKey === action.key && item.addressCount === 1) { item.sourceItem.activateMarker(); return true }
            return false
        }
        case "reentry":
            reentryInject = action.inject
            reentryArmed = true
            return perform(action.trigger)
        case "viewport":
            for (const change of action.sequence) {
                if (change[0] === "zoom") targetMap.zoomLevel = change[1]
                else if (change[0] === "pan") targetMap.pan(change[1], change[2])
                else targetMap.center = QtPositioning.coordinate(change[1], change[2])
            }
            return true
        }
        return fail("unknown action " + action.do)
    }

    function equal(a, b) {
        if (typeof a === "number" && typeof b === "number") return Math.abs(a - b) < 1e-7
        if (a === null || b === null || typeof a !== "object" || typeof b !== "object") return a === b
        if (Array.isArray(a) !== Array.isArray(b) || Object.keys(a).length !== Object.keys(b).length) return false
        for (const key in b) if (!equal(a[key], b[key])) return false
        return true
    }
    function contrast(text, surface) {
        function luminance(c) {
            function linear(v) { return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear(c.r) + 0.7152 * linear(c.g) + 0.0722 * linear(c.b)
        }
        const a = luminance(text), b = luminance(surface)
        return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
    }
    function legible(pairs) {
        for (const pair of pairs) {
            const surface = pair[1].color !== undefined ? pair[1].color : pair[1].palette.window
            if (surface.a !== 1 || pair[0].color.a !== 1 || contrast(pair[0].color, surface) < 4.5) return false
        }
        return true
    }
    // Qt's own projection decides which listed addresses the map can show.
    function inView(row) {
        const point = targetMap.fromCoordinate(QtPositioning.coordinate(row.latitude, row.longitude), false)
        return point.x >= 0 && point.x <= targetMap.width && point.y >= 0 && point.y <= targetMap.height
    }
    function scrollDetailsToEnd() {
        const flickable = targetDetailsScroll.contentItem
        const bottom = Math.max(0, flickable.contentHeight - flickable.height)
        if (bottom <= 0) return false
        flickable.contentY = flickable.originY + bottom
        return !flickable.atYBeginning
    }

    function mismatch(e) {
        if (!targetMap.mapReady || targetMap.error !== Map.NoError) return "map not ready"
        if (targetMap.viewportPending || !Resales.mapViewportReady) return "viewport pending"
        if (attributionImage.status !== Image.Ready) return "OneMap logo not ready"
        if (Resales.loading || Resales.busy) return "requests outstanding"
        if (reentryArmed) return "reentrant intents not injected"
        if (e.camera && (targetMap.zoomLevel !== e.camera.zoom || Math.abs(targetMap.center.latitude - e.camera.latitude) > 1e-7
                || Math.abs(targetMap.center.longitude - e.camera.longitude) > 1e-7)) return "camera"
        const f = e.filters
        if (Resales.town !== f.town || townControl.currentText !== f.town) return "town shows " + townControl.currentText
        if (Resales.flatType !== f.type || typeControl.currentText !== f.type) return "flat type shows " + typeControl.currentText
        if (Resales.minimumPrice !== f.minimum || minimumControl.value !== f.minimum) return "minimum shows " + minimumControl.value
        if (Resales.maximumPrice !== f.maximum || priceControl.value !== f.maximum) return "maximum shows " + priceControl.value
        if (Resales.recencyMonths !== f.months || recencyControl.currentIndex !== (f.months === 12 ? 1 : f.months === 24 ? 2 : 0))
            return "window shows " + recencyControl.currentText
        if (Resales.datasetLatestMonth !== plan.latestMonth || Resales.maximumAvailablePrice !== plan.maximumAvailablePrice)
            return "dataset bounds"
        if (Resales.filterSummary !== e.summary) return "summary reads " + Resales.filterSummary
        // Every listed row, in order, as a buyer reads it.
        if (Resales.addressCount !== e.addresses.length || targetList.count !== e.addresses.length)
            return "list has " + targetList.count + " rows"
        if (emptyNotice.visible !== (e.addresses.length === 0)) return "empty-state notice"
        for (let i = 0; i < e.addresses.length; i++) {
            const item = targetList.itemAtIndex(i), row = e.addresses[i]
            if (!item) return "row " + i + " not created"
            if (item.addressKey !== row.key) return "row " + i + " is " + item.addressKey
            if (item.text !== row.text) return "row " + i + " reads " + JSON.stringify(item.text)
            if (item.highlighted !== (e.selected !== null && e.selected.key === row.key)) return "row " + i + " highlight"
        }
        const map = mapDifference(e)
        if (map) return map
        const selection = selectionDifference(e)
        if (selection) return selection
        if (e.detailsAtTop && !targetDetailsScroll.contentItem.atYBeginning) return "details kept their scrolled position"
        if (!legible(textSurfacePairs)) return "text below 4.5:1 contrast"
        return ""
    }

    function mapDifference(e) {
        if (Resales.mappedCount !== e.addresses.length) return "map holds " + Resales.mappedCount + " addresses"
        const shown = {}
        let count = 0
        for (const row of e.addresses) if (inView(row)) { shown[row.key] = row; count++ }
        if (Resales.inViewAddressCount !== count) return "map counts " + Resales.inViewAddressCount + " in view; Qt projects " + count
        if (targetMap.mapItems.length !== Resales.presentationCount) return "map delegates"
        const seen = {}
        let represented = 0
        for (const item of targetMap.mapItems) {
            if (seen[item.mapKey]) return "duplicate marker " + item.mapKey
            seen[item.mapKey] = true
            represented += item.addressCount
            if (item.addressCount === 1) {
                const row = shown[item.mapKey]
                if (!row) return "marker for an address not listed or out of view: " + item.mapKey
                if (item.address !== row.address || item.priceLabel !== row.mapLabel) return "marker " + item.mapKey + " reads " + item.priceLabel
                if (item.selected !== (e.selected !== null && e.selected.key === item.mapKey)) return "marker selection " + item.mapKey
            } else if (item.selected || targetMap.zoomLevel >= 15) {
                return "group " + item.mapKey
            }
        }
        if (represented !== count) return "markers represent " + represented + " of " + count + " addresses"
        if (e.selected !== null && shown[e.selected.key] && !seen[e.selected.key]) return "selected marker missing"
        const status = e.selected === null ? "" : shown[e.selected.key] ? "Selected address is highlighted on this map."
            : "Selected address is outside this map view."
        if (Resales.selectionMapStatus !== status) return "map status reads " + Resales.selectionMapStatus
        return ""
    }

    function selectionDifference(e) {
        const s = e.selected
        const recent = JSON.parse(Resales.recentTransactionsJson)
        if (s === null) {
            if (Resales.selectedMapKey !== "" || Resales.selectedAddressIndex !== -1 || targetList.currentIndex !== -1)
                return "selection not cleared"
            if (Resales.selectedHeading !== "Choose an address" || Resales.detailReady || targetTrendLoader.item
                    || recent.length !== 0 || retryControl.visible) return "details not cleared"
            return ""
        }
        if (Resales.selectedMapKey !== s.key) return "selected " + Resales.selectedMapKey
        if (Resales.selectedAddressIndex !== s.index || targetList.currentIndex !== s.index) return "selected row index"
        if (Resales.selectedHeading !== s.heading) return "heading reads " + Resales.selectedHeading
        if (Resales.selectedMetrics !== s.metrics) return "figures read " + JSON.stringify(Resales.selectedMetrics)
        if (Resales.selectedLease !== s.lease) return "lease reads " + Resales.selectedLease
        if (Resales.selectedLocation !== s.location) return "location reads " + Resales.selectedLocation
        if (Resales.detailStatus !== s.status) return "detail status reads " + Resales.detailStatus
        if (s.detail === "error")
            return Resales.detailReady || !retryControl.visible || targetTrendLoader.item || recent.length !== 0
                ? "refused details are not offered for retry" : ""
        if (!Resales.detailReady || retryControl.visible) return "details not loaded"
        if (recent.length !== s.recent.length) return recent.length + " registrations listed"
        for (let i = 0; i < recent.length; i++)
            if (recent[i].id !== s.recent[i].id || recent[i].heading !== s.recent[i].heading || recent[i].details !== s.recent[i].details)
                return "registration " + i + " reads " + JSON.stringify(recent[i])
        if (!equal(JSON.parse(Resales.trendJson), s.trend)) return "trend data"
        const chart = targetTrendLoader.item
        const plotted = s.trend.ObservedMonths > 0 ? s.trend.Points.length : 0
        if (!chart || chart.pointCount !== plotted) return "chart has " + (chart ? chart.pointCount : 0) + " points"
        for (let i = 0; i < plotted; i++) {
            const point = chart.pointAt(i), expected = s.trend.Points[i]
            if (point.x !== expected.X || (expected.PriceThousands === null ? !Number.isNaN(point.y)
                    : !(Math.abs(point.y - expected.PriceThousands) <= 1e-7))) return "chart point " + i
        }
        if (!legible(chart.textSurfacePairs)) return "chart text below 4.5:1 contrast"
        return ""
    }

    Timer {
        id: timer
        interval: 25; repeat: true; running: true
        onTriggered: {
            const e = gate.plan.steps[gate.phase]
            if (Date.now() - gate.started > (gate.phase === 0 ? 20000 : 5000)) { gate.fail("timed out: " + gate.difference); return }
            while (gate.cursor < e.actions.length) {
                const action = e.actions[gate.cursor]
                if (!gate.perform(action)) { gate.difference = "waiting to perform " + action.do; return }
                gate.cursor++
                gate.centered = false
            }
            gate.difference = gate.mismatch(e)
            if (gate.difference) return
            // Leave the details scrolled when the next step changes the selection.
            if (e.scrollDetailsBeforeNext && !gate.scrolled) {
                if (!gate.scrollDetailsToEnd()) { gate.difference = "details cannot scroll"; return }
                gate.scrolled = true
                return
            }
            console.log("HDB_API_STEP " + e.name + " ms=" + (Date.now() - gate.started) + " addresses=" + Resales.addressCount)
            gate.phase++
            gate.cursor = 0
            gate.scrolled = false
            gate.difference = ""
            gate.started = Date.now()
            if (gate.phase === gate.plan.steps.length) {
                console.log("HDB_API_ACCEPTANCE_PASS steps=" + gate.phase)
                stop()
                Qt.quit()
            }
        }
    }
}
