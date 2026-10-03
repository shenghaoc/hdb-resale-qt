#!/usr/bin/env bash
# Launch only in the coordinated native desktop window; outputs are isolated.
set -uo pipefail
source /workspace/shared/hdb-env.sh
root="${M10_ROOT:-/workspace/shared/m10-maplibre}"
prefix="$root/install-current"
export QT_PLUGIN_PATH="$prefix/plugins:$QtDir/plugins"
export QML_IMPORT_PATH="$prefix/qml:$QtDir/qml"
export QML2_IMPORT_PATH="$QML_IMPORT_PATH"
export LD_LIBRARY_PATH="$prefix/lib:$prefix/lib64:$root/sysroot/usr/lib/x86_64-linux-gnu:/workspace/shared/hdb-toolchain/sysroot/usr/lib/x86_64-linux-gnu:$QtDir/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export QSG_RHI_BACKEND=opengl
export M10_MAPLIBRE_TRACE=1
cd "$root/experiment"
name="${M10_RUN_NAME:-comparison}"
mkdir -p "$root/results"
# Geometry must be passed from observed B map viewport; defaults are provisional.
timeout --signal=TERM --kill-after=5s 360s "$root/experiment-build/m10-maplibre" \
 --data-dir "${M10_DATA_DIR:-/workspace/shared/hdb-m10-maplibre-data}" \
 --output "$root/results/$name.json" \
 --width "${M10_VIEWPORT_WIDTH:-871}" --height "${M10_VIEWPORT_HEIGHT:-529}" "$@" \
 > "$root/results/$name.log" 2>&1
code=$?
printf '%s\n' "$code" > "$root/results/$name.exit"
exit "$code"
