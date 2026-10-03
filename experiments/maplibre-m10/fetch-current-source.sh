#!/usr/bin/env bash
# Official source-only acquisition into an EMPTY experiment root; abort on failure.
# No authentication or alternate-binary fallback is attempted.
set -euo pipefail
: "${M10_ROOT:?Set M10_ROOT to an empty isolated directory outside the app repository}"
script_dir=$(cd "$(dirname "$0")" && pwd)
mkdir -p "$M10_ROOT/source-current"
git -C "$M10_ROOT/source-current" init
git -C "$M10_ROOT/source-current" remote add origin https://github.com/maplibre/maplibre-native-qt.git
git -C "$M10_ROOT/source-current" fetch --depth=1 origin c3485f3a9590081c7be8edbc542341e688f6663d
git -C "$M10_ROOT/source-current" checkout --detach FETCH_HEAD
git -C "$M10_ROOT/source-current" submodule update --init --depth=1 vendor/maplibre-native
core="$M10_ROOT/source-current/vendor/maplibre-native"
# Only prerequisites of the OpenGL Qt library-only route. Each uses its gitlink pin.
git -C "$core" submodule update --init --depth=1 --jobs=4 -- \
 vendor/args vendor/boost vendor/earcut.hpp vendor/eternal vendor/expected-lite vendor/filesystem \
 vendor/kdbush.hpp vendor/PMTiles vendor/polylabel vendor/protozero vendor/rapidjson \
 vendor/supercluster vendor/unique_resource vendor/unordered_dense vendor/vector-tile vendor/wagyu \
 vendor/maplibre-native-base/deps/cheap-ruler-cpp vendor/maplibre-native-base/deps/geojson-vt-cpp \
 vendor/maplibre-native-base/deps/geojson.hpp vendor/maplibre-native-base/deps/geometry.hpp \
 vendor/maplibre-native-base/deps/jni.hpp vendor/maplibre-native-base/deps/shelf-pack-cpp \
 vendor/maplibre-native-base/deps/variant vendor/maplibre-tile-spec
git -C "$core/vendor/maplibre-tile-spec" submodule update --init --depth=1 -- \
 cpp/vendor/earcut cpp/vendor/fsst
git -C "$core" apply "$script_dir/native-library-only.patch"
git -C "$M10_ROOT/source-current" apply "$script_dir/instrumentation.patch"
printf 'Prepared pinned Qt binding %s and native core %s\n' \
 "$(git -C "$M10_ROOT/source-current" rev-parse HEAD)" "$(git -C "$core" rev-parse HEAD)"
