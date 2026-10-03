#!/usr/bin/env bash
set -euo pipefail
: "${QtDir:?Set QtDir to the pinned Qt 6.12.0 installation}"
cd "$(dirname "$0")"
cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_PREFIX_PATH="$QtDir${CMAKE_PREFIX_PATH:+;$CMAKE_PREFIX_PATH}"
cmake --build build -j2
