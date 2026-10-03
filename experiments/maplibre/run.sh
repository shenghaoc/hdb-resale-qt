#!/usr/bin/env bash
set -euo pipefail
: "${QtDir:?Set QtDir to the pinned Qt 6.12.0 installation}"
cd "$(dirname "$0")"
: "${MAPLIBRE_PREFIX:?Set MAPLIBRE_PREFIX to an isolated Qt-6.12-compatible MapLibre install}"
export QT_PLUGIN_PATH="$MAPLIBRE_PREFIX/plugins${QT_PLUGIN_PATH:+:$QT_PLUGIN_PATH}"
export QML_IMPORT_PATH="$MAPLIBRE_PREFIX/qml${QML_IMPORT_PATH:+:$QML_IMPORT_PATH}"
export LD_LIBRARY_PATH="$MAPLIBRE_PREFIX/lib:$MAPLIBRE_PREFIX/lib64:$QtDir/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export QSG_RHI_BACKEND=opengl
exec ./build/maplibre-spike --plugin "$MAPLIBRE_PREFIX/plugins/geoservices/libqtgeoservices_maplibre.so" "$@"
