import QtQuick
import QtQuick.Controls
import QtGraphs

Column {
    id: root
    spacing: 3
    readonly property var trend: JSON.parse(Resales.trendJson)
    property bool completed: false
    readonly property int pointCount: medianSeries.count
    function pointAt(index) { return medianSeries.at(index) }
    function refreshSeries() {
        const points = []
        if (trend.ObservedMonths > 0) {
            for (const point of trend.Points)
                points.push(Qt.point(point.X, point.PriceThousands === null ? NaN : point.PriceThousands))
        }
        if (Resales.runtimeGateFault === "buyer-drop-trend" && points.length > 0) points.pop()
        if (Resales.runtimeGateFault === "buyer-nan-trend") {
            for(let i=0;i<points.length;i++) if(Number.isFinite(points[i].y)) { points[i]=Qt.point(points[i].x,NaN);break }
        }
        medianSeries.replace(points)
    }
    function pointsAgree() {
        const expectedCount = trend.ObservedMonths > 0 ? trend.Points.length : 0
        if (medianSeries.count !== expectedCount) return false
        for(let i=0;i<expectedCount;i++) {
            const actual=medianSeries.at(i);const expected=trend.Points[i]
            if(actual.x!==expected.X)return false
            if(expected.PriceThousands===null) { if(!Number.isNaN(actual.y))return false }
            else if(!Number.isFinite(actual.y)||Math.abs(actual.y-expected.PriceThousands)>0.0000001)return false
        }
        return true
    }
    onTrendChanged: if(completed) refreshSeries()
    Component.onCompleted: { completed=true; refreshSeries() }
    Label { text: "Monthly median price · matching sales"; font.bold: true; width: parent.width; wrapMode: Text.WordWrap }
    Label {
        width: parent.width; font.pixelSize: 11; color: "#455a64"; wrapMode: Text.WordWrap
        text: trend.Start + "–" + trend.End + " · " + trend.Sales + " sales in " + trend.ObservedMonths + " observed months"
    }
    GraphsView {
        id: graph
        width: parent.width; height: visible ? 130 : 0
        visible: root.trend.ObservedMonths > 0
        Accessible.name: "Monthly median resale prices in thousands of Singapore dollars. Gaps mean no matching sale."
        marginLeft: 4; marginRight: 8; marginTop: 4; marginBottom: 2
        theme: GraphsTheme {
            colorScheme: GraphsTheme.ColorScheme.Light
            backgroundVisible: false
            plotAreaBackgroundVisible: false
            axisYLabelFont.pixelSize: 10
            axisXLabelFont.pixelSize: 10
            grid.mainColor: "#dce4e9"
        }
        axisX: ValueAxis { min: 0; max: Math.max(1, root.trend.Points.length - 1); labelsVisible: false; gridVisible: false; subGridVisible: false }
        axisY: ValueAxis {
            min: root.trend.MinimumY; max: root.trend.MaximumY
            tickInterval: (max-min)/4; labelDecimals: 0; subGridVisible: false
            titleText: "S$000"; titleFont.pixelSize: 10
        }
        LineSeries {
            id: medianSeries; color: "#1565c0"; width: 2
            pointDelegate: Rectangle {
                property real pointValueY
                width: 5; height: 5; radius: 2.5; color: "#1565c0"
                visible: Number.isFinite(pointValueY)
            }
        }
    }
    Row {
        width: parent.width; visible: graph.visible
        Label { width: root.width/2; text: root.trend.Start; font.pixelSize: 10 }
        Label { width: root.width/2; text: root.trend.End; horizontalAlignment: Text.AlignRight; font.pixelSize: 10 }
    }
    Label {
        width: parent.width; wrapMode: Text.WordWrap; font.pixelSize: 11; color: "#455a64"
        text: graph.visible ? "Gaps mean no matching sale; dots show observed monthly medians. Flat-type and price filters change this cohort."
                            : "No matching registrations in this 24-month display window."
    }
}
