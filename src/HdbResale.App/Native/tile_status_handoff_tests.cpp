// SPDX-License-Identifier: GPL-3.0-or-later
// Starting and stopping the monitor while another thread logs must never route a message past the
// application's own handler, or drop one: every message reaches the custom handler exactly once. The
// logger counts the messages it begins while hdb_tile_status_start() is running, and the loop continues
// until enough of them have overlapped a handler transition.
#include "tile_status.h"
#include <QtCore/qlogging.h>
#include <atomic>
#include <cstdio>
#include <thread>

namespace {
std::atomic<long> received{0};
void custom(QtMsgType, const QMessageLogContext &, const QString &) { received.fetch_add(1); }
}

int main()
{
    constexpr long minimumCycles = 20000, minimumOverlap = 1000, cycleLimit = 50000000;
    qInstallMessageHandler(custom);
    std::atomic<bool> finished{false}, starting{false};
    std::atomic<long> emitted{0}, duringStart{0};
    std::thread logger([&] {
        while (!finished.load()) {
            if (starting.load()) duringStart.fetch_add(1);
            qWarning("HDB_HANDOFF");
            emitted.fetch_add(1);
        }
    });
    long cycles = 0;
    while ((cycles < minimumCycles || duringStart.load() < minimumOverlap) && cycles < cycleLimit) {
        starting.store(true);
        hdb_tile_status_start();
        starting.store(false);
        hdb_tile_status_stop();
        ++cycles;
    }
    finished.store(true);
    logger.join();
    if (duringStart.load() < minimumOverlap || received.load() != emitted.load()) {
        std::fprintf(stderr, "Handoff: %ld cycles, %ld messages begun during a start; the custom handler received %ld of %ld\n",
                     cycles, duringStart.load(), received.load(), emitted.load());
        return 1;
    }
    return 0;
}
