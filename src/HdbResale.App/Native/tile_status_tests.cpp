// SPDX-License-Identifier: GPL-3.0-or-later
#include "tile_status.h"
#include <QtCore/qlogging.h>
#include <atomic>
#include <cstdio>
#include <thread>
#include <vector>

namespace {
std::atomic<int> forwarded{0};
void original(QtMsgType, const QMessageLogContext &, const QString &) { forwarded.fetch_add(1); }
void failure() { qWarning("QGeoTileRequestManager: Failed to fetch tile (1614,1015,11) 5 times, giving up. Last error message was: 'Service Unavailable'"); }
bool require(bool ok, const char *message) { if (!ok) std::fprintf(stderr, "%s\n", message); return ok; }
}
int main()
{
    // With no custom handler, Qt hands back its own default handler: the one the monitor forwards to.
    if (!require(qInstallMessageHandler(nullptr) != nullptr, "Qt returned no default handler to forward to")) return 1;
    qInstallMessageHandler(original);
    hdb_tile_status_start();
    qWarning("Unrelated warning");
    qWarning("QGeoTileRequestManager: Failed to fetch tile (transient retry)");
    qInfo("QGeoTileRequestManager: Failed to fetch tile (1614,1015,11) 5 times, giving up. Last error message was: 'not a warning'");
    if (!require(hdb_tile_status_exhausted() == 0, "Unrelated diagnostics counted as tile exhaustion")) return 1;
    failure();
    hdb_tile_status_start();
    if (!require(hdb_tile_status_exhausted() == 1, "Repeated installation reset the counter")) return 1;
    std::vector<std::thread> workers;
    for (int i = 0; i != 8; ++i) workers.emplace_back(failure);
    for (auto &worker : workers) worker.join();
    if (!require(hdb_tile_status_exhausted() == 9, "Concurrent failures were lost")) return 1;
    // Each diagnostic reached the original handler exactly once, so the repeated start installed nothing.
    if (!require(forwarded.load() == 12, "Original logging handler did not receive every diagnostic once")) return 1;
    return 0;
}
