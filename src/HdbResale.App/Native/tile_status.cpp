// SPDX-License-Identifier: GPL-3.0-or-later
#include "tile_status.h"
#include <QtCore/QString>
#include <QtCore/qlogging.h>
#include <atomic>
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
    // Every diagnostic continues to the handler this one replaced: Qt's own default handler unless the
    // application installed another. That keeps the platform's sink, such as the debugger for a Windows
    // GUI application or the system log; this library never formats or writes diagnostics itself.
    if (const auto handler = previous.load(std::memory_order_acquire))
        handler(type, context, message);
}
}

void hdb_tile_status_start()
{
    const std::lock_guard<std::mutex> lock(installation);
    if (installed) return;
    // Look up the handler to forward to before installing this one, so no diagnostic arrives while it is
    // unknown. For that instant Qt's default handler takes any concurrent message.
    const auto current = qInstallMessageHandler(nullptr);
    // Qt 6 hands back its default handler when none was installed, so this is never null with the pinned
    // Qt. Without a handler to forward to, stay on Qt's default and leave monitoring off rather than
    // replace the platform's diagnostic sink.
    if (!current) return;
    exhausted.store(0, std::memory_order_relaxed);
    previous.store(current, std::memory_order_release);
    qInstallMessageHandler(observe);
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
