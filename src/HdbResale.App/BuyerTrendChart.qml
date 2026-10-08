import QtQuick
import QtQuick.Controls
import QtGraphs

// The selected address's 24-month median chart, in the platform palette and font so dark mode and text scaling
// flow through. C# computes the series; this only plots it.
Column {
    id: root
    spacing: root.theme.xs
    readonly property var theme: Window.window.theme
    readonly property real captionSize: Window.window.font.pointSize * theme.captionScale
    readonly property var trend: JSON.parse(Resales.trendJson)
    property bool completed: false
    readonly property int pointCount: medianSeries.count
    readonly property var textSurfacePairs: [[metadataLabel, root], [cautionLabel, root]]
    function pointAt(index) { return medianSeries.at(index) }
    function refreshSeries() {
        const points = []
        if (trend.ObservedMonths > 0) {
            for (const point of trend.Points)
                points.push(Qt.point(point.X, point.PriceThousands === null ? NaN : point.PriceThousands))
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
    Label {
        text: qsTr("Monthly median price"); font.weight: Font.DemiBold; width: parent.width; wrapMode: Text.WordWrap
        Accessible.role: Accessible.Heading
    }
    Label {
        id: metadataLabel
        width: parent.width; font.pointSize: root.captionSize; color: root.theme.secondaryText; wrapMode: Text.WordWrap
        font.features: { "tnum": 1 }
        text: root.trend.Start + "–" + root.trend.End + " · " + root.trend.Sales + " sales in " + root.trend.ObservedMonths + " observed months"
    }
    GraphsView {
        id: graph
        width: parent.width; height: visible ? 130 : 0
        visible: root.trend.ObservedMonths > 0
        Accessible.name: "Monthly median resale prices in thousands of Singapore dollars. Gaps mean no sale that month."
        marginLeft: 4; marginRight: 8; marginTop: 4; marginBottom: 2
        theme: GraphsTheme {
            colorScheme: GraphsTheme.ColorScheme.Automatic
            backgroundVisible: false
            plotAreaBackgroundVisible: false
            axisYLabelFont.pointSize: root.captionSize
            axisXLabelFont.pointSize: root.captionSize
            axisX.labelTextColor: root.palette.windowText
            axisY.labelTextColor: root.palette.windowText
            labelTextColor: root.palette.windowText
            grid.mainColor: root.palette.mid
        }
        axisX: ValueAxis { min: 0; max: Math.max(1, root.trend.Points.length - 1); labelsVisible: false; gridVisible: false; subGridVisible: false }
        axisY: ValueAxis {
            min: root.trend.MinimumY; max: root.trend.MaximumY
            tickInterval: (max-min)/4; labelDecimals: 0; subGridVisible: false
            titleText: "S$000"; titleFont.pointSize: root.captionSize
        }
        LineSeries {
            id: medianSeries; color: root.palette.link; width: 2
            pointDelegate: Rectangle {
                property real pointValueY
                width: 5; height: 5; radius: 2.5; color: root.palette.link
                visible: Number.isFinite(pointValueY)
            }
        }
    }
    Row {
        width: parent.width; visible: graph.visible
        Label { width: root.width/2; text: root.trend.Start; font.pointSize: root.captionSize; color: root.theme.secondaryText; font.features: { "tnum": 1 } }
        Label { width: root.width/2; text: root.trend.End; horizontalAlignment: Text.AlignRight; font.pointSize: root.captionSize; color: root.theme.secondaryText; font.features: { "tnum": 1 } }
    }
    Label {
        id: cautionLabel
        width: parent.width; wrapMode: Text.WordWrap; font.pointSize: root.captionSize; color: root.theme.secondaryText
        text: graph.visible ? qsTr("Every flat type. Gaps mean no sale that month; dots are observed monthly medians.")
                            : qsTr("No registrations at this address in this 24-month window.")
    }
}
