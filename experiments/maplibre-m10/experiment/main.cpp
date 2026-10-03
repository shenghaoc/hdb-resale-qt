// Isolated M10 candidate host. No production/Bridge code or renderer patches here.
#include <QGuiApplication>
#include <QCommandLineParser>
#include <QCryptographicHash>
#include <QDateTime>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QGeoCoordinate>
#include <QImage>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QMetaObject>
#include <QPointer>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickWindow>
#include <QQuickItem>
#include <QRegularExpression>
#include <QSaveFile>
#include <QSet>
#include <QTimer>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <limits>

static qint64 steadyNs() {
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}
static QString nsText(qint64 value) { return QString::number(value); }
static QJsonObject processMemory() {
    QJsonObject result{{"steadyNs", nsText(steadyNs())}, {"source", "/proc/self/status"}};
    QFile file("/proc/self/status");
    if (!file.open(QIODevice::ReadOnly)) { result.insert("available", false); return result; }
    const QHash<QString, QString> names{{"VmRSS", "rssKiB"}, {"VmHWM", "peakRssKiB"}, {"VmSize", "virtualKiB"}};
    const auto lines = QString::fromUtf8(file.readAll()).split('\n');
    for (const QString &line : lines) {
        const int colon = line.indexOf(':');
        const QString key = line.left(colon);
        if (colon < 0 || !names.contains(key)) continue;
        const auto fields = line.mid(colon + 1).trimmed().split(QRegularExpression("\\s+"), Qt::SkipEmptyParts);
        if (!fields.isEmpty()) result.insert(names.value(key), fields.first().toLongLong());
    }
    result.insert("available", result.contains("rssKiB"));
    return result;
}
static double milliseconds(qint64 from, qint64 to) { return double(to - from) / 1000000.0; }
static std::atomic<QObject *> traceSink{nullptr};
static QtMessageHandler previousHandler = nullptr;
static void captureMessage(QtMsgType type, const QMessageLogContext &context, const QString &message) {
    if (message.startsWith(QStringLiteral("M10_MAPLIBRE "))) {
        if (QObject *sink = traceSink.load(std::memory_order_acquire))
            QMetaObject::invokeMethod(sink, "nativeTrace", Qt::QueuedConnection, Q_ARG(QString, message));
    }
    if (previousHandler) previousHandler(type, context, message);
    else { const QByteArray bytes = message.toLocal8Bit(); std::fprintf(stderr, "%s\n", bytes.constData()); }
}

struct Point {
    QString key, transactionId;
    double latitude{}, longitude{};
};
struct Snapshot {
    QString name, json, sha256;
    QByteArray bytes;
    QJsonObject manifest;
    QVector<Point> points;
    QHash<QString, int> byKey;
};
struct Stage { QString kind, snapshot, label; };

class Probe final : public QObject {
    Q_OBJECT
    Q_PROPERTY(QString sourceJson READ sourceJson NOTIFY sourceJsonChanged)
    Q_PROPERTY(QString selectedKey READ selectedKey NOTIFY selectedKeyChanged)
    Q_PROPERTY(QString selectedId READ selectedId NOTIFY selectedIdChanged)
    Q_PROPERTY(QString status READ status NOTIFY statusChanged)
public:
    Probe(QString dataDir, QString output, bool manual, qint64 mainEntryNs, QSize requestedViewport, QObject *parent = nullptr)
        : QObject(parent), m_dataDir(std::move(dataDir)), m_output(std::move(output)), m_manual(manual), m_mainEntryNs(mainEntryNs), m_requestedViewport(requestedViewport) {
        m_timeout.setSingleShot(true);
        m_timeout.setInterval(25000);
        connect(&m_timeout, &QTimer::timeout, this, [this] {
            fail(QStringLiteral("25-second stage timeout: ") + m_stage.label +
                 QStringLiteral("; no qualified settled native frame/drain (not a rendering pass)"));
        });
        m_drain.setInterval(50);
        connect(&m_drain, &QTimer::timeout, this, [this] {
            if (m_finished) return;
            if (!checkViewport("periodic")) return;
            if (m_map && m_map->property("error").toInt() != 0) {
                fail(QStringLiteral("QtLocation map error: ") + m_map->property("errorString").toString());
                return;
            }
            if (m_waitingDrain && steadyNs() - m_lastActivityNs >= 400000000LL &&
                steadyNs() - m_completedNs >= 400000000LL) {
                m_waitingDrain = false;
                m_timeout.stop();
                startNext();
            }
        });
    }
    QString sourceJson() const { return m_sourceJson; }
    QString selectedKey() const { return m_selectedKey; }
    QString selectedId() const { return m_selectedId; }
    QString status() const { return m_status; }

