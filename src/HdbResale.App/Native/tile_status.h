// SPDX-License-Identifier: GPL-3.0-or-later
#pragma once
#include <cstdint>
#if defined(_WIN32)
#  if defined(HDB_TILE_STATUS_BUILD)
#    define HDB_TILE_STATUS_API __declspec(dllexport)
#  else
#    define HDB_TILE_STATUS_API __declspec(dllimport)
#  endif
#else
#  define HDB_TILE_STATUS_API __attribute__((visibility("default")))
#endif

extern "C" {
// Installs the monitor once; later calls do nothing. It stays installed until the process exits.
HDB_TILE_STATUS_API void hdb_tile_status_start();
HDB_TILE_STATUS_API std::uint64_t hdb_tile_status_exhausted();
}
