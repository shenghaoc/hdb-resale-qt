#!/usr/bin/env python3
"""Prepare an offline, ordered resale source pack without merging source headers."""
import argparse
import csv
import hashlib
import json
import os
import re
import shutil
import tempfile
from pathlib import Path

SCHEMA = 'hdb-transaction-sources-v1'
REQUIRED = {'month', 'town', 'flat_type', 'block', 'street_name', 'resale_price'}
EVIDENCE = ('address-evidence.csv', 'postal-address-evidence.csv', 'building-evidence.geojson',
            'address-normalization.txt')

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def prepare(sources, evidence, output):
    """sources is an ordered sequence of (identity, CSV path); output must be new."""
    output = Path(output)
    if not 1 <= len(sources) <= 16:
        raise ValueError('Provide 1–16 ordered sources.')
    identities = [identity for identity, _ in sources]
    if any(not re.fullmatch(r'[A-Za-z0-9_-]{1,64}', identity) for identity in identities) or len(set(identities)) != len(identities):
        raise ValueError('Invalid or duplicate source identity.')
    if len(set(identity.casefold() for identity in identities)) != len(identities):
        raise ValueError('Source paths must also be unique on case-insensitive filesystems.')
    output.parent.mkdir(parents=True, exist_ok=True)
    lock = output.with_name('.' + output.name + '.prepare.lock')
    # An exclusive sibling lock serializes this tool's invocations. An interrupted
    # process leaves an explicit lock for operator inspection, never a valid pack.
    descriptor = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    stage = None
    try:
        os.write(descriptor, str(os.getpid()).encode('ascii'))
        if output.exists() or output.is_symlink():
            raise FileExistsError('Output already exists; choose a new directory.')
        stage = Path(tempfile.mkdtemp(prefix='.' + output.name + '-', dir=output.parent))
        (stage / 'transactions').mkdir()
        declarations = []
        total_bytes = 0
        for identity, source_path in sources:
            source_path = Path(source_path)
            if source_path.is_symlink():
                raise ValueError('Source CSV must be a regular file, not a symlink.')
            target = stage / 'transactions' / (identity + '.csv')
            # Hash and parse the same held input, retaining decoded field strings.
            with source_path.open('r', encoding='utf-8-sig', newline='') as source:
                raw_bytes = os.fstat(source.fileno()).st_size
                if not 1 <= raw_bytes <= 268_435_456:
                    raise ValueError('Source CSV exceeds 256 MiB or is empty.')
                raw_sha = hashlib.file_digest(source.buffer, 'sha256').hexdigest()
                source.seek(0)
                reader = csv.reader(source, strict=True)
                header = next(reader)
                if len(header) != len(set(header)) or not REQUIRED <= set(header) or 'source_row' in header or len(header) > 127:
                    raise ValueError('Missing, duplicate or already prepared source header.')
                if any(len(column) > 256 for column in header):
                    raise ValueError('Source column name exceeds 256 characters.')
                records = 0
                with target.open('w', encoding='utf-8', newline='') as destination:
                    writer = csv.writer(destination, lineterminator='\n')
                    writer.writerow(['source_row', *header])
                    for values in reader:
                        if not values:
                            continue
                        if len(values) != len(header):
                            raise ValueError('Source CSV field count differs from header.')
                        records += 1
                        if records > 2_000_000:
                            raise ValueError('Source exceeds 2,000,000 records.')
                        # The original record's ending physical line, including header.
                        writer.writerow([reader.line_num, *values])
                    destination.flush()
                    os.fsync(destination.fileno())
            if not records:
                raise ValueError('Empty source CSV rejected.')
            size = target.stat().st_size
            total_bytes += size
            if size > 268_435_456 or total_bytes > 536_870_912:
                raise ValueError('Prepared CSV bounds exceeded.')
            declarations.append(dict(sourceIdentity=identity, path='transactions/' + identity + '.csv',
                                     sha256=digest(target), bytes=size, records=records,
                                     rawSha256=raw_sha, rawBytes=raw_bytes, columns=['source_row', *header]))
        for name in EVIDENCE:
            path = Path(evidence) / name
            if path.is_file():
                shutil.copyfile(path, stage / name)
            elif name in EVIDENCE[:3]:
                raise FileNotFoundError('Required location evidence missing: ' + name)
        manifest = dict(schemaVersion=SCHEMA, sources=declarations)
        (stage / 'transaction-sources.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        if output.exists() or output.is_symlink():
            raise FileExistsError('Output appeared during preparation.')
        stage.rename(output)
        stage = None
        return manifest
    finally:
        os.close(descriptor)
        if stage is not None:
            shutil.rmtree(stage)
        lock.unlink()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', action='append', required=True, metavar='IDENTITY=CSV')
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    sources = []
    for value in args.source:
        identity, separator, path = value.partition('=')
        if not separator or not path:
            parser.error('--source must be IDENTITY=CSV')
        sources.append((identity, Path(path)))
    print(json.dumps(prepare(sources, args.evidence, args.output), indent=2))
