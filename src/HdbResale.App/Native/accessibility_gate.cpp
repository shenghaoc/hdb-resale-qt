// SPDX-License-Identifier: GPL-3.0-or-later
#include "accessibility_gate.h"
#include <QtCore/QElapsedTimer>
#include <QtCore/QPointer>
#include <QtCore/QVariantMap>
#include <QtGui/QAccessible>
#include <QtGui/QAccessibleEvent>
#include <QtQml/qqml.h>

namespace {

struct RecordedEvent
{
    QString type;
    qint64 ns = 0;
    QPointer<QObject> object;
    QString name;
    QString message;
    bool assertive = false;
};

QList<RecordedEvent> &recorded()
{
    static QList<RecordedEvent> events;
    return events;
}

QElapsedTimer &sinceRecord()
{
    static QElapsedTimer timer;
    return timer;
}

QString typeName(QAccessible::Event type)
{
    switch (type) {
    case QAccessible::Announcement: return QStringLiteral("Announcement");
    case QAccessible::Focus: return QStringLiteral("Focus");
    case QAccessible::NameChanged: return QStringLiteral("NameChanged");
    case QAccessible::DescriptionChanged: return QStringLiteral("DescriptionChanged");
    case QAccessible::StateChanged: return QStringLiteral("StateChanged");
    case QAccessible::ValueChanged: return QStringLiteral("ValueChanged");
    case QAccessible::Selection: return QStringLiteral("Selection");
    case QAccessible::ObjectShow: return QStringLiteral("ObjectShow");
    case QAccessible::ObjectHide: return QStringLiteral("ObjectHide");
    default: return QStringLiteral("Event%1").arg(int(type));
    }
}

// Qt calls this for every accessibility event instead of the platform bridge, so a recording gate reaches no screen
// reader; it is installed only when the keyboard gate asks.
void handle(QAccessibleEvent *event)
{
    RecordedEvent entry;
    entry.type = typeName(event->type());
    entry.ns = sinceRecord().nsecsElapsed();
    entry.object = event->object();
    if (const QAccessibleInterface *interface = event->accessibleInterface())
        entry.name = interface->text(QAccessible::Name);
    if (event->type() == QAccessible::Announcement) {
        const auto *announcement = static_cast<QAccessibleAnnouncementEvent *>(event);
        entry.message = announcement->message();
        entry.assertive = announcement->politeness() == QAccessible::AnnouncementPoliteness::Assertive;
    }
    recorded().append(entry);
}

} // namespace

bool AccessibilityGate::press(QObject *object)
{
    auto *interface = QAccessible::queryAccessibleInterface(object);
    auto *actions = interface ? interface->actionInterface() : nullptr;
    if (!actions || !actions->actionNames().contains(QAccessibleActionInterface::pressAction()))
        return false;
    actions->doAction(QAccessibleActionInterface::pressAction());
    return true;
}

void AccessibilityGate::record()
{
    sinceRecord().start();
    QAccessible::setActive(true);
    QAccessible::installUpdateHandler(&handle);
}

void AccessibilityGate::clearEvents()
{
    recorded().clear();
    sinceRecord().start();
}

QVariantList AccessibilityGate::events() const
{
    QVariantList list;
    for (const RecordedEvent &entry : std::as_const(recorded())) {
        QVariantMap map;
        map.insert(QStringLiteral("type"), entry.type);
        map.insert(QStringLiteral("ms"), double(entry.ns) / 1e6);
        map.insert(QStringLiteral("object"), QVariant::fromValue(entry.object.data()));
        map.insert(QStringLiteral("name"), entry.name);
        map.insert(QStringLiteral("message"), entry.message);
        map.insert(QStringLiteral("assertive"), entry.assertive);
        list.append(map);
    }
    return list;
}

namespace {
void collect(QAccessibleInterface *interface, QVariantList &out, int depth)
{
    if (!interface || !interface->isValid() || depth > 64)
        return;
    QVariantMap map;
    map.insert(QStringLiteral("role"), int(interface->role()));
    map.insert(QStringLiteral("name"), interface->text(QAccessible::Name));
    out.append(map);
    for (int i = 0; i < interface->childCount(); ++i)
        collect(interface->child(i), out, depth + 1);
}
} // namespace

QVariantList AccessibilityGate::elements(QObject *object) const
{
    QVariantList out;
    collect(QAccessible::queryAccessibleInterface(object), out, 0);
    return out;
}

void registerAccessibilityGate()
{
    qmlRegisterSingletonType<AccessibilityGate>("HdbResale.Acceptance", 1, 0, "AccessibilityGate",
        [](QQmlEngine *, QJSEngine *) -> QObject * { return new AccessibilityGate; });
}
