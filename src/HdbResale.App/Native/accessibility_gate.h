// SPDX-License-Identifier: GPL-3.0-or-later
#pragma once
#include <QtCore/QObject>
#include <QtCore/QVariantList>

// Only registered by the opt-in keyboard gate. Exercise Qt's public accessibility
// interfaces, rather than calling a QML handler or synthesizing a click.
class AccessibilityGate : public QObject
{
    Q_OBJECT
public:
    using QObject::QObject;
    Q_INVOKABLE bool press(QObject *object);
    // Starts recording the accessibility events Qt raises (what a platform bridge would pass to a screen reader),
    // in the order and with the timing they are raised. Also turns accessibility on, which Qt otherwise does only
    // when a screen reader is running; some events, such as an item gaining focus, are raised only then.
    Q_INVOKABLE void record();
    Q_INVOKABLE void clearEvents();
    // One map per event: type ("Focus", "Announcement", "NameChanged", ...), ms (since record()), object, name (the
    // object's accessible name when raised) and, for an Announcement, message and assertive.
    Q_INVOKABLE QVariantList events() const;
    // The accessible elements under an object (a window, an item) as Qt presents them to a platform bridge, depth first:
    // one map per element with role (QAccessible::Role, as Accessible.role numbers it) and name.
    Q_INVOKABLE QVariantList elements(QObject *object) const;
    // Sets which controls Tab reaches (Qt::TabFocusBehavior), as a platform setting would: VoiceOver's full keyboard access
    // on macOS and KDE reach every control, while macOS by default stops only at text fields and lists.
    Q_INVOKABLE void setTabFocusBehavior(int behavior);
};
void registerAccessibilityGate();
