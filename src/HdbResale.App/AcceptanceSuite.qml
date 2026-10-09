// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick
import QtTest

// Opt-in app-level acceptance overlay on frozen Stage 3. Every input is QtTest
// in-process input. Window sizes are test assignments, not KWin frame actions.
Item {
    id: gate
    required property var targetWindow
    readonly property var w: targetWindow
    readonly property bool production: Resales.apiGate === "acceptance-production"
    readonly property var list: w.resultList
    readonly property var details: w.detailsScrollView
    readonly property var chart: item("trendLoader").item
    property int phase: 0
    property bool dispatching: false
    property double started: Date.now()
    property double lastWheel: 0
    property real oldY: 0
    property int defaultCount: 0
    property int selectionSignals: 0
    property int failures: 0
    property var state: ({})
    property var captureMeta: ({})
    TestEvent { id: events }
    TestResult { id: result }
    Connections { target: Resales; function onSelectedMapKeyChanged() { gate.selectionSignals++ } }
    function visualChild(parent, name) {
        if (!parent) return null
        if (parent.objectName === name) return parent
        if (parent.children) for (const child of parent.children) {
            const found = visualChild(child, name)
            if (found) return found
        }
        return null
    }
    function item(name, parent = w) {
        // Virtualized delegates can have a different QObject owner from their
        // visual parent; inspect both trees using the same stable objectName.
        const found = result.findChild(parent, name) || visualChild(parent === w ? w.contentItem : parent, name)
        if (!found) throw new Error("missing objectName=" + name)
        return found
    }
    function check(ok, message) { if (!ok) throw new Error(message) }
    function recordCheck(ok, message) {
        if (ok) return
        failures++
        console.error("HDB_ACCEPTANCE_DEFECT phase="+phase+" "+message+" focus="+w.activeFocusItem)
        dump(w.contentItem)
    }
    function key(k, mods = Qt.NoModifier) { check(events.keyClick(k, mods, -1), "key delivery " + k) }
    function type(s) { for (const c of s) check(events.keyClickChar(c, Qt.NoModifier, -1), "text delivery") }
    function click(target) { check(events.mouseClick(target, target.width/2, target.height/2, Qt.LeftButton, Qt.NoModifier, -1), "click delivery") }
    function query(text) { key(Qt.Key_F, Qt.ControlModifier); key(Qt.Key_A, Qt.ControlModifier); if (text.length) type(text); else key(Qt.Key_Backspace) }
    function next(name) { console.log("HDB_ACCEPTANCE_STEP " + name); phase++; started = Date.now() }
    function dump(node, depth = 0) {
        if (!node || depth > 14) return
        console.log("HDB_ACCEPTANCE_TREE " + " ".repeat(depth) + node + " objectName=" + node.objectName
            + " visible=" + node.visible + " xywh=" + [node.x,node.y,node.width,node.height] + " focus=" + node.activeFocus)
        if (node.children) for (const child of node.children) dump(child, depth + 1)
    }
    function fail(message) {
        console.error("HDB_ACCEPTANCE_FAIL phase=" + phase + " " + message)
        dump(w.contentItem); timer.stop(); Qt.quit()
    }
    function wheel(target, delta) {
        check(events.mouseWheel(target, target.width/2, target.height/2, Qt.NoButton, Qt.NoModifier, 0, delta, -1), "wheel delivery")
    }
    function names() {
        const rows = []
        for (let i = 0; i < list.count; i++) {
            const row = list.itemAtIndex(i)
            if (!row) continue
            const p = row.mapToItem(list, 0, 0)
            if (p.y < list.height && p.y + row.height > 0)
                rows.push({key:row.addressKey, name:row.Accessible.name, selected:row.highlighted, y:p.y, height:row.height})
        }
        return rows
    }
    function checkpoint(name, facts = false) {
        console.log("HDB_ACCEPTANCE_ATSPI " + JSON.stringify({name:name, count:list.count, selected:Resales.selectedMapKey,
            rows:names(), facts:facts ? JSON.parse(Resales.selectedInspectorJson) : [],
            registrations:facts ? JSON.parse(Resales.recentTransactionsJson) : []}))
    }
    function capture(name) {
        const pane = item("inspector")
        const p = details.mapToItem(pane, 0, 0)
        const graph = chart ? item("trendGraph", chart) : null
        captureMeta = {file:name + ".png", width:pane.width, height:pane.height,
            scroll:{x:p.x,y:p.y,width:details.width,height:details.height,contentY:details.contentItem.contentY},
            headerY:item("detailsPane").mapToItem(pane,0,0).y,
            chart:chart ? {y:chart.mapToItem(details,0,0).y,height:chart.height} : null,
            palette:{window:String(w.palette.window),text:String(w.palette.windowText),link:String(w.palette.link),secondary:String(w.theme.secondaryText)},
            font:{family:w.font.family,pointSize:w.font.pointSize}, platform:Qt.platform.pluginName}
        timer.stop()
        check(pane.grabToImage(function(image) {
            if (!image.saveToFile(name + ".png")) { fail("capture write " + name); return }
            console.log("HDB_ACCEPTANCE_CAPTURE " + JSON.stringify(captureMeta))
            phase++; started=Date.now(); timer.start()
        }), "capture start " + name)
    }
    function editPrice(control, value) {
        control.contentItem.forceActiveFocus(Qt.OtherFocusReason)
        key(Qt.Key_A,Qt.ControlModifier); type(String(value)); key(Qt.Key_Return)
    }
    function settled(ms = 450) { return Date.now() - started >= ms }
    function step() {
        if (Date.now() - started > 12000) throw new Error("deadline: count=" + list.count + " selected=" + Resales.selectedMapKey + " focus=" + w.activeFocusItem)
        switch (phase) {
        case 0:
            if (!w.active) w.requestActivate()
            if (!w.active || Resales.busy || list.count < (production ? 1000 : 6)) return
            check(Qt.platform.pluginName === "wayland", "actual QPA=" + Qt.platform.pluginName)
            check(list.count === (production ? Resales.addressCount : 6), "initial count")
            defaultCount = list.count
            console.log("HDB_ACCEPTANCE_PLATFORM " + Qt.platform.pluginName + " defaultCount=" + defaultCount)
            console.log("HDB_ACCEPTANCE_DATASET " + Resales.datasetSummary)
            checkpoint("initial")
            next("runtime-platform-and-load"); break
        case 1:
            if (!settled(3000)) return
            query(production ? "geylang 30" : "bedok res")
            next("native-QtTest-find-query"); break
        case 2:
            if (list.count !== (production ? 6 : 3)) return
            if (production) {
                check(list.itemAtIndex(0).address === "30 BALAM RD" && list.itemAtIndex(1).address === "30 CASSIA CRES", "exact blocks must precede prefixes")
                for (let i=2;i<6;i++) check(/^30[1245] UBI AVE 1$/.test(list.itemAtIndex(i).address), "prefix identity")
            } else check(names().map(r=>r.key).join(",") === "bedok-748a-bedok-reservoir-cres,bedok-748b-bedok-reservoir-cres,bedok-747a-bedok-reservoir-cres", "fixture order")
            key(Qt.Key_Down); key(Qt.Key_Down); selectionSignals=0; key(Qt.Key_Return)
            next("ranking-and-keyboard-selection"); break
        case 3:
            if (!Resales.detailReady) return
            check(Resales.selectedHeading === (production ? "30 CASSIA CRES" : "748B BEDOK RESERVOIR CRES"), "selected address")
            check(selectionSignals === 1, "one selection signal")
            state.key=Resales.selectedMapKey; selectionSignals=0
            type(production ? " c" : "e")
            next("one-selection-signal"); break
        case 4:
            if (!settled()) return
            check(Resales.selectedMapKey===state.key && selectionSignals===0, "quiet model refinement (not spoken assertion)")
            type("zz")
            next("refinement-retains-selection"); break
        case 5:
            if (list.count !== 0) return
            check(Resales.selectedMapKey==="" && !w.detailsView.visible, "hidden selection cleared")
            key(Qt.Key_Escape); key(Qt.Key_Escape)
            next("hidden-selection-clears"); break
        case 6:
            if (list.count !== defaultCount) return
            check(list.activeFocus, "second Escape list focus")
            type(production ? "560121" : "4717")
            next("list-type-to-search"); break
        case 7:
            if (list.count !== (production ? 1 : 2)) return
            check(w.searchField.activeFocus, "type-to-search focus")
            key(Qt.Key_Escape); key(Qt.Key_L,Qt.ControlModifier)
            next("postal-query-and-filter-focus"); break
        case 8:
            if (!w.filtersView.townPicker.activeFocus || list.count!==defaultCount) return
            key(Qt.Key_Home); key(Qt.Key_Down)
            next("town-filter-input"); break
        case 9:
            if (!settled()) return
            check(Resales.townIndex > 0 && list.count < defaultCount && list.count > 0, "town filter did not narrow")
            for (const row of names()) check(item("addressRow-"+row.key).townName==="ANG MO KIO", "town membership")
            click(w.filtersView.resetButton)
            next("town-filter-and-reset"); break
        case 10:
            if (list.count!==defaultCount) return
            editPrice(w.filtersView.pricePicker,500000)
            next("maximum-price-input"); break
        case 11:
            if (!settled()) return
            check(Resales.maximumPrice===500000 && list.count===(production?2569:3), "maximum count="+list.count)
            editPrice(w.filtersView.minimumPicker,600000)
            next("maximum-price-count"); break
        case 12:
            if (!settled()) return
            check(Resales.minimumPrice===600000 && list.count===0 && w.filtersView.priceInvalid, "reversed range")
            click(w.filtersView.resetButton)
            next("reversed-range"); break
        case 13:
            if (list.count!==defaultCount) return
            w.filtersView.recencyPicker.forceActiveFocus(Qt.OtherFocusReason); key(Qt.Key_Home); key(Qt.Key_Down)
            next("recency-input"); break
        case 14:
            if (!settled()) return
            check(Resales.recencyMonths===12 && list.count===(production?7517:4),"recency count="+list.count)
            click(w.filtersView.resetButton)
            next("recency-and-reset"); break
        case 15:
            if (list.count!==defaultCount) return
            query(production?"560121":"bedok res")
            next("prepare-inspector"); break
        case 16:
            if (list.count!==(production?1:3)) return
            key(Qt.Key_Down); if (!production) key(Qt.Key_Down); key(Qt.Key_Return)
            next("select-inspector"); break
        case 17:
            if (!Resales.detailReady || !chart || !chart.pointsAgree()) return
            state.key=Resales.selectedMapKey
            check(state.key===(production?"ang-mo-kio-121-ang-mo-kio-ave-3":"bedok-748b-bedok-reservoir-cres"),"inspector identity")
            check(w.inspectorView.count===4,"four sections")
            for(let i=0;i<w.inspectorView.count;i++) {
                const section=w.inspectorView.itemAt(i)
                check(section.labelWidth<=section.width*0.4,"label column width")
                for(const fact of section.facts) {
                    const row=item("fact-"+fact.label,section), label=item("factLabel",row), value=item("factValue",row)
                    check(label.width>0 && value.width>0 && !label.truncated && !value.truncated,"fact clipping: "+fact.label)
                    check(label.height<=row.height+1 && value.height<=row.height+1,"fact row geometry")
                    if (fact.label==="Nearest MRT" || fact.label==="Postal code")
                        // A readable wrapped label is an observation. Clipping,
                        // row geometry and the 40% width cap remain assertions.
                        console.log("HDB_ACCEPTANCE_OBSERVATION label="+fact.label+" lineCount="+label.lineCount+" width="+label.width)
                }
            }
            check(String(item("medianSeries",chart).color)===String(w.palette.link),"chart link colour")
            check(w.contrastRatio(w.theme.secondaryText,w.palette.window)>=4.5,"secondary palette contrast")
            check(chart.trend.Sales===(production?11:10) && chart.trend.ObservedMonths===(production?8:10),"trend counts")
            check(chart.trend.MinimumY===(production?300:600) && chart.trend.MaximumY===(production?500:1150),"trend bounds")
            check(chart.trend.Points.some(p=>p.PriceThousands===null),"missing chart gap")
            console.log("HDB_ACCEPTANCE_FACTS "+JSON.stringify(JSON.parse(Resales.selectedInspectorJson)))
            checkpoint("selected-inspector",true)
            next("inspector-geometry-chart-and-palette"); break
        case 18:
            if (!settled(3000)) return
            capture("inspector-top"); break
        case 19:
            oldY=details.contentItem.contentY; wheel(details,-240)
            next("inspector-wheel-down-input"); break
        case 20:
            if (!settled()) return
            check(details.contentItem.contentY>oldY+20,"wheel down did not scroll inspector")
            oldY=details.contentItem.contentY; wheel(details,120)
            next("inspector-wheel-down"); break
        case 21:
            if (!settled()) return
            check(details.contentItem.contentY<oldY-10,"wheel up did not scroll inspector")
            click(details); key(Qt.Key_End)
            next("inspector-wheel-up"); break
        case 22:
            if (!settled()) return
            check(Math.abs(details.contentItem.contentY-(details.contentItem.contentHeight-details.contentItem.height))<2,"inspector End")
            key(Qt.Key_Home)
            next("inspector-End"); break
        case 23:
            if (!settled()) return
            check(Math.abs(details.contentItem.contentY-details.contentItem.originY)<2,"inspector Home")
            key(Qt.Key_PageDown)
            next("inspector-Home"); break
        case 24:
            if (!settled()) return
            check(details.contentItem.contentY>details.contentItem.height/2,"inspector PageDown")
            key(Qt.Key_Home)
            next("inspector-PageDown"); break
        case 25: {
            // Drive the actual wheel until the chart enters at the bottom clip edge.
            if (!settled(150) || Date.now()-lastWheel<150) return
            const y=chart.mapToItem(details,0,0).y
            if(y<details.height-50 && y>details.height-chart.height+30) { capture("chart-bottom-clip"); break }
            wheel(details,y>=details.height-50?-60:60); lastWheel=Date.now(); break
        }
        case 26: {
            if (!settled(150) || Date.now()-lastWheel<150) return
            const y=chart.mapToItem(details,0,0).y
            if(y< -30 && y> -chart.height+50) { capture("chart-top-clip"); break }
            wheel(details,y>=-30?-60:60); lastWheel=Date.now(); break
        }
        case 27:
            w.width=640; w.height=600
            next("compact-size-assignment"); break
        case 28:
            if(!settled() || !w.compact) return
            w.showView(1)
            next("compact-addresses-command"); break
        case 29:
            if(!settled()) return
            check(item("detailsBack").activeFocus,"compact Back focus")
            click(item("mapToggle"))
            next("compact-map-tab-input"); break
        case 30:
            if(!settled()) return
            check(w.mapView.visible && Resales.selectedMapKey===state.key,"map visibility/retained selection")
            recordCheck(w.mapView.activeFocus,"Stage2 Map toggle must focus map")
            click(item("addressesToggle"))
            next("compact-Qt-map-focus"); break
        case 31:
            if(!settled()) return
            check(Resales.selectedMapKey===state.key,"Addresses retains selection")
            recordCheck(item("detailsBack").activeFocus,"Stage2 Addresses toggle must focus Back")
            click(item("detailsBack"))
            next("compact-Back-input"); break
        case 32:
            if(!settled()) return
            check(Resales.selectedMapKey==="" && list.activeFocus,"Back clears selection and focuses list")
            key(Qt.Key_Down); key(Qt.Key_Return)
            next("compact-reselect"); break
        case 33:
            if(!settled() || Resales.selectedMapKey==="") return
            state.key=Resales.selectedMapKey
            key(Qt.Key_L,Qt.ControlModifier)
            next("compact-filter-shortcut"); break
        case 34:
            if(!settled()) return
            check(w.filtersView.expanded && w.filtersView.townPicker.activeFocus && Resales.selectedMapKey===state.key,"compact Filters focus/selection")
            w.width=1360; w.height=900
            next("widen-size-assignment"); break
        case 35:
            if(!settled()) return
            check(!w.compact && Resales.selectedMapKey===state.key,"widen retained selection")
            w.width=640; w.height=600; w.filtersView.expanded=false
            query(production?"560121":"748")
            next("modal-setup"); break
        case 36:
            if(!settled() || list.count!==(production?1:2)) return
            list.forceActiveFocus(Qt.OtherFocusReason); state.search=w.searchField.text; state.count=list.count
            click(item("aboutButton"))
            next("About-button"); break
        case 37:
            if(!w.modalOpen) return
            key(Qt.Key_F,Qt.ControlModifier); key(Qt.Key_L,Qt.ControlModifier)
            check(!w.searchField.activeFocus && !w.filtersView.expanded,"modal shortcuts leaked")
            key(Qt.Key_Escape)
            next("modal-shortcut-isolation"); break
        case 38:
            if(state.contentAboutOpened && !state.contentAboutClosed) {
                if(!w.modalOpen) return
                key(Qt.Key_Escape); state.contentAboutClosed=true
                return
            }
            if(w.modalOpen || !settled()) return
            check(w.searchField.text===state.search && list.count===state.count,"About Escape changed search/results")
            if(!state.contentAboutOpened) {
                // The clicked button is the invocation origin. Separately
                // verify a content-origin invocation returns to that content.
                recordCheck(item("aboutButton").activeFocus,"About-button close restores invoking button focus")
                console.log("HDB_ACCEPTANCE_OBSERVATION About clicked-button focus expectation corrected")
                list.forceActiveFocus(Qt.OtherFocusReason); w.showAbout()
                state.contentAboutOpened=true
                return
            }
            recordCheck(list.activeFocus,"About content-origin close restores list focus")
            query(""); w.width=1360; w.height=900
            next("modal-Escape-isolation"); break
        case 39:
            if(!settled() || list.count!==defaultCount) return
            list.forceActiveFocus(Qt.OtherFocusReason); key(Qt.Key_Home); oldY=list.contentY
            wheel(list,-480)
            next("list-wheel-input"); break
        case 40:
            if(!settled()) return
            // Fixture's six rows may fit at full height; production must move.
            if(production) check(list.contentY>oldY+20,"large-list wheel did not scroll")
            checkpoint("list-wheel")
            next("list-wheel-and-AT-SPI"); break
        case 41:
            if(!settled(3000)) return
            key(Qt.Key_End)
            next("large-list-End-input"); break
        case 42:
            if(!settled()) return
            check(list.currentIndex===list.count-1,"list End index")
            checkpoint("list-end")
            next("large-list-end-exposure"); break
        case 43:
            if(!settled(3000)) return
            key(Qt.Key_PageUp)
            next("large-list-PageUp-input"); break
        case 44:
            if(!settled()) return
            check(list.currentIndex<list.count-1,"list PageUp")
            checkpoint("list-page-up")
            next("large-list-page-exposure"); break
        case 45:
            if(!settled(3000)) return
            key(Qt.Key_Home)
            next("large-list-Home-input"); break
        case 46:
            if(!settled()) return
            check(list.currentIndex===0,"list Home")
            checkpoint("list-home")
            next("large-list-home-exposure"); break
        case 47:
            if(!settled(3000)) return
            oldY=list.contentY
            check(events.mousePress(list,list.width/2,40,Qt.LeftButton,Qt.NoModifier,-1),"drag press")
            check(events.mouseMove(list,list.width/2,140,100,Qt.LeftButton,Qt.NoModifier),"drag move")
            check(events.mouseRelease(list,list.width/2,140,Qt.LeftButton,Qt.NoModifier,-1),"drag release")
            next("mouse-drag-input"); break
        case 48:
            if(!settled(700)) return
            check(Math.abs(list.contentY-oldY)<2 && !list.flicking,"mouse drag flicked list")
            next("mouse-drag-does-not-flick"); break
        case 49:
            w.filtersView.typePicker.forceActiveFocus(Qt.OtherFocusReason)
            key(Qt.Key_Home)
            const typeIndex=w.filtersView.typePicker.find("4 ROOM")
            check(typeIndex>0,"4 ROOM option missing")
            for(let i=0;i<typeIndex;i++) key(Qt.Key_Down)
            next("flat-type-input"); break
        case 50:
            if(!settled()) return
            check(w.filtersView.typePicker.currentText==="4 ROOM" && list.count===(production?7238:5),"flat type count="+list.count)
            click(w.filtersView.resetButton)
            next("flat-type-count-and-reset"); break
        case 51:
            if(!settled() || list.count!==defaultCount) return
            editPrice(w.filtersView.minimumPicker,500000)
            next("minimum-price-input"); break
        case 52:
            if(!settled()) return
            check(Resales.minimumPrice===500000 && list.count===(production?6787:3),"minimum price count="+list.count)
            click(w.filtersView.resetButton)
            next("minimum-price-count-and-reset"); break
        case 53:
            if(!settled() || list.count!==defaultCount) return
            console.log("HDB_ACCEPTANCE_COMPLETE "+(production?"production":"fixture")+" defects="+failures)
            if (failures===0) console.log("HDB_ACCEPTANCE_PASS "+(production?"production":"fixture"))
            timer.stop(); w.finishGate(); break
        }
    }
    Timer {
        id: timer; interval:50; repeat:true; running:true
        onTriggered: {
            if(gate.dispatching) return
            gate.dispatching=true
            try { gate.step() } catch(error) { gate.fail(String(error)) } finally { gate.dispatching=false }
        }
    }
}
