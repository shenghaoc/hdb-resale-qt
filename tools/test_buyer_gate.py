import copy,json,unittest
from pathlib import Path
from buyer_gate import expectation,verify,STEPS
class BuyerGateTests(unittest.TestCase):
 def test_canonical_csv_oracle_and_recent_order(self):
  es=expectation(Path(__file__).resolve().parents[1]/'data')
  self.assertEqual(STEPS,[e['name'] for e in es])
  self.assertEqual(6,es[0]['rows']);self.assertEqual(6,es[0]['addresses'])
  chosen=es[6]['selected'];self.assertEqual('ANG MO KIO|509|ANG MO KIO AVE 8',chosen['key'])
  self.assertEqual(390000,chosen['medianPrice']);self.assertEqual(81,chosen['minimumArea'])
  self.assertEqual('HDB-34',chosen['recent'][0]['id'])
  self.assertEqual('62 years 05 months',chosen['recent'][0]['lease'])
  self.assertEqual(0,es[8]['rows']);self.assertIsNone(es[8]['selected']);self.assertIsNone(es[12]['selected'])
 def test_strict_runner_requires_every_step_and_teardown(self):
  good='\n'.join('HDB_BUYER_STEP '+s+' ms=1' for s in STEPS)+'\nHDB_BUYER_PASS\nHDB_GATE_EXIT\n'
  verify(0,good)
  for bad in [good.replace('HDB_GATE_EXIT',''),good.replace('HDB_BUYER_STEP budget','HDB_BUYER_STEP missing'),good+'\nTypeError\n',good+'\nHDB_BUYER_PASS\n']:
   with self.assertRaises(RuntimeError):verify(0,bad)
  with self.assertRaises(RuntimeError):verify(1,good)
if __name__=='__main__':unittest.main()
