// SPDX-License-Identifier: GPL-3.0-or-later
#include "tile_status.h"
#include <QtGui/QGuiApplication>

void hdb_set_desktop_file_name(const char16_t *name)
{
    // Static: it is kept for the application object the Bridge creates later.
    QGuiApplication::setDesktopFileName(QString::fromUtf16(name));
}
