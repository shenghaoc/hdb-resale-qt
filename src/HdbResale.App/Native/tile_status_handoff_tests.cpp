// SPDX-License-Identifier: GPL-3.0-or-later
// Starting and stopping the monitor while another thread logs must never route a message past the
// application's own handler, or drop one: every message reaches the custom handler exactly once. The
// logger keeps running until enough handoffs have happened while it was logging.
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
    constexpr long minimumCycles = 20000, minimumOverlap = 2000, cycleLimit = 50000000;
    qInstallMessageHandler(custom);
    std::atomic<bool> finished{false};
    std::atomic<long> emitted{0};
    std::thread logger([&] {
        while (!finished.load()) {
            qWarning("HDB_HANDOFF");
            emitted.fetch_add(1);
        }
    });
    while (emitted.load() == 0) std::this_thread::yield();
    const long before = emitted.load();
    long cycles = 0;
    while ((cycles < minimumCycles || emitted.load() - before < minimumOverlap) && cycles < cycleLimit) {
        hdb_tile_status_start();
        hdb_tile_status_stop();
        ++cycles;
    }
    const long overlap = emitted.load() - before;
    finished.store(true);
    logger.join();
    if (overlap < minimumOverlap || received.load() != emitted.load()) {
        std::fprintf(stderr, "Handoff: %ld cycles, %ld messages logged during them; the custom handler received %ld of %ld\n",
                     cycles, overlap, received.load(), emitted.load());
        return 1;
    }
    return 0;
}