    bool load() {
        QFile mf(QDir(m_dataDir).filePath("manifest.json"));
        if (!mf.open(QIODevice::ReadOnly)) return loadError("Cannot read manifest: " + mf.errorString());
        const QByteArray manifestBytes = mf.readAll();
        m_manifestSha = QString::fromLatin1(QCryptographicHash::hash(manifestBytes, QCryptographicHash::Sha256).toHex());
        QJsonParseError parseError;
        const QJsonDocument manifest = QJsonDocument::fromJson(manifestBytes, &parseError);
        if (parseError.error != QJsonParseError::NoError || !manifest.isArray())
            return loadError("Manifest must be a valid JSON array");
        const QSet<QString> required{"full", "town", "different", "budget", "empty"};
        for (const auto &entry : manifest.array()) {
            const QJsonObject meta = entry.toObject();
            const QString name = meta.value("name").toString();
            if (!required.contains(name)) continue;
            if (m_snapshots.contains(name)) return loadError("Duplicate manifest snapshot: " + name);
            Snapshot snapshot; snapshot.name = name; snapshot.manifest = meta;
            QFile file(QDir(m_dataDir).filePath(name + ".geojson"));
            if (!file.open(QIODevice::ReadOnly)) return loadError("Cannot read snapshot: " + name);
            snapshot.bytes = file.readAll();
            if (snapshot.bytes.size() != meta.value("geojsonBytes").toInteger(-1))
                return loadError("Byte-length mismatch: " + name);
            snapshot.sha256 = QString::fromLatin1(QCryptographicHash::hash(snapshot.bytes, QCryptographicHash::Sha256).toHex());
            QString expectedSha = meta.value("sha256").toString();
            if (expectedSha.isEmpty()) expectedSha = meta.value("geojsonSha256").toString();
            if (!expectedSha.isEmpty() && expectedSha.compare(snapshot.sha256, Qt::CaseInsensitive) != 0)
                return loadError("SHA-256 mismatch: " + name);
            const QJsonDocument doc = QJsonDocument::fromJson(snapshot.bytes, &parseError);
            if (parseError.error != QJsonParseError::NoError || !doc.isObject() ||
                doc.object().value("type").toString() != "FeatureCollection" ||
                !doc.object().value("features").isArray()) return loadError("Invalid GeoJSON: " + name);
            const QJsonArray features = doc.object().value("features").toArray();
            if (features.size() != meta.value("addresses").toInteger(-1))
                return loadError("Feature-count mismatch: " + name);
            QSet<QString> transactionIds;
            qint64 transactionCount = 0;
            static const QRegularExpression idPattern("^HDB-[0-9]+$");
            for (const auto &value : features) {
                const auto feature = value.toObject();
                const auto properties = feature.value("properties").toObject();
                const auto geometry = feature.value("geometry").toObject();
                const auto coordinate = geometry.value("coordinates").toArray();
                Point point{properties.value("key").toString(), properties.value("transactionId").toString(), 0, 0};
                if (feature.value("type").toString() != "Feature" || geometry.value("type").toString() != "Point" ||
                    coordinate.size() != 2 || !coordinate[0].isDouble() || !coordinate[1].isDouble() ||
                    point.key.isEmpty() || feature.value("id").toString() != point.key ||
                    snapshot.byKey.contains(point.key) || !idPattern.match(point.transactionId).hasMatch() ||
                    transactionIds.contains(point.transactionId)) return loadError("Invalid/duplicate stable identity or geometry: " + name);
                point.longitude = coordinate[0].toDouble(); point.latitude = coordinate[1].toDouble();
                if (!QGeoCoordinate(point.latitude, point.longitude).isValid() ||
                    !std::isfinite(point.latitude) || !std::isfinite(point.longitude))
                    return loadError("Invalid coordinate: " + name);
                const qint64 count = properties.value("transactionCount").toInteger(-1);
                if (count <= 0 || !properties.value("medianPrice").isDouble())
                    return loadError("Invalid aggregation fields: " + name);
                transactionCount += count;
                transactionIds.insert(point.transactionId);
                snapshot.byKey.insert(point.key, snapshot.points.size());
                snapshot.points.append(std::move(point));
            }
            if (transactionCount != meta.value("locatedTransactions").toInteger(-1))
                return loadError("Located-transaction sum mismatch: " + name);
            const QString expectedKey = meta.value("selectedKey").toString();
            const QString expectedId = meta.value("selectedId").toString();
            if (snapshot.points.isEmpty()) {
                if (!expectedKey.isEmpty() || !expectedId.isEmpty()) return loadError("Empty snapshot selection mismatch");
            } else if (!snapshot.byKey.contains(expectedKey) ||
                       snapshot.points.at(snapshot.byKey.value(expectedKey)).transactionId != expectedId)
                return loadError("Manifest selection key/transaction ID mismatch: " + name);
            snapshot.json = QString::fromUtf8(snapshot.bytes);
            m_validation.append(QJsonObject{{"name", name}, {"features", snapshot.points.size()},
                {"bytes", snapshot.bytes.size()}, {"sha256", snapshot.sha256},
                {"expectedHashPresent", !expectedSha.isEmpty()},
                {"hashValidation", expectedSha.isEmpty() ? "observed_only_no_expected_hash_in_manifest" : "matched"},
                {"countBytesIdentityAnchorAndAggregationChecks", "passed"}});
            m_snapshots.insert(name, std::move(snapshot));
        }
        for (const QString &name : required) if (!m_snapshots.contains(name)) return loadError("Missing snapshot: " + name);
        const auto &full = m_snapshots["full"];
        for (auto it = m_snapshots.cbegin(); it != m_snapshots.cend(); ++it) {
            for (const auto &point : it->points) if (!full.byKey.contains(point.key))
                return loadError("Filtered snapshot key absent from full export: " + point.key);
        }
        const QStringList sequence{"full", "town", "different", "budget", "full", "empty", "full", "town", "full"};
        int index = 0;
        for (const QString &name : sequence) m_stages.append({"source", name, QString::number(++index) + "-" + name});
        m_stages.append({"selection", "full", "10-selection-only"});
        m_stages.append({"zoom", "full", "11-zoom"});
        m_stages.append({"pan", "full", "12-pan"});
        m_afterValidationNs = steadyNs();
        recordMemory("after-validation");
        return true;
    }
    void beforeQmlLoad() { m_beforeQmlNs = steadyNs(); recordMemory("before-qml-load"); }
    void qmlEngineReady() { m_qmlReadyNs = steadyNs(); recordMemory("qml-engine-load-returned"); }
    void attachWindow(QQuickWindow *window) {
        m_window = window;
        m_map = window->findChild<QObject *>("map");
        connect(window, &QQuickWindow::frameSwapped, this, [this] {
            const qint64 at = steadyNs();
            QMetaObject::invokeMethod(this, [this, at] { windowFrameSwapped(at); }, Qt::QueuedConnection);
        }, Qt::DirectConnection);
        m_drain.start();
        QTimer::singleShot(0, this, [this] { startNext(); });
    }
    void closing() {
        traceSink.store(nullptr, std::memory_order_release);
        if (!m_finished) {
            m_finished = true;
            m_timeout.stop(); m_drain.stop();
            writeResults(m_manual && m_manualReady ? "manual_closed" : "interrupted", "Window/application closed before automatic completion");
        }
    }
    Q_INVOKABLE void pick(double latitude, double longitude, double zoom) {
        if (!m_current || !m_map) return;
        if (!m_manual && m_active && m_stage.kind != "selection") {
            fail("Unexpected interactive selection during an automated measurement"); return;
        }
        const qint64 started = steadyNs();
        QPointF clicked;
        if (!project(latitude, longitude, clicked)) { fail("Map.fromCoordinate unavailable for application-side picking"); return; }
        int chosen = -1;
        double best = 12.0 * 12.0;
        const double width = m_map->property("width").toDouble();
        const double height = m_map->property("height").toDouble();
        for (int i = 0; i < m_current->points.size(); ++i) {
            const Point &point = m_current->points.at(i);
            QPointF pixel;
            if (!project(point.latitude, point.longitude, pixel)) { fail("Projection failed while picking"); return; }
            if (pixel.x() < 0 || pixel.y() < 0 || pixel.x() > width || pixel.y() > height) continue;
            const double dx = pixel.x() - clicked.x(), dy = pixel.y() - clicked.y();
            const double distance = dx * dx + dy * dy;
            if (distance < best || (distance == best && (chosen < 0 || point.key < m_current->points.at(chosen).key))) {
                best = distance; chosen = i;
            }
        }
        if (chosen < 0) setSelection({}, {});
        else {
            const Point &point = m_current->points.at(chosen);
            if (!m_current->byKey.contains(point.key) ||
                m_current->points.at(m_current->byKey.value(point.key)).transactionId != point.transactionId) {
                fail("Application-side pick failed stable identity revalidation"); return;
            }
            setSelection(point.key, point.transactionId);
        }
        m_picks.append(QJsonObject{{"snapshot", m_current->name}, {"kind", "application_screen_space_nearest_center_not_renderer_hit_test"},
            {"latitude", latitude}, {"longitude", longitude}, {"zoom", zoom}, {"radiusLogicalPixels", 12},
            {"selectedKey", m_selectedKey}, {"selectedId", m_selectedId}, {"elapsedMs", milliseconds(started, steadyNs())}});
        if (m_manualReady) writeResults("manual_ready", {});
    }
public slots:
    void nativeTrace(const QString &line) {
        const auto parts = line.split(' ', Qt::SkipEmptyParts);
        QHash<QString, QString> fields;
        for (const QString &part : parts) { const int equal = part.indexOf('='); if (equal > 0) fields.insert(part.left(equal), part.mid(equal + 1)); }
        const qint64 ns = fields.value("ns").toLongLong();
        const QString event = fields.value("event");
        const QString map = fields.value("map");
        const QString source = QString::fromUtf8(QByteArray::fromPercentEncoding(fields.value("source").toUtf8()));
        QJsonObject trace;
        for (auto it = fields.cbegin(); it != fields.cend(); ++it) trace.insert(it.key(), it.value());
        if (m_native.size() < 20000) m_native.append(trace); else m_traceTruncated = true;
        m_lastActivityNs = steadyNs();
        if (m_finished) return;
        if (event == "map-load-fail") { fail("Native map-load-fail observer event"); return; }
        if (!m_active || ns < m_submitNs) return;
        if (!m_mapId.isEmpty() && map != m_mapId) return;
        if (event == "source-update-enter" && source == "hdb-addresses") {
            if (m_stage.kind != "source") { fail("Unexpected address source update in selection/camera-only stage"); return; }
            if (fields.value("submittedBytes").toLongLong() != m_current->bytes.size()) {
                if (m_updateEnterNs > 0) { fail("Unexpected address payload after matching source submission"); return; }
                m_stageRecord.insert("ignoredOldOrUnexpectedSubmissionBytes", fields.value("submittedBytes")); return;
            }
            m_mapId = map;
            m_updateSequence = fields.value("seq");
            m_updateEnterNs = ns;
            m_updateReturnNs = 0; m_frameStartNs = 0; m_firstFullNs = 0;
            ++m_updateCount;
        } else if (event == "source-update-return" && source == "hdb-addresses" && fields.value("seq") == m_updateSequence) {
            m_updateReturnNs = ns; m_frameStartNs = 0;
        } else if (event == "frame-start") {
            const qint64 after = m_stage.kind == "source" ? m_updateReturnNs : m_submitNs;
            if (after > 0 && ns > after) m_frameStartNs = ns;
        } else if (event == "frame-full" && m_frameStartNs > 0 && ns >= m_frameStartNs) {
            if (!m_firstFullNs) m_firstFullNs = ns;
            if (!m_firstEligibleFullNs) m_firstEligibleFullNs = ns;
            if (fields.value("needsRepaint") == "0") completeStage(ns);
        }
    }
signals:
    void sourceJsonChanged();
    void selectedKeyChanged();
    void selectedIdChanged();
    void statusChanged();
    void cameraRequested(double latitude, double longitude, double zoom);
private:
    bool loadError(const QString &error) { m_loadError = error; qCritical().noquote() << error; writeResults("validation_failed", error); return false; }
    void setStatus(const QString &status) { m_status = status; emit statusChanged(); }
    void setSelection(const QString &key, const QString &id) {
        if (m_selectedKey != key) { m_selectedKey = key; emit selectedKeyChanged(); }
        if (m_selectedId != id) { m_selectedId = id; emit selectedIdChanged(); }
    }
    bool project(double latitude, double longitude, QPointF &pixel) const {
        const QGeoCoordinate coordinate(latitude, longitude);
        return QMetaObject::invokeMethod(m_map, "fromCoordinate", Qt::DirectConnection,
            Q_RETURN_ARG(QPointF, pixel), Q_ARG(QGeoCoordinate, coordinate), Q_ARG(bool, false)) &&
            std::isfinite(pixel.x()) && std::isfinite(pixel.y());
    }
    bool checkViewport(const QString &phase) {
        const double width = m_map ? m_map->property("width").toDouble() : 0.0;
        const double height = m_map ? m_map->property("height").toDouble() : 0.0;
        if (!m_map || !std::isfinite(width) || !std::isfinite(height) ||
            std::abs(width - m_requestedViewport.width()) > 0.01 ||
            std::abs(height - m_requestedViewport.height()) > 0.01) {
            fail(QString("Viewport mismatch at %1: requested %2x%3, actual %4x%5")
                 .arg(phase).arg(m_requestedViewport.width()).arg(m_requestedViewport.height()).arg(width).arg(height));
            return false;
        }
        return true;
    }
    void startNext() {
        if (m_finished || !checkViewport("stage-start")) return;
        if (m_nextStage >= m_stages.size()) { finish(); return; }
        m_stage = m_stages.at(m_nextStage++);
        m_active = true;
        m_updateEnterNs = m_updateReturnNs = m_frameStartNs = m_firstFullNs = 0;
        m_firstSwapNs = m_lastSwapNs = 0; m_updateSequence.clear(); m_updateCount = 0;
        m_stageRecord = QJsonObject{{"label", m_stage.label}, {"kind", m_stage.kind}, {"snapshot", m_stage.snapshot}};
        m_current = &m_snapshots[m_stage.snapshot];
        m_stageRecord.insert("submittedFeatureCount", m_current->points.size());
        m_stageRecord.insert("inputSha256", m_current->sha256);
        setStatus(m_stage.label + " · " + QString::number(m_current->points.size()) + " addresses · awaiting native settled frame");
        m_submitNs = steadyNs();
        m_lastActivityNs = m_submitNs;
        m_stageRecord.insert("hostSubmitSteadyNs", nsText(m_submitNs));
        m_timeout.start();
        if (m_stage.kind == "source") {
            m_sourceJson = m_current->json;
            setSelection(m_current->manifest.value("selectedKey").toString(), m_current->manifest.value("selectedId").toString());
            emit sourceJsonChanged();
        } else if (m_stage.kind == "selection") {
            int candidate = -1;
            for (int i = 0; i < m_current->points.size(); ++i) {
                const Point &point = m_current->points.at(i); QPointF screen;
                if (point.key == m_selectedKey || !project(point.latitude, point.longitude, screen)) continue;
                const double viewWidth = m_map->property("width").toDouble();
                const double viewHeight = m_map->property("height").toDouble();
                if (screen.x() > 12 && screen.x() < viewWidth - 12 &&
                    screen.y() > std::min(150.0, viewHeight * 0.4) && screen.y() < viewHeight - 12) {
                    bool coordinateUnique = true;
                    for (int j = 0; j < m_current->points.size(); ++j) {
                        if (j != i && m_current->points.at(j).latitude == point.latitude &&
                            m_current->points.at(j).longitude == point.longitude) { coordinateUnique = false; break; }
                    }
                    if (coordinateUnique) { candidate = i; break; }
                }
            }
            if (candidate < 0) { fail("No on-screen alternative address for selection-only oracle"); return; }
            const Point &point = m_current->points.at(candidate);
            pick(point.latitude, point.longitude, m_cameraZoom);
            m_stageRecord.insert("expectedPickedKey", point.key);
            m_stageRecord.insert("expectedPickedTransactionId", point.transactionId);
            if (m_selectedKey != point.key || m_selectedId != point.transactionId) {
                fail("Selection-only nearest-center oracle did not return the expected stable key/transaction ID"); return;
            }
        } else if (m_stage.kind == "zoom") {
            const QString anchor = m_current->manifest.value("selectedKey").toString();
            const Point &point = m_current->points.at(m_current->byKey.value(anchor));
            m_cameraLatitude = point.latitude; m_cameraLongitude = point.longitude; m_cameraZoom = 16.0;
            emit cameraRequested(m_cameraLatitude, m_cameraLongitude, m_cameraZoom);
        } else if (m_stage.kind == "pan") {
            m_cameraLongitude += 0.018; m_cameraLatitude += 0.009;
            emit cameraRequested(m_cameraLatitude, m_cameraLongitude, m_cameraZoom);
        }
        m_stageRecord.insert("hostPropertyAssignmentMs", milliseconds(m_submitNs, steadyNs()));
        qInfo().noquote() << "M10_HOST stage=" + m_stage.label << "submit_ns=" + nsText(m_submitNs)
                          << "features=" + QString::number(m_current->points.size());
    }
    void windowFrameSwapped(qint64 ns) {
        if (m_active && ns >= m_submitNs) {
            if (!m_firstSwapNs) m_firstSwapNs = ns;
            m_lastSwapNs = ns;
        }
        if (m_swaps.size() < 20000) m_swaps.append(nsText(ns));
        // Window swaps are recorded but not used as an endless-drain activity source.
    }
    void completeStage(qint64 fullNs) {
        if (!m_active || !checkViewport("stage-complete")) return;
        m_active = false; m_completedNs = steadyNs();
        if (!m_firstSettledFullNs) m_firstSettledFullNs = fullNs;
        m_stageRecord.insert("processMemory", recordMemory("stage-complete-" + m_stage.label));
        m_stageRecord.insert("status", "qualified_native_observer_full_frame");
        m_stageRecord.insert("nativeUpdateEnterSteadyNs", nsText(m_updateEnterNs));
        m_stageRecord.insert("nativeUpdateReturnSteadyNs", nsText(m_updateReturnNs));
        m_stageRecord.insert("postUpdateFrameStartSteadyNs", nsText(m_frameStartNs));
        m_stageRecord.insert("firstFullFrameObserverSteadyNs", nsText(m_firstFullNs));
        m_stageRecord.insert("settledFullFrameObserverSteadyNs", nsText(fullNs));
        m_stageRecord.insert("hostSubmitToSettledObserverMs", milliseconds(m_submitNs, fullNs));
        if (m_updateReturnNs) {
            m_stageRecord.insert("nativeSourceCallMs", milliseconds(m_updateEnterNs, m_updateReturnNs));
            m_stageRecord.insert("nativeReturnToSettledObserverMs", milliseconds(m_updateReturnNs, fullNs));
        }
        m_stageRecord.insert("matchingSourceUpdateCount", m_updateCount);
        m_stageRecord.insert("sourceUpdateSequence", m_updateSequence);
        m_stageRecord.insert("firstWindowFrameSwappedSignalSteadyNs", nsText(m_firstSwapNs));
        m_stageRecord.insert("lastWindowFrameSwappedSignalBeforeCallbackSteadyNs", nsText(m_lastSwapNs));
        if (m_map) {
            const QGeoCoordinate center = m_map->property("center").value<QGeoCoordinate>();
            m_stageRecord.insert("camera", QJsonObject{{"latitude", center.latitude()}, {"longitude", center.longitude()},
                {"qtLocationZoom", m_map->property("zoomLevel").toDouble()}, {"width", m_map->property("width").toDouble()},
                {"height", m_map->property("height").toDouble()}, {"devicePixelRatio", m_window ? m_window->devicePixelRatio() : 0.0}});
        }
        m_stageRecord.insert("selectedKey", m_selectedKey);
        m_stageRecord.insert("selectedTransactionId", m_selectedId);
        m_stageRecord.insert("selectionIdentityInCurrentExport", m_selectedKey.isEmpty() ? m_current->points.isEmpty() :
            (m_current->byKey.contains(m_selectedKey) && m_current->points.at(m_current->byKey.value(m_selectedKey)).transactionId == m_selectedId));
        m_results.append(m_stageRecord);
        setStatus(m_stage.label + " · native full-frame callback observed; waiting quiet drain");
        if (m_stage.kind == "source" && m_stage.snapshot == "full" && !m_screenshotRequested) {
            m_screenshotRequested = true;
            QTimer::singleShot(120, this, [this] { captureFullScreenshot(); });
        }
        if (m_manual) {
            m_timeout.stop(); m_manualReady = true;
            setStatus("Manual mode · full export loaded · click/pan/zoom; nearest-center application picking");
            writeResults("manual_ready", {});
        } else m_waitingDrain = true;
        writeResults(m_manual ? "manual_ready" : "running", {});
    }
    void captureFullScreenshot() {
        if (!m_window || !m_current || m_current->name != "full") {
            m_screenshotError = "Full-stage screenshot was not captured before data changed"; return;
        }
        if (!checkViewport("screenshot")) return;
        auto *mapItem = qobject_cast<QQuickItem *>(m_map.data());
        const QImage windowImage = m_window->grabWindow();
        if (!mapItem || windowImage.isNull() || m_window->width() <= 0 || m_window->height() <= 0) {
            m_screenshotError = "Cannot obtain window image/map item for viewport crop"; return;
        }
        const QRectF sceneRect = mapItem->mapRectToScene(QRectF(0, 0, mapItem->width(), mapItem->height()));
        const double scaleX = double(windowImage.width()) / m_window->width();
        const double scaleY = double(windowImage.height()) / m_window->height();
        const QRect crop = QRectF(sceneRect.x() * scaleX, sceneRect.y() * scaleY,
                                  sceneRect.width() * scaleX, sceneRect.height() * scaleY).toAlignedRect();
        if (!windowImage.rect().contains(crop)) { m_screenshotError = "Map viewport extends outside captured window"; return; }
        // Crop the composed window, so sibling overlays inside the map rect survive.
        const QImage image = windowImage.copy(crop);
        m_screenshotCrop = QJsonObject{{"sceneX", sceneRect.x()}, {"sceneY", sceneRect.y()},
            {"logicalWidth", sceneRect.width()}, {"logicalHeight", sceneRect.height()},
            {"pixelX", crop.x()}, {"pixelY", crop.y()}, {"pixelWidth", crop.width()}, {"pixelHeight", crop.height()}};
        m_screenshotPath = QFileInfo(m_output).absolutePath() + "/" + QFileInfo(m_output).completeBaseName() + ".full.png";
        QDir().mkpath(QFileInfo(m_screenshotPath).absolutePath());
        if (image.isNull() || !image.save(m_screenshotPath)) { m_screenshotError = "grabWindow/save failed"; m_screenshotPath.clear(); }
        if (m_manualReady) writeResults("manual_ready", {});
    }
    void fail(const QString &error) {
        if (m_finished) return;
        m_finished = true; m_active = false; m_timeout.stop(); m_drain.stop();
        m_stageRecord.insert("status", "failed"); m_stageRecord.insert("failure", error);
        m_stageRecord.insert("failureSteadyNs", nsText(steadyNs()));
        m_results.append(m_stageRecord);
        setStatus(error); qCritical().noquote() << "M10_HOST failed:" << error;
        writeResults("failed", error);
        QTimer::singleShot(0, qApp, [] { QCoreApplication::exit(2); });
    }
    void finish() {
        if (m_finished) return;
        m_finished = true; m_timeout.stop(); m_drain.stop();
        setStatus("Completed source, selection, zoom and pan observer measurements");
        const bool written = writeResults("completed", {});
        QTimer::singleShot(0, qApp, [written] { QCoreApplication::exit(written ? 0 : 3); });
    }
    QJsonObject recordMemory(const QString &label) {
        QJsonObject sample = processMemory(); sample.insert("label", label); m_memorySamples.append(sample); return sample;
    }
    bool writeResults(const QString &runStatus, const QString &error) {
        QJsonObject startup{{"mainEntrySteadyNs", nsText(m_mainEntryNs)}, {"afterValidationSteadyNs", nsText(m_afterValidationNs)},
            {"beforeQmlLoadSteadyNs", nsText(m_beforeQmlNs)}, {"qmlEngineLoadReturnedSteadyNs", nsText(m_qmlReadyNs)},
            {"firstEligibleFullObserverSteadyNs", nsText(m_firstEligibleFullNs)}, {"firstSettledFullObserverSteadyNs", nsText(m_firstSettledFullNs)}};
        if (m_qmlReadyNs) startup.insert("mainToQmlEngineLoadReturnedMs", milliseconds(m_mainEntryNs, m_qmlReadyNs));
        if (m_firstEligibleFullNs) startup.insert("mainToFirstEligibleFullObserverMs", milliseconds(m_mainEntryNs, m_firstEligibleFullNs));
        if (m_firstSettledFullNs) startup.insert("mainToFirstSettledFullObserverMs", milliseconds(m_mainEntryNs, m_firstSettledFullNs));
        QJsonObject root{{"schemaVersion", 1}, {"qtVersion", qVersion()}, {"status", runStatus},
            {"error", error}, {"utcWritten", QDateTime::currentDateTimeUtc().toString(Qt::ISODateWithMs)},
            {"dataDirectory", m_dataDir}, {"manifestSha256Observed", m_manifestSha}, {"manual", m_manual},
            {"clock", "std::chrono::steady_clock nanoseconds; absolute values encoded as strings"},
            {"timingSemantics", "native observer callback delivery, not GPU fence, physical presentation, or strict source-generation proof; post-update frame-start/full required"},
            {"windowFrameSwappedSemantics", "signal-emission steady timestamp; no guaranteed association with source generation or physical presentation"},
            {"inputPreparation", "Snapshots parsed/validated before measurement; imported C# filter/aggregate/serialization timings are not added to candidate native timings"},
            {"identityValidation", "All feature keys/IDs unique and well-formed; representative IDs come from exact exported snapshots; manifest anchor checked; original transaction database not re-queried"},
            {"picker", "application-side current-filtered-set screen-space nearest center within 12 logical px via QtLocation.fromCoordinate; stable-key tie break; not native renderer hit-testing"},
            {"requestedViewport", QJsonObject{{"width", m_requestedViewport.width()}, {"height", m_requestedViewport.height()}, {"units", "logical pixels"}}},
            {"observedViewport", QJsonObject{{"width", m_map ? m_map->property("width").toDouble() : 0}, {"height", m_map ? m_map->property("height").toDouble() : 0}}},
            {"screenshotPath", m_screenshotPath}, {"screenshotError", m_screenshotError}, {"screenshotCrop", m_screenshotCrop},
            {"startup", startup}, {"processMemorySamples", m_memorySamples},
            {"startupAndMemoryComparisonScope", "standalone native renderer/measurement process retaining prevalidated snapshots; excludes .NET/Bridge and C# producer; not equivalent to full baseline app memory/startup"},
            {"validation", m_validation}, {"stages", m_results}, {"picks", m_picks},
            {"nativeTrace", m_native}, {"nativeTraceTruncated", m_traceTruncated}, {"windowFrameSwappedSignalNs", m_swaps}};
        if (m_active) root.insert("activeStage", m_stageRecord);
        QDir().mkpath(QFileInfo(m_output).absolutePath());
        QSaveFile out(m_output);
        if (!out.open(QIODevice::WriteOnly)) { std::fprintf(stderr, "Cannot write results: %s\n", qPrintable(out.errorString())); return false; }
        out.write(QJsonDocument(root).toJson(QJsonDocument::Indented));
        if (!out.commit()) { std::fprintf(stderr, "Cannot commit results: %s\n", qPrintable(out.errorString())); return false; }
        return true;
    }

