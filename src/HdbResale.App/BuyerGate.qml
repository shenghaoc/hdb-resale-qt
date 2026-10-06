import QtQuick
import QtQuick.Layouts
import QtLocation
import QtPositioning
Item {
    required property var targetMap
    required property var targetList
    required property var townControl
    required property var typeControl
    required property var minimumControl
    required property var priceControl
    required property var recencyControl
    required property var attributionImage
    required property var targetTrendLoader
    required property var targetDetailsScroll
    required property var textSurfacePairs
    property var expected: JSON.parse(Resales.gateBuyerExpectedJson)
    property int phase: 0
    property double started: Date.now()
    property bool prepared: false
    property bool listActivated: false
    visible: false
    // Keep the empty-state text taller than the viewport, as in the reported
    // clipped detail case. Otherwise Flickable can clamp to zero without a reset.
    Component.onCompleted: targetDetailsScroll.Layout.maximumHeight = 100
    function equal(a,b) {
        if (typeof a === "number" && typeof b === "number") return Math.abs(a-b)<0.0000001
        if (a === null || b === null || typeof a !== "object" || typeof b !== "object") return a===b
        if (Array.isArray(a) && a.length!==b.length) return false
        for(const key in b) if(!equal(a[key],b[key]))return false
        return true
    }
    function apply(e) { if(Resales.runtimeGateFault!=="buyer-skip-minimum"||phase!==4)Resales.setBuyerFilters(e.town,e.type,e.minimum,e.maximum,e.months) }
    function leaseMonths(months) { return months<0?"expired / below zero":Math.floor(months/12)+"y "+months%12+"m" }
    function mapAgrees() {
        const expectedRows=JSON.parse(Resales.gateMapRowsJson);const byKey={};const seen={}
        for(const row of expectedRows)byKey[row.mapKey]=row
        for(const item of targetMap.mapItems) {
            const row=byKey[item.mapKey]
            if(!row||seen[item.mapKey]||item.transactionId!==row.transactionId||item.transactionCount!==row.transactionCount
                ||item.addressCount!==row.addressCount||item.address!==row.address||item.priceLabel!==row.priceLabel
                ||Math.abs(item.latitude-row.latitude)>1e-10||Math.abs(item.longitude-row.longitude)>1e-10)return false
            seen[item.mapKey]=true
        }
        return targetMap.mapItems.length===expectedRows.length
    }
    function contrast(text, surface) {
        function luminance(c) {
            function linear(v) { return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear(c.r) + 0.7152 * linear(c.g) + 0.0722 * linear(c.b)
        }
        const a = luminance(text), b = luminance(surface)
        return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
    }
    function textIsLegible(pairs) {
        for (const pair of pairs) {
            const surface = pair[1].color !== undefined ? pair[1].color : pair[1].palette.window
            if (surface.a !== 1 || pair[0].color.a !== 1 || contrast(pair[0].color, surface) < 4.5) return false
        }
        return true
    }
    function scrollDetailsToEnd() {
        const flickable = targetDetailsScroll.contentItem
        const bottom = Math.max(0, flickable.contentHeight - flickable.height)
        if (bottom <= 0) return false
        flickable.contentY = flickable.originY + bottom
        return !flickable.atYBeginning
    }
    function ready(e) {
        if (!textIsLegible(textSurfacePairs)) return false
        if ((phase === 7 && !e.selected || phase === 8 || phase === 12) && !targetDetailsScroll.contentItem.atYBeginning) return false
        const state=JSON.parse(Resales.buyerStateJson)
        if (!targetMap.mapReady || targetMap.error!==Map.NoError || targetMap.viewportPending || !Resales.mapViewportReady || attributionImage.status!==Image.Ready
            || targetList.count!==e.addresses || state.addresses!==e.addresses || state.rows!==e.rows || state.latest!==e.latest
            || !equal(JSON.parse(Resales.trendJson),e.trend) || !equal(state.selected,e.selected) || townControl.currentText!==e.town || typeControl.currentText!==e.type
            || minimumControl.value!==e.minimum || priceControl.value!==e.maximum || Resales.recencyMonths!==e.months
            || recencyControl.currentIndex!==(e.months===12?1:e.months===24?2:0)
            || targetMap.mapItems.length!==Resales.presentationCount || !mapAgrees()) return false
        if(phase===1 && e.expectedFullMapped!==null && Resales.mappedCount!==e.expectedFullMapped)return false
        if (e.selected) {
            const chart=targetTrendLoader.item
            const trendCount=e.trend.ObservedMonths>0?24:0
            if(!chart||chart.pointCount!==trendCount||!chart.pointsAgree()||!textIsLegible(chart.textSurfacePairs))return false
            for(let i=0;i<trendCount;i++) {
                const point=chart.pointAt(i);const expectedPoint=e.trend.Points[i]
                if(point.x!==expectedPoint.X)return false
                if(expectedPoint.PriceThousands===null) { if(!Number.isNaN(point.y))return false }
                else if(!Number.isFinite(point.y)||Math.abs(point.y-expectedPoint.PriceThousands)>0.0000001)return false
            }
            const recent=JSON.parse(Resales.recentTransactionsJson)
            if(recent.length!==e.selected.recent.length||Resales.selectedHeading!==e.selected.address||Resales.selectedMetrics.indexOf(e.selected.latest)<0)return false
            for(let i=0;i<recent.length;i++) {
                const observed=e.selected.recent[i]
                if(recent[i].id!==observed.id||recent[i].details.indexOf(observed.lease || "unavailable")<0)return false
                const source=e.sourceEvidence ? e.sourceEvidence[observed.id] : null
                if(source && (recent[i].details.indexOf("Source "+source.identity+", row "+source.row+"; SHA-256 "+source.sha256)<0))return false
                if(source && /^[0-9]{1,3}$/.test(observed.lease || "") && recent[i].details.indexOf("reported in whole years")<0)return false
            }
            if(e.leaseMinimum===null) {
                if(Resales.selectedLease.indexOf("Derived remaining lease unavailable")<0)return false
            } else {
                if(Resales.selectedLease.indexOf("at "+e.latest)<0||Resales.selectedLease.indexOf("Not an eligibility assessment")<0)return false
                const formatLease=months => e.leaseWholeYears ? (months<0 ? "expired / below zero" : Math.round(months/12)+" years") : leaseMonths(months)
                const minimum=formatLease(e.leaseMinimum),maximum=formatLease(e.leaseMaximum)
                const leaseRange=minimum+(minimum===maximum?"":"–"+maximum)
                if(e.leaseWholeYears && Resales.selectedLease.indexOf("source precision is whole years")<0)return false
                if(Resales.selectedLease.indexOf("approximately "+leaseRange+".")<0)return false
            }
            if(phase===11) {
                let marker=null;for(const item of targetMap.mapItems)if(item.mapKey===e.key)marker=item
                if(!marker||!marker.selected||marker.transactionCount!==e.selected.count||marker.address!==e.selected.address)return false
            }
            if(Resales.selectedMapKey!==e.selected.key||Resales.selectedAddressIndex!==e.addressIndex||targetList.currentIndex!==e.addressIndex)return false
        } else if(Resales.selectedMapKey!==""||JSON.parse(Resales.recentTransactionsJson).length!==0||targetTrendLoader.item)return false
        return true
    }
    function fail() { console.error("HDB_GATE_FAIL buyer phase="+phase+" elapsed="+(Date.now()-started)+" state="+Resales.buyerStateJson); timer.stop();Qt.quit() }
    Timer {
        id:timer; interval:25;repeat:true;running:true
        onTriggered: {
            if(Date.now()-started>(Resales.scaleExpandedCoverage?10000:5000)){fail();return}
            const e=expected[phase]
            if(!prepared) {
                prepared=true
                if(phase===6) {
                    // Exercise the list's actual delegate action, not an alternate selection store.
                    if(Resales.selectedMapKey!==""){fail();return}
                    targetList.positionViewAtIndex(e.addressIndex,ListView.Center)
                } else if(phase===10) {
                    apply(e);Resales.selectAddress(e.key)
                    targetMap.center=QtPositioning.coordinate(Resales.selectedLatitude,Resales.selectedLongitude);targetMap.zoomLevel=16
                } else if(phase===11) {
                    Resales.selectAddress("")
                } else if(phase===14) {
                    targetMap.zoomLevel=12;targetMap.pan(70,80);targetMap.zoomLevel=16;targetMap.center=QtPositioning.coordinate(1.37,103.85)
                } else apply(e)
            }
            if(phase===6 && !listActivated) {
                const item=targetList.itemAtIndex(e.addressIndex)
                if(!item)return
                if(item.addressKey!==e.key||item.highlighted){fail();return}
                listActivated=true
                if(Resales.runtimeGateFault!=="buyer-skip-list")item.clicked()
            }
            if(phase===11 && Resales.selectedMapKey==="") {
                for(const item of targetMap.mapItems)if(item.mapKey===e.key){item.sourceItem.activateMarker();break}
            }
            if(!ready(e))return
            if(phase===6) {
                const item=targetList.itemAtIndex(Resales.selectedAddressIndex)
                if(!item||item.addressKey!==e.key||!item.highlighted)return
            }
            if(phase===14 && (targetMap.zoomLevel!==16||Resales.mapViewportZoom!==16||Math.abs(Resales.mapViewportLatitude-1.37)>1e-10))return
            // Start each clear-selection transition with a genuinely scrolled detail viewport.
            if (e.selected && (phase === 6 || phase === 7 || phase === 11) && !scrollDetailsToEnd()) return
            if (phase === 8 || phase === 12) console.log("HDB_BUYER_DETAILS_RESET " + e.name)
            const elapsed=Date.now()-started
            if(elapsed>(Resales.scaleExpandedCoverage?10000:5000)){fail();return}
            console.log("HDB_BUYER_STEP "+e.name+" ms="+elapsed+" transactions="+Resales.visibleCount+" addresses="+Resales.addressCount)
            phase++;prepared=false;started=Date.now()
            if(phase===expected.length){console.log("HDB_BUYER_PASS");stop();Qt.quit()}
        }
    }
}
