import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from prepare_transaction_sources import prepare

HEADER = 'month,town,flat_type,block,street_name,resale_price'
ROW = '2015-01,T,3 ROOM,1,ST,400000'

class TransactionPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.evidence = self.root / 'evidence'
        self.evidence.mkdir()
        for name in ('address-evidence.csv', 'postal-address-evidence.csv', 'building-evidence.geojson'):
            (self.evidence / name).write_text('location evidence fixture\n')

    def tearDown(self):
        self.temp.cleanup()

    def source(self, name, text):
        path = self.root / (name + '.csv')
        path.write_text(text, encoding='utf-8', newline='')
        return (name, path)

    def test_ordered_headers_duplicates_raw_strings_and_determinism(self):
        sources = [self.source('absent', HEADER + '\n' + ROW + '\n' + ROW + '\n'),
                   self.source('blank', HEADER + ',remaining_lease\n' + ROW + ',\n'),
                   self.source('years', HEADER + ',remaining_lease,flat_model\n' + ROW + ',070,"Model,\n A "\n')]
        a, b = self.root / 'a', self.root / 'b'
        first = prepare(sources, self.evidence, a)
        self.assertEqual(first, prepare(sources, self.evidence, b))
        self.assertEqual((a / 'transaction-sources.json').read_bytes(), (b / 'transaction-sources.json').read_bytes())
        self.assertEqual(['absent', 'blank', 'years'], [source['sourceIdentity'] for source in first['sources']])
        for original, declaration in zip(sources, first['sources']):
            self.assertEqual(hashlib.sha256(original[1].read_bytes()).hexdigest(), declaration['rawSha256'])
            self.assertEqual(original[1].stat().st_size, declaration['rawBytes'])
            prepared = a / declaration['path']
            self.assertEqual(hashlib.sha256(prepared.read_bytes()).hexdigest(), declaration['sha256'])
            self.assertEqual(prepared.read_bytes(), (b / declaration['path']).read_bytes())
            with prepared.open(newline='') as file:
                rows = list(csv.DictReader(file))
            self.assertEqual(declaration['records'], len(rows))
            if original[0] == 'absent':
                self.assertNotIn('remaining_lease', rows[0])
                self.assertEqual(['2', '3'], [row['source_row'] for row in rows])
            elif original[0] == 'blank':
                self.assertEqual('', rows[0]['remaining_lease'])
            else:
                self.assertEqual('070', rows[0]['remaining_lease'])
                self.assertEqual('Model,\n A ', rows[0]['flat_model'])
                self.assertEqual('3', rows[0]['source_row'])

    def test_historical_evidence_is_excluded_while_canonical_evidence_and_marker_survive(self):
        (self.evidence / 'historical-postal-evidence.json').write_text('historical fixture, deliberately not parsed')
        (self.evidence / 'address-normalization.txt').write_text('terminal-road-types-v1\n')
        source = self.source('a', HEADER + '\n' + ROW + '\n')
        output = self.root / 'canonical'
        prepare([source], self.evidence, output)
        self.assertFalse((output / 'historical-postal-evidence.json').exists())
        self.assertTrue((self.evidence / 'historical-postal-evidence.json').exists())
        for name in ('address-evidence.csv', 'postal-address-evidence.csv', 'building-evidence.geojson', 'address-normalization.txt'):
            self.assertEqual((self.evidence / name).read_bytes(), (output / name).read_bytes())

    def test_invalid_identity_duplicate_and_case_collisions_fail_before_activation(self):
        source = self.source('good', HEADER + '\n' + ROW + '\n')
        for names in (['../bad'], ['/bad'], ['a/b'], ['good', 'good'], ['a', 'A']):
            with self.subTest(names=names), self.assertRaises(ValueError):
                prepare([(name, source[1]) for name in names], self.evidence, self.root / 'pack')
            self.assertFalse((self.root / 'pack').exists())

    def test_malformed_or_empty_later_source_leaves_no_partial_pack(self):
        a = self.source('a', HEADER + '\n' + ROW + '\n')
        for text in (HEADER + '\n', HEADER + '\nwrong,width\n', HEADER + '\n"unterminated\n', HEADER + ',month\n'):
            b = self.source('b', text)
            with self.subTest(text=text), self.assertRaises((ValueError, csv.Error)):
                prepare([a, b], self.evidence, self.root / 'pack')
            self.assertFalse((self.root / 'pack').exists())
            self.assertEqual([], list(self.root.glob('.pack-*')))

    def test_existing_pack_is_preserved_and_not_overwritten(self):
        source = self.source('a', HEADER + '\n' + ROW + '\n')
        output = self.root / 'pack'
        prepare([source], self.evidence, output)
        before = {str(file.relative_to(output)): file.read_bytes() for file in output.rglob('*') if file.is_file()}
        with self.assertRaises(FileExistsError):
            prepare([source], self.evidence, output)
        self.assertEqual(before, {str(file.relative_to(output)): file.read_bytes() for file in output.rglob('*') if file.is_file()})

    def test_copy_failure_cleans_owned_staging_and_lock(self):
        source = self.source('a', HEADER + '\n' + ROW + '\n')
        with patch('prepare_transaction_sources.shutil.copyfile', side_effect=OSError('interrupted')), self.assertRaises(OSError):
            prepare([source], self.evidence, self.root / 'pack')
        self.assertFalse((self.root / 'pack').exists())
        self.assertEqual([], list(self.root.glob('.pack*')))

    def test_concurrent_preparation_is_refused_without_removing_other_lock(self):
        source = self.source('a', HEADER + '\n' + ROW + '\n')
        lock = self.root / '.pack.prepare.lock'
        lock.write_text('other owner')
        with self.assertRaises(FileExistsError):
            prepare([source], self.evidence, self.root / 'pack')
        self.assertEqual('other owner', lock.read_text())

if __name__ == '__main__':
    unittest.main()