    QString m_dataDir, m_output, m_sourceJson{"{\"type\":\"FeatureCollection\",\"features\":[]}"};
    QString m_selectedKey, m_selectedId, m_status{"Validating data"}, m_manifestSha, m_loadError;
    bool m_manual{}, m_manualReady{}, m_active{}, m_waitingDrain{}, m_finished{}, m_traceTruncated{}, m_screenshotRequested{};
    QHash<QString, Snapshot> m_snapshots;
    const Snapshot *m_current{};
    QVector<Stage> m_stages;
    Stage m_stage;
    int m_nextStage{}, m_updateCount{};
    QString m_mapId, m_updateSequence, m_screenshotPath, m_screenshotError;
    qint64 m_submitNs{}, m_updateEnterNs{}, m_updateReturnNs{}, m_frameStartNs{}, m_firstFullNs{}, m_completedNs{}, m_lastActivityNs{}, m_firstSwapNs{}, m_lastSwapNs{};
    qint64 m_mainEntryNs{}, m_afterValidationNs{}, m_beforeQmlNs{}, m_qmlReadyNs{}, m_firstEligibleFullNs{}, m_firstSettledFullNs{};
    double m_cameraLatitude{1.3521}, m_cameraLongitude{103.8198}, m_cameraZoom{11};
    QSize m_requestedViewport;
    QJsonObject m_stageRecord, m_screenshotCrop;
    QJsonArray m_validation, m_results, m_picks, m_native, m_swaps, m_memorySamples;
    QTimer m_timeout, m_drain;
    QPointer<QQuickWindow> m_window;
    QPointer<QObject> m_map;
};

