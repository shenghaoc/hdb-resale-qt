// SPDX-License-Identifier: GPL-3.0-or-later
import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
ApplicationWindow {
    id: root
    visible: true
    width: 460; height: 707
    title: "HDB accessibility reduction"
    ColumnLayout {
        anchors.fill: parent
        RowLayout {
            Button { text: "Small"; onClicked: { root.width = 640; root.height = 600 } }
            Button { text: "Large"; onClicked: { root.width = 460; root.height = 707 } }
            Button { text: "Filter"; onClicked: { list.model = 1 } }
            Button { text: "All"; onClicked: { list.model = 1000 } }
            Button { text: "Hide"; onClicked: list.visible = !list.visible }
        }
        ScrollView {
            Layout.fillWidth: true; Layout.fillHeight: true
            ScrollBar.vertical.Accessible.name: "Scroll rows"
            ListView {
                id: list
                clip: true; model: 1000; reuseItems: false
                Accessible.role: Accessible.List
                Accessible.name: "Rows"
                delegate: ItemDelegate {
                    required property int index
                    width: list.width; height: 54
                    text: "row " + index
                    Accessible.role: Accessible.ListItem
                    Accessible.name: "row " + index
                }
            }
        }
    }
    Timer {
        interval: 250; repeat: true; running: true
        property real oldY: -1
        onTriggered: {
            if (oldY === list.contentY) return
            oldY = list.contentY
            const rows = []
            for (const child of list.contentItem.children) {
                if (!("index" in child)) continue
                const y = child.y - list.contentY
                if (y + child.height > 0 && y < list.height)
                    rows.push({index:child.index, y:y, height:child.height})
            }
            console.log("HDB_QT_REPRO_ROWS " + JSON.stringify({contentY:list.contentY, height:list.height, rows:rows}))
            // Read-only evidence capture, never used to drive the list.
            list.grabToImage(image => image.saveToFile("list-repro-" + Math.round(list.contentY) + ".png"))
        }
    }
}
