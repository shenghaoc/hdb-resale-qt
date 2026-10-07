// SPDX-License-Identifier: GPL-3.0-or-later
#include "tile_status.h"
#include <QtCore/QString>
#include <QtCore/qlogging.h>
#include <atomic>
#include <cstdio>
#include <mutex>

namespace {
std::atomic<std::uint64_t> exhausted{0};
std::atomic<QtMessageHandler> previous{nullptr};
std::mutex installation;
bool installed = false;

void observe(QtMsgType type, const QMessageLogContext &context, const QString &message)
{
    // Qt Location 6.12 reports exhausted per-tile retries without setting Map.error.
    // This is intentionally narrow and integration-tested against the pinned Qt.
    if (type == QtWarningMsg &&
        message.startsWith(QStringLiteral("QGeoTileRequestManager: Failed to fetch tile (")) &&
        message.contains(QStringLiteral(" times, giving up. Last error message was:")))
        exhausted.fetch_add(1, std::memory_order_relaxed);
    if (const auto handler = previous.load(std::memory_order_acquire)) {
        handler(type, context, message);
    } else {
        const auto line = qFormatLogMessage(type, context, message).toLocal8Bit();
        std::fprintf(stderr, "%s\n", line.constData());
        std::fflush(stderr);
    }
}
}

void hdb_tile_status_start()
{
    const std::lock_guard<std::mutex> lock(installation);
    if (installed) return;
    exhausted.store(0, std::memory_order_relaxed);
    previous.store(qInstallMessageHandler(observe), std::memory_order_release);
    installed = true;
}

void hdb_tile_status_stop()
{
    const std::lock_guard<std::mutex> lock(installation);
    if (!installed) return;
    qInstallMessageHandler(previous.load(std::memory_order_acquire));
    installed = false;
}

std::uint64_t hdb_tile_status_exhausted()
{
    return exhausted.load(std::memory_order_relaxed);
}
