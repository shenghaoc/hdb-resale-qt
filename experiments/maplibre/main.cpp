// Isolated loader and QML host. No changes to the HDB app or Bridge dependencies.
#include <QCoreApplication>
#include <QGuiApplication>
#include <QCommandLineParser>
#include <QFile>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QPluginLoader>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QGeoServiceProvider>
#include <QDebug>
#include <memory>

int main(int argc, char **argv) {
    bool probeOnly = false;
    for (int i = 1; i < argc; ++i)
        if (QString::fromLocal8Bit(argv[i]) == "--probe-only") probeOnly = true;
    std::unique_ptr<QCoreApplication> app;
    if (probeOnly) app = std::make_unique<QCoreApplication>(argc, argv);
    else app = std::make_unique<QGuiApplication>(argc, argv);
    QCommandLineParser parser;
    parser.addHelpOption();
    parser.addOption({"probe-only", "Load GeoServices plugin without any GUI or GPU."});
    parser.addOption({"plugin", "Exact plugin library to inspect and load.", "file"});
    parser.addOption({"qml", "QML experiment file.", "file", "Demo-v3.qml"});
    parser.addOption({"style", "MapLibre style URL (local or remote).", "url", "http://127.0.0.1:8765/onemap-style.json"});
    parser.addOption({"data", "Local GeoJSON fixture.", "file", "synthetic-polygons.geojson"});
    parser.process(*app);
    qInfo().noquote() << "Qt version:" << qVersion();
    if (parser.isSet("plugin")) {
        QPluginLoader loader(parser.value("plugin"));
        qInfo().noquote() << "Metadata:" << QJsonDocument(loader.metaData()).toJson(QJsonDocument::Compact);
        const bool loaded = loader.load();
        qInfo().noquote() << "Plugin loaded:" << loaded;
        if (!loaded) {
            qCritical().noquote() << "Plugin loader error:" << loader.errorString();
            return 2;
        }
    }
    qInfo() << "Available providers:" << QGeoServiceProvider::availableServiceProviders();
    if (probeOnly) return 0;
    QFile dataFile(parser.value("data"));
    if (!dataFile.open(QIODevice::ReadOnly)) { qCritical() << dataFile.errorString(); return 3; }
    QJsonParseError error;
    const auto data = QJsonDocument::fromJson(dataFile.readAll(), &error);
    if (error.error != QJsonParseError::NoError || !data.isObject()) {
        qCritical() << "Invalid GeoJSON:" << error.errorString(); return 3;
    }
    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty("spikeData", data.toVariant());
    engine.rootContext()->setContextProperty("spikeStyleUrl", parser.value("style"));
    engine.load(QUrl::fromLocalFile(QFileInfo(parser.value("qml")).absoluteFilePath()));
    if (engine.rootObjects().isEmpty()) return 4;
    return app->exec();
}
