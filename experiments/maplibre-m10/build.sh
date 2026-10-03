#!/usr/bin/env bash
set -euo pipefail
source /workspace/shared/hdb-env.sh
root="${M10_ROOT:-/workspace/shared/m10-maplibre}"
variant=${1:-current}
export LIBRARY_PATH="$root/sysroot/usr/lib/x86_64-linux-gnu:/workspace/shared/hdb-toolchain/sysroot/usr/lib/x86_64-linux-gnu${LIBRARY_PATH:+:$LIBRARY_PATH}"
if [[ "$variant" == current ]]; then
  targets=(qtgeoservices_maplibre declarative_maplibre_locationplugin declarative_maplibre)
else
  targets=(qtgeoservices_maplibre declarative_locationplugin_maplibre)
fi
python3 "$root/measure_command.py" cmake --build "$root/build-$variant" --parallel "${JOBS:-2}" --target "${targets[@]}"
