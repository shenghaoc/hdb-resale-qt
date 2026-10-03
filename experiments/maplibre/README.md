# Isolated experiment

This folder does not modify the HDB project or add a required application dependency. Read [the spike report](../../docs/map/maplibre-spike.md) for what actually ran. Rendering is currently blocked by the official stable Linux artifact's missing ICU 56 dependency.

## Build the independent Qt 6.12.0 host

```sh
python3 generate_fixture.py
export QtDir=/path/to/Qt/6.12.0/gcc_64
bash build_probe.sh
./build/maplibre-spike --probe-only
```

The baseline probe lists installed providers without opening a window.

## Reproduce the observed plugin failure

```sh
MAPLIBRE_PREFIX=/path/to/maplibre-native-qt_v3.0.0_Qt6.7.3_Linux bash run.sh --probe-only
```

Expected in this environment: exit 2, missing `libicuuc.so.56`. The prefix is isolated; the pinned Qt installation is not changed. Download provenance and hash are in [the evidence folder](../../docs/map/maplibre-evidence/).

## Future GUI run with a qualified matching plugin

Only after coordinating desktop ownership, start a local style server from this folder:

```sh
python3 -m http.server 8765 --bind 127.0.0.1
```

In another shell, set `MAPLIBRE_PREFIX` to the separate compatible build and run:

```sh
MAPLIBRE_PREFIX=/path/to/isolated/install bash run.sh --qml Demo-v3.qml
# For a separately qualified main/4.x build:
MAPLIBRE_PREFIX=/path/to/isolated/install bash run.sh --qml Demo-v4.qml
# Optional official vector basemap smoke test:
MAPLIBRE_PREFIX=/path/to/isolated/install bash run.sh --qml Demo-v3.qml --style https://demotiles.maplibre.org/style.json
```

The experiment uses 4,096 artificial rectangles and synthetic price values. It does not assert real HDB coverage or measured performance. `--data` accepts a local GeoJSON file, but the demo's captions and expressions assume this synthetic fixture; adapt them explicitly for real data. No user/private data is uploaded.

OneMap mode displays the OneMap logo and provider links. Alternate style mode enables the map's attribution display, which must be visually checked against the chosen provider's terms before any production use.
