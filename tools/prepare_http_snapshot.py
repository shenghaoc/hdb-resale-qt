#!/usr/bin/env python3
"""Package a prepared source directory for a local static HTTP fixture. No upload."""
import argparse
import gzip
import hashlib
import json
import shutil
import tempfile
from pathlib import Path
from prepare_transaction_sources import EVIDENCE, SCHEMA, digest

def package(prepared, output):
    prepared, output = Path(prepared), Path(output)
    if output.exists() or output.is_symlink():
        raise FileExistsError('Choose a new output directory; existing packs are never changed.')
    descriptor = prepared / 'transaction-sources.json'
    if descriptor.is_symlink() or not 1 <= descriptor.stat().st_size <= 1_048_576:
        raise ValueError('Invalid source descriptor.')
    sources = json.loads(descriptor.read_text(encoding='utf-8'))
    if sources.get('schemaVersion') != SCHEMA or not 1 <= len(sources.get('sources', [])) <= 16:
        raise ValueError('Unsupported source descriptor.')
    paths = ['transaction-sources.json', *EVIDENCE[:3]]
    paths += [name for name in EVIDENCE[3:] if (prepared / name).is_file()]
    for source in sources['sources']:
        identity = source['sourceIdentity']
        import re
        if not isinstance(identity, str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,64}', identity) or source['path'] != f'transactions/{identity}.csv':
            raise ValueError('Invalid source identity/path.')
        path = prepared / source['path']
        if path.stat().st_size != source['bytes'] or digest(path) != source['sha256']:
            raise ValueError('Prepared source integrity failed.')
        paths.append(source['path'])
    if len({path.casefold() for path in paths}) != len(paths):
        raise ValueError('Duplicate source path.')
    expected = set(paths)
    actual = {str(path.relative_to(prepared)) for path in prepared.rglob('*') if path.is_file()}
    if actual != expected:
        raise ValueError('Prepared directory contains missing or unexpected files.')
    output.parent.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix='.' + output.name + '-', dir=output.parent))
    try:
        (stage / 'objects').mkdir()
        (stage / 'manifests').mkdir()
        files, total, compressed_total = [], 0, 0
        for path in paths:
            source = prepared / path
            if source.is_symlink() or source.parent.is_symlink() or not 1 <= source.stat().st_size <= 268_435_456:
                raise ValueError('Snapshot input must be a bounded regular file.')
            temporary = stage / 'object.gz'
            # Hash the same held bytes written to the deterministic gzip object.
            sha, size = hashlib.sha256(), 0
            with source.open('rb') as input, temporary.open('xb') as raw:
                with gzip.GzipFile(filename='', mode='wb', fileobj=raw, mtime=0, compresslevel=6) as output_stream:
                    while chunk := input.read(65536):
                        size += len(chunk)
                        if size > 268_435_456:
                            raise ValueError('Snapshot input exceeds 256 MiB.')
                        sha.update(chunk)
                        output_stream.write(chunk)
            compressed_size, compressed_sha = temporary.stat().st_size, digest(temporary)
            total += size
            compressed_total += compressed_size
            if total > 805_306_368 or compressed_total > 805_306_368 or compressed_size > 268_435_456:
                raise ValueError('Snapshot bounds exceeded.')
            files.append(dict(path=path, sha256=sha.hexdigest(), bytes=size,
                              gzipSha256=compressed_sha, gzipBytes=compressed_size))
            temporary.replace(stage / 'objects' / (compressed_sha + '.gz'))
        manifest = dict(schemaVersion='hdb-desktop-snapshot-v1', importerVersion=1, files=files)
        data = (json.dumps(manifest, indent=2) + '\n').encode('utf-8')
        manifest_sha = hashlib.sha256(data).hexdigest()
        (stage / 'manifests' / (manifest_sha + '.json')).write_bytes(data)
        current = dict(schemaVersion='hdb-snapshot-current-v1', manifestSha256=manifest_sha)
        (stage / 'current.json').write_text(json.dumps(current, indent=2) + '\n', encoding='utf-8')
        stage.rename(output)
        return current
    finally:
        if stage.exists():
            shutil.rmtree(stage)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepared', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(package(args.prepared, args.output), indent=2))
