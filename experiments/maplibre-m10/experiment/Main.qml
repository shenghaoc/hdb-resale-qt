import QtQuick
import QtQuick.Controls
import QtLocation
import QtPositioning
import MapLibre.Location 4.0

ApplicationWindow {
    id: root
    width: viewportWidth; height: viewportHeight; visible: true
    title: "M10 isolated MapLibre native address layer"
    Plugin {
        id: mapPlugin
        name: "maplibre"
        PluginParameter { name: "maplibre.map.styles"; value: styleUrl }
        PluginParameter { name: "maplibre.cache.memory"; value: true }
    }
    Map {
        id: map
        objectName: "map"
        width: viewportWidth
        height: viewportHeight
        anchors { left: parent.left; top: parent.top }
        plugin: mapPlugin
        center: QtPositioning.coordinate(1.3521, 103.8198)
        zoomLevel: 11
        copyrightsVisible: false
        MapLibre.style: Style {
            SourceParameter {
                styleId: "hdb-addresses"
                type: "geojson"
                property string data: probe.sourceJson
            }
            LayerParameter {
                styleId: "addresses"
                type: "circle"
                property string source: "hdb-addresses"
                paint: ({
                    "circle-radius": ["interpolate",["linear"],["zoom"],10,3,12,5,15,7],
                    "circle-color": ["interpolate",["linear"],["get","medianPrice"],300000,"#189a90",700000,"#dda34c",1100000,"#ce4c62"],
                    "circle-stroke-color": "#ffffff",
                    "circle-stroke-width": 1,
                    "circle-opacity": 0.88
                })
            }
            LayerParameter {
                styleId: "selection"
                type: "circle"
                property string source: "hdb-addresses"
                paint: ({"circle-radius": 11,"circle-color":"#ffffff","circle-opacity":0.2,"circle-stroke-color":"#172d9a","circle-stroke-width":3})
            }
            FilterParameter {
                styleId: "selection"
                expression: ["==", "key", probe.selectedKey]
            }
        }
        TapHandler {
            onTapped: (eventPoint) => {
                let coordinate = map.toCoordinate(eventPoint.position, false)
                probe.pick(coordinate.latitude, coordinate.longitude, map.zoomLevel)
            }
        }
        WheelHandler { onWheel: (event) => map.zoomLevel += event.angleDelta.y / 120 }
        DragHandler { target: null; onTranslationChanged: (delta) => map.pan(-delta.x, -delta.y) }
    }
    Connections {
        target: probe
        function onCameraRequested(latitude, longitude, zoom) {
            map.center = QtPositioning.coordinate(latitude, longitude)
            map.zoomLevel = zoom
        }
    }
    Rectangle {
        anchors { top: map.top; left: map.left; margins: 12 }
        width: 620; height: 102; color: "#f5ffffff"; radius: 5
        Column {
            anchors { fill: parent; margins: 10 }
            spacing: 4
            Text { text: "Real M9 address summaries · one GeoJSON source · native circles"; font.bold: true }
            Text { text: probe.status; wrapMode: Text.Wrap; width: 600 }
            Text { text: "Selected: " + probe.selectedId + "  " + probe.selectedKey; width: 600; elide: Text.ElideRight }
            Text { text: "Click selection uses host point picking; native rendered-feature query is unavailable."; font.pixelSize: 11 }
        }
    }
    Rectangle {
        anchors { right: map.right; bottom: map.bottom; margins: 8 }
        width: 450; height: 38; color: "white"
        Row {
            anchors.centerIn: parent; spacing: 8
            Image { width: 70; height: 30; fillMode: Image.PreserveAspectFit; source: "onemap-logo.png" }
            Text {
                anchors.verticalCenter: parent.verticalCenter
                text: '<a href="https://www.onemap.gov.sg/">OneMap</a> © contributors | <a href="https://www.sla.gov.sg/">Singapore Land Authority</a>'
                font.pixelSize: 12
                onLinkActivated: link => Qt.openUrlExternally(link)
            }
        }
    }
}
