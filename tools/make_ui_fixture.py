#!/usr/bin/env python3
"""Generate a clearly synthetic UI stress fixture for visual review.

Not real HDB, ACRA or building data. Streets are prefixed TEST and coordinates are
invented. The fixture exercises long addresses, many results, dense/clustered
markers, multi-month trends and the match outcomes (matched, ambiguous,
unmatched). It must never be used for data, coverage or expanded-data-gate claims.
Usage: make_ui_fixture.py OUTPUT_DIR [--seed N] [--blocks N]
"""
import argparse
import csv
import json
import random
from pathlib import Path

TOWNS = {
    'ANG MO KIO': (1.3691, 103.8454), 'BEDOK': (1.3236, 103.9273), 'BISHAN': (1.3526, 103.8352),
    'BUKIT MERAH': (1.2819, 103.8239), 'CHOA CHU KANG': (1.3840, 103.7470), 'CLEMENTI': (1.3162, 103.7649),
    'JURONG WEST': (1.3404, 103.7090), 'PASIR RIS': (1.3721, 103.9474), 'PUNGGOL': (1.4043, 103.9021),
    'QUEENSTOWN': (1.2942, 103.8061), 'SENGKANG': (1.3868, 103.8914), 'TAMPINES': (1.3496, 103.9568),
    'TOA PAYOH': (1.3343, 103.8563), 'WOODLANDS': (1.4382, 103.7890), 'YISHUN': (1.4304, 103.8354),
}
FLAT_TYPES = ['2 ROOM', '3 ROOM', '4 ROOM', '5 ROOM', 'EXECUTIVE']
BASE_PRICE = {'2 ROOM': 260000, '3 ROOM': 380000, '4 ROOM': 560000, '5 ROOM': 680000, 'EXECUTIVE': 790000}
LONG_STREETS = ['TEST LONG-NAMED NEIGHBOURHOOD CENTRAL ROAD EXTENSION 14', 'TEST AVENUE 3']
FOOTPRINT = 0.00012


def months(first=(2017, 1), last=(2018, 12)):
    year, month = first
    while (year, month) <= last:
        yield f'{year}-{month:02d}'
        month += 1
        if month == 13:
            year, month = year + 1, 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--seed', type=int, default=7)
    parser.add_argument('--blocks', type=int, default=450)
    args = parser.parse_args()
    rng = random.Random(args.seed)
    out = args.output
    out.mkdir(parents=True, exist_ok=True)
    (out / 'SYNTHETIC_UI_FIXTURE.txt').write_text('Synthetic UI stress fixture. Not real data; no coverage claims.\n')
    all_months = list(months())
    transactions, properties, postal, features = [], [], [], []
    row = 2
    prop_row = 2
    postal_row = 2
    ids = 1
    town_names = list(TOWNS)
    for index in range(args.blocks):
        town = town_names[index % len(town_names)]
        lat, lon = TOWNS[town]
        street = LONG_STREETS[0] if index % 29 == 0 else f'TEST {town.split()[0]} STREET {1 + index % 9}'
        block = str(100 + index) + ('A' if index % 17 == 0 else '')
        cluster = rng.random() < 0.5  # half the blocks pack into a tight cluster per town
        spread = 0.0035 if cluster else 0.02
        y = lat + rng.uniform(-spread, spread)
        x = lon + rng.uniform(-spread, spread)
        kind = 'unmatched' if index % 13 == 5 else 'ambiguous' if index % 11 == 7 else 'matched'
        if kind != 'unmatched':
            properties.append((prop_row, block, street)); prop_row += 1
            postal_code = f'{600000 + index:06d}'
            postal.append((postal_row, block, street, postal_code)); postal_row += 1
            if kind == 'ambiguous':
                postal.append((postal_row, block, street, f'{700000 + index:06d}')); postal_row += 1
            features.append({'type': 'Feature', 'properties': {'OBJECTID': ids, 'ENTITYID': ids, 'BLK_NO': block, 'POSTAL_COD': postal_code, 'ST_COD': 'SYNTH'},
                             'geometry': {'type': 'Polygon', 'coordinates': [[[x, y], [x + FOOTPRINT, y], [x + FOOTPRINT, y + FOOTPRINT], [x, y + FOOTPRINT], [x, y]]]}})
            ids += 1
        flat = rng.choice(FLAT_TYPES)
        count = rng.choice([1, 1, 2, 3, 5, 9, 14]) if index % 6 else 24   # some addresses with long histories
        for _ in range(count):
            month = rng.choice(all_months)
            price = int(BASE_PRICE[flat] * rng.uniform(0.85, 1.25) / 1000) * 1000
            transactions.append((row, month, town, flat, block, street, f'{rng.randint(1, 15):02d} TO {rng.randint(16, 30):02d}',
                                 rng.choice([45, 67, 82, 93, 110, 125]), 'Improved', rng.randint(1975, 2012), '60 years 01 month', price))
            row += 1
    def write(name, header, rows):
        with open(out / name, 'w', newline='') as handle:
            writer = csv.writer(handle)
            writer.writerow(header)
            writer.writerows(rows)
    write('transactions.csv', ['source_row', 'month', 'town', 'flat_type', 'block', 'street_name', 'storey_range',
                               'floor_area_sqm', 'flat_model', 'lease_commence_date', 'remaining_lease', 'resale_price'], transactions)
    write('address-evidence.csv', ['source_row', 'blk_no', 'street'], properties)
    write('postal-address-evidence.csv', ['source_row', 'block', 'street_name', 'postal_code'], postal)
    (out / 'building-evidence.geojson').write_text(json.dumps({'type': 'FeatureCollection', 'features': features}))
    print(f'{len(transactions)} transactions, {len(features)} footprints in {out}')


if __name__ == '__main__':
    main()
