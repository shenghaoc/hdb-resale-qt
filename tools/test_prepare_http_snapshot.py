import gzip
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from prepare_transaction_sources import prepare
from prepare_http_snapshot import package

class HttpPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        evidence = self.root / 'evidence'
        evidence.mkdir()
        for name in ('address-evidence.csv', 'postal-address-evidence.csv', 'building-evidence.geojson'):
            (evidence / name).write_text('fixture evidence\n')
        raw = self.root / 'source.csv'
        raw.write_text('month,town,flat_type,block,street_name,resale_price,remaining_lease\n2015-01,T,3 ROOM,1,ST,400000,070\n2015-01,T,3 ROOM,1,ST,400000,070\n')
        self.prepared = self.root / 'prepared'
        prepare([('years', raw)], evidence, self.prepared)

    def tearDown(self):
        self.temp.cleanup()

    def test_deterministic_objects_inventory_and_bytes(self):
        a, b = self.root / 'a', self.root / 'b'
        current = package(self.prepared, a)
        self.assertEqual(current, package(self.prepared, b))
        manifest_path = 'manifests/' + current['manifestSha256'] + '.json'
        manifest_bytes = (a / manifest_path).read_bytes()
        self.assertEqual(hashlib.sha256(manifest_bytes).hexdigest(), current['manifestSha256'])
        manifest = json.loads(manifest_bytes)
        self.assertEqual('hdb-desktop-snapshot-v1', manifest['schemaVersion'])
        self.assertEqual(1, manifest['importerVersion'])
        self.assertEqual(5, len(manifest['files']))
        for entry in manifest['files']:
            object_path = 'objects/' + entry['gzipSha256'] + '.gz'
            compressed = (a / object_path).read_bytes()
            self.assertEqual(compressed, (b / object_path).read_bytes())
            self.assertEqual(len(compressed), entry['gzipBytes'])
            self.assertEqual(hashlib.sha256(compressed).hexdigest(), entry['gzipSha256'])
            unpacked = gzip.decompress(compressed)
            self.assertEqual(unpacked, (self.prepared / entry['path']).read_bytes())
            self.assertEqual(len(unpacked), entry['bytes'])
            self.assertEqual(hashlib.sha256(unpacked).hexdigest(), entry['sha256'])

    def test_existing_pack_is_never_modified(self):
        output = self.root / 'output'
        package(self.prepared, output)
        pointer = (output / 'current.json').read_bytes()
        with self.assertRaises(FileExistsError):
            package(self.prepared, output)
        self.assertEqual(pointer, (output / 'current.json').read_bytes())

    def test_unexpected_file_corruption_and_symlinks_rejected(self):
        csv = self.prepared / 'transactions/years.csv'
        old = csv.read_bytes()
        csv.write_bytes(old + b'corruption')
        with self.assertRaises(ValueError): package(self.prepared, self.root / 'corrupt')
        csv.write_bytes(old)
        extra = self.prepared / 'transactions.csv'
        extra.write_text('unexpected')
        with self.assertRaises(ValueError): package(self.prepared, self.root / 'extra')
        extra.unlink()
        csv.unlink()
        csv.symlink_to(self.root / 'source.csv')
        with self.assertRaises(ValueError): package(self.prepared, self.root / 'link')
        self.assertEqual([], list(self.root.glob('.corrupt-*')))
        self.assertEqual([], list(self.root.glob('.extra-*')))

    def test_bad_descriptor_path_or_version_rejected(self):
        descriptor = self.prepared / 'transaction-sources.json'
        value = json.loads(descriptor.read_text())
        value['sources'][0]['path'] = '../source.csv'
        descriptor.write_text(json.dumps(value))
        with self.assertRaises(ValueError): package(self.prepared, self.root / 'bad-path')
        value['schemaVersion'] = 'future'
        descriptor.write_text(json.dumps(value))
        with self.assertRaises(ValueError): package(self.prepared, self.root / 'bad-version')
