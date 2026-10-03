import unittest
from audit_reachable_history import LOCAL,SECRET
class PublicationPatternTests(unittest.TestCase):
 def test_private_build_and_binary_paths_are_rejected(self):
  for path in ['.local/input.csv','tmp/private-onemap/x.json','src/bin/a.dll','experiments/build/output','cache/geocode-cache.csv','release.bundle','libqt.so.6']:
   with self.subTest(path=path):self.assertIsNotNone(LOCAL.search(path))
  for path in ['experiments/maplibre/main.cpp','experiments/large-map/retention.patch','docs/large-map/README.md','tools/build_probe.sh']:
   with self.subTest(path=path):self.assertIsNone(LOCAL.search(path))
 def test_credential_signatures_without_storing_a_credential(self):
  for value in ['gh'+'p_'+'X'*40,'github_'+'pat_'+'Y'*40,'sk-'+'proj-'+'Z'*40]:
   self.assertIsNotNone(SECRET.search(value))
  self.assertIsNone(SECRET.search('Public address counts:7618, transaction counts:241920'))
if __name__=='__main__':unittest.main()
