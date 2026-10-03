#!/usr/bin/env python3
"""Generate compact, deterministic synthetic geometry; never mislabeled as real HDB data."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent
features = []
for row in range(64):
    for col in range(64):
        i = row * 64 + col
        x, y = round(103.800 + col * 0.0014, 7), round(1.310 + row * 0.0014, 7)
        w, h = 0.00045, 0.00025
        ring = [[x, y], [round(x+w, 7), y], [round(x+w, 7), round(y+h, 7)], [x, round(y+h, 7)], [x, y]]
        features.append({"type": "Feature", "id": i, "properties": {"price": 300000 + (i * 7919) % 800000, "synthetic": True}, "geometry": {"type": "Polygon", "coordinates": [ring]}})
geojson = {"type": "FeatureCollection", "features": features}
(root / "synthetic-polygons.geojson").write_text(json.dumps(geojson, separators=(",", ":")))
base = {"version": 8, "name": "OneMap raster test", "sources": {"onemap": {"type": "raster", "tiles": ["https://www.onemap.gov.sg/maps/tiles/Default/{z}/{x}/{y}.png"], "tileSize": 256, "minzoom": 11, "maxzoom": 19, "attribution": "OneMap © contributors | Singapore Land Authority"}}, "layers": [{"id": "onemap", "type": "raster", "source": "onemap"}]}
(root / "onemap-style.json").write_text(json.dumps(base, indent=2)+"\n")
(root / "Demo-v4.qml").write_text((root / "Demo-v3.qml").read_text().replace("official v3.0.0 sources", "unreleased main c3485f3 sources").replace("import MapLibre 3.0", "import MapLibre.Location 4.0"))
print(f"Generated {len(features)} synthetic polygons; {len(features)*5} coordinate pairs")