int main(int argc, char **argv) {
    const qint64 mainEntryNs = steadyNs();
    if (!qEnvironmentVariableIsSet("M10_MAPLIBRE_TRACE")) qputenv("M10_MAPLIBRE_TRACE", "1");
    QGuiApplication app(argc, argv);
    QCommandLineParser parser;
    parser.setApplicationDescription("Isolated QtLocation/MapLibre native address-layer comparison");
    parser.addHelpOption();
    parser.addOption({"data-dir", "Exact C# exported snapshot directory", "directory", "/workspace/shared/hdb-m10-maplibre-data"});
    parser.addOption({"output", "JSON measurement result path", "file", "m10-maplibre-results.json"});
    parser.addOption({"manual", "Load full snapshot and remain open for manual interaction"});
    const QString defaultWidth = qEnvironmentVariable("M10_VIEWPORT_WIDTH", "1200");
    const QString defaultHeight = qEnvironmentVariable("M10_VIEWPORT_HEIGHT", "800");
    parser.addOption({"width", "Map-only viewport width in logical pixels", "pixels", defaultWidth});
    parser.addOption({"height", "Map-only viewport height in logical pixels", "pixels", defaultHeight});
    parser.process(app);
    bool widthOk = false, heightOk = false;
    const int viewportWidth = parser.value("width").toInt(&widthOk);
    const int viewportHeight = parser.value("height").toInt(&heightOk);
    if (!widthOk || !heightOk || viewportWidth < 128 || viewportHeight < 128 || viewportWidth > 10000 || viewportHeight > 10000) {
        qCritical("Viewport width/height must be integers from 128 to 10000 logical pixels"); return 3;
    }
    Probe probe(QFileInfo(parser.value("data-dir")).absoluteFilePath(), QFileInfo(parser.value("output")).absoluteFilePath(), parser.isSet("manual"), mainEntryNs, QSize(viewportWidth, viewportHeight));
    if (!probe.load()) return 3;
    previousHandler = qInstallMessageHandler(captureMessage);
    traceSink.store(&probe, std::memory_order_release);
    probe.beforeQmlLoad();
    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty("probe", &probe);
    engine.rootContext()->setContextProperty("viewportWidth", viewportWidth);
    engine.rootContext()->setContextProperty("viewportHeight", viewportHeight);
    QString qmlPath = QDir::current().filePath("Main.qml");
    if (!QFileInfo::exists(qmlPath)) qmlPath = QFileInfo(QString::fromUtf8(__FILE__)).absolutePath() + "/Main.qml";
    engine.rootContext()->setContextProperty("styleUrl", QUrl::fromLocalFile(QFileInfo(qmlPath).absolutePath() + "/onemap-style.json").toString());
    engine.load(QUrl::fromLocalFile(QFileInfo(qmlPath).absoluteFilePath()));
    probe.qmlEngineReady();
    if (engine.rootObjects().isEmpty()) {
        traceSink.store(nullptr, std::memory_order_release); qInstallMessageHandler(previousHandler);
        probe.closing(); return 4;
    }
    auto *window = qobject_cast<QQuickWindow *>(engine.rootObjects().first());
    if (!window) { traceSink.store(nullptr); qInstallMessageHandler(previousHandler); probe.closing(); return 4; }
    probe.attachWindow(window);
    QObject::connect(&app, &QCoreApplication::aboutToQuit, &probe, &Probe::closing);
    const int result = app.exec();
    traceSink.store(nullptr, std::memory_order_release);
    qInstallMessageHandler(previousHandler);
    return result;
}

#include "main.moc"
