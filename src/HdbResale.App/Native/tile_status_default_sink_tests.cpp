// SPDX-License-Identifier: GPL-3.0-or-later
// With no custom handler installed, every diagnostic must still reach Qt's default sink: the debugger for a
// Windows GUI application, the system log or stderr elsewhere. CTest runs this with QT_FORCE_STDERR_LOGGING=1,
// so that sink is stderr, and requires the markers below in the output in order.
#include "tile_status.h"
#include <QtCore/qlogging.h>
#include <cstdio>

int main()
{
    hdb_tile_status_start();
    qWarning("HDB_DEFAULT_SINK_BEFORE");
    qWarning("QGeoTileRequestManager: Failed to fetch tile (1614,1015,11) 5 times, giving up. Last error message was: 'Service Unavailable'");
    const auto counted = hdb_tile_status_exhausted();
    qWarning("HDB_DEFAULT_SINK_AFTER");
    if (counted != 1) {
        std::printf("HDB_TEST_FAILED exhausted=%llu\n", static_cast<unsigned long long>(counted));
        return 1;
    }
    return 0;
}
