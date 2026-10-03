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

class BuyerContractAuditTests(unittest.TestCase):
 def fixture(self):
  import hashlib
  from audit_reachable_history import M10,M11_DOMAIN_FILES,LEGACY_IMPORT_SHA
  blobs={name:('public source '+name).encode() for name in M11_DOMAIN_FILES}
  record={'contract_version':2,'base':M10,'legacy_import_sha256':LEGACY_IMPORT_SHA,
          'expanded_import_v2_sha256':'a'*64,'allowed_domain_sha256':{name:hashlib.sha256(data).hexdigest() for name,data in blobs.items()}}
  return blobs,record
 def test_narrow_evolution_preserves_all_other_protection(self):
  from audit_reachable_history import validate_buyer_contract,M11_DOMAIN_FILES
  blobs,record=self.fixture()
  self.assertEqual([],validate_buyer_contract(list(M11_DOMAIN_FILES),record,blobs.__getitem__))
  for name in ['data/transactions.csv','data/address-evidence.csv','docs/coverage/results.json','global.json','Directory.Build.props','src/HdbResale.Domain/AddressMatching.cs']:
   with self.subTest(name=name):self.assertTrue(validate_buyer_contract(list(M11_DOMAIN_FILES)+[name],record,blobs.__getitem__))
 def test_contract_must_pin_exact_files_actual_bytes_and_both_digests(self):
  import copy
  from audit_reachable_history import validate_buyer_contract,M11_DOMAIN_FILES,LEGACY_IMPORT_SHA
  blobs,record=self.fixture()
  for key,value in [('contract_version',1),('legacy_import_sha256','b'*64),('expanded_import_v2_sha256',LEGACY_IMPORT_SHA),('allowed_domain_sha256',{})]:
   altered=copy.deepcopy(record);altered[key]=value
   self.assertTrue(validate_buyer_contract(list(M11_DOMAIN_FILES),altered,blobs.__getitem__))
  changed=dict(blobs);changed[next(iter(changed))]=b'unreviewed change'
  self.assertTrue(validate_buyer_contract(list(M11_DOMAIN_FILES),record,changed.__getitem__))

if __name__=='__main__':unittest.main()
