#!/usr/bin/env bash
set -euo pipefail
source /workspace/shared/hdb-env.sh
root="${M10_ROOT:-/workspace/shared/m10-maplibre}"
variant=${1:-current}
export PKG_CONFIG_PATH="$root/sysroot/usr/lib/x86_64-linux-gnu/pkgconfig"
export PKG_CONFIG_SYSROOT_DIR="$root/sysroot"
export LIBRARY_PATH="$root/sysroot/usr/lib/x86_64-linux-gnu:/workspace/shared/hdb-toolchain/sysroot/usr/lib/x86_64-linux-gnu${LIBRARY_PATH:+:$LIBRARY_PATH}"
cmake -S "$root/source-$variant" -B "$root/build-$variant" -G Ninja \
 -DCMAKE_BUILD_TYPE=Release \
 -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
 -DCMAKE_TOOLCHAIN_FILE="$QtDir/lib/cmake/Qt6/qt.toolchain.cmake" \
 -DCMAKE_PREFIX_PATH="$root/sysroot/usr;/workspace/shared/hdb-toolchain/sysroot/usr;$QtDir" \
 -DCMAKE_INSTALL_PREFIX="$root/install-$variant" \
 -DCMAKE_INSTALL_RPATH="$root/sysroot/usr/lib/x86_64-linux-gnu;/workspace/shared/hdb-toolchain/sysroot/usr/lib/x86_64-linux-gnu;$QtDir/lib;\$ORIGIN" \
 -DCMAKE_CXX_FLAGS="-isystem $root/sysroot/usr/include" \
 -DCMAKE_C_FLAGS="-isystem $root/sysroot/usr/include" \
 -DCMAKE_SHARED_LINKER_FLAGS="-L$root/sysroot/usr/lib/x86_64-linux-gnu" \
 -DCMAKE_EXE_LINKER_FLAGS="-L$root/sysroot/usr/lib/x86_64-linux-gnu" \
 -DICU_ROOT="$root/sysroot/usr" \
 -DMLN_WITH_OPENGL=ON -DMLN_WITH_VULKAN=OFF -DMLN_WITH_METAL=OFF \
 -DMLN_WITH_GLFW=OFF -DMLN_WITH_WERROR=OFF \
 -DMLN_QT_WITH_WIDGETS=OFF -DMLN_QT_WITH_LOCATION=ON \
 -DMLN_QT_WITH_INTERNAL_ICU=OFF -DMLN_QT_WITH_QUICK_PLUGIN=ON
