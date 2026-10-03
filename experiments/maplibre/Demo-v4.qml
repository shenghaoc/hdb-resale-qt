// API verified against unreleased main c3485f3 sources. Runtime not verified here.
import QtQuick
import QtQuick.Controls
import QtLocation
import QtPositioning
import MapLibre.Location 4.0

ApplicationWindow {
    id: window
    width: 1000
    height: 720
    visible: true
    title: "Isolated MapLibre Qt Location spike"
    property bool highPriceOnly: false
    property bool usingOneMap: spikeStyleUrl.indexOf("onemap-style.json") >= 0

    Plugin {
        id: mapPlugin
        name: "maplibre"
        PluginParameter { name: "maplibre.map.styles"; value: spikeStyleUrl }
        PluginParameter { name: "maplibre.cache.memory"; value: true }
    }
    MapView {
        id: view
        anchors.fill: parent
        map.plugin: mapPlugin
        map.center: QtPositioning.coordinate(1.355, 103.845)
        map.zoomLevel: 12
        map.copyrightsVisible: !window.usingOneMap
        MapLibre.style: Style {
            SourceParameter {
                styleId: "synthetic-hdb-shaped-polygons"
                type: "geojson"
                property var data: spikeData
            }
            LayerParameter {
                styleId: "polygon-fill"
                type: "fill"
                property string source: "synthetic-hdb-shaped-polygons"
                paint: ({
                    "fill-color": ["interpolate", ["linear"], ["get", "price"], 300000, "#24a7a1", 700000, "#f5b454", 1100000, "#d75468"],
                    "fill-opacity": ["interpolate", ["linear"], ["zoom"], 10, 0.3, 15, 0.8]
                })
            }
            LayerParameter {
                styleId: "polygon-outline"
                type: "line"
                property string source: "synthetic-hdb-shaped-polygons"
                paint: ({
                    "line-color": "#24363b",
                    "line-width": ["interpolate", ["linear"], ["zoom"], 10, 0.25, 16, 1.5]
                })
            }
            FilterParameter {
                styleId: "polygon-fill"
                expression: window.highPriceOnly ? [">=", "price", 700000] : []
            }
            FilterParameter {
                styleId: "polygon-outline"
                expression: window.highPriceOnly ? [">=", "price", 700000] : []
            }
        }
    }
    Rectangle {
        anchors { top: parent.top; left: parent.left; margins: 12 }
        width: info.implicitWidth + 24
        height: info.implicitHeight + 20
        color: "#eff6f6"
        radius: 6
        Column {
            id: info
            anchors.centerIn: parent
            spacing: 6
            Label { text: "4,096 synthetic polygons; artificial prices; no HDB coverage claim" }
            Label { text: "Qt Location → maplibre GeoServices → OneMap raster + 2 native style layers" }
            Label { text: "Map ready: " + view.map.mapReady + " | " + view.map.errorString }
            Button {
                text: window.highPriceOnly ? "Show all synthetic polygons" : "Show synthetic prices ≥ $700k"
                onClicked: window.highPriceOnly = !window.highPriceOnly
            }
        }
    }
    Rectangle {
        anchors { right: parent.right; bottom: parent.bottom; margins: 8 }
        visible: window.usingOneMap
        width: credits.implicitWidth + 16
        height: 32
        color: "white"
        Row {
            id: credits
            anchors.centerIn: parent
            spacing: 8
            Image {
                width: 64; height: 26
                fillMode: Image.PreserveAspectFit
                source: "../../src/HdbResale.App/assets/onemap-logo.png"
            }
            Label {
                text: '<a href="https://www.onemap.gov.sg/">OneMap</a> © contributors | <a href="https://www.sla.gov.sg/">Singapore Land Authority</a>'
                onLinkActivated: link => Qt.openUrlExternally(link)
            }
        }
    }
}
