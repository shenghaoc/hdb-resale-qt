// SPDX-License-Identifier: GPL-3.0-or-later
// Starting and stopping the monitor while another thread logs must never route a message past the
// application's own handler, or drop one: every message reaches the custom handler exactly once.
#include "tile_status.h"
#include <QtCore/qlogging.h>
#include <atomic>
#include <cstdio>
#include <thread>

namespace {
std::atomic<int> received{0};
void custom(QtMsgType, const QMessageLogContext &, const QString &) { received.fetch_add(1); }
}

int main()
{
    constexpr int messages = 20000;
    qInstallMessageHandler(custom);
    std::atomic<bool> done{false};
    std::thread logger([&] {
        for (int i = 0; i != messages; ++i) qWarning("HDB_HANDOFF %d", i);
        done.store(true);
    });
    int cycles = 0;
    while (!done.load()) {
        hdb_tile_status_start();
        hdb_tile_status_stop();
        ++cycles;
    }
    logger.join();
    if (received.load() != messages) {
        std::fprintf(stderr, "The custom handler received %d of %d messages over %d cycles\n", received.load(), messages, cycles);
        return 1;
    }
    return 0;
}
