// SPDX-License-Identifier: GPL-3.0-or-later
#include "tile_status.h"
#include <QtCore/QString>
#include <QtCore/qlogging.h>
#include <atomic>
#include <mutex>
#include <thread>

namespace {
std::atomic<std::uint64_t> exhausted{0};
std::atomic<QtMessageHandler> previous{nullptr};
// False only between installing observe() and publishing the handler it replaced.
std::atomic<bool> published{true};
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
    // A message can arrive in the instant between installing this handler and publishing the replaced
    // one; it waits for that instead of being dropped.
    while (!published.load(std::memory_order_acquire))
        std::this_thread::yield();
    if (const auto handler = previous.load(std::memory_order_acquire))
        handler(type, context, message);
}
}

void hdb_tile_status_start()
{
    const std::lock_guard<std::mutex> lock(installation);
    if (installed) return;
    exhausted.store(0, std::memory_order_relaxed);
    // One atomic swap, so an application's custom handler is never removed, even briefly.
    published.store(false, std::memory_order_release);
    const auto replaced = qInstallMessageHandler(observe);
    previous.store(replaced, std::memory_order_release);
    published.store(true, std::memory_order_release);
    // Qt 6 hands back its default handler when none was installed, so this is never null with the pinned
    // Qt. Without a handler to forward to, restore Qt's default and leave monitoring off rather than
    // replace the platform's diagnostic sink.
    if (!replaced) {
        qInstallMessageHandler(nullptr);
        return;
    }
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
