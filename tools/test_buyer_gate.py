import copy,csv,json,tempfile,unittest
from pathlib import Path
from buyer_gate import expectation,verify,STEPS
from prepare_transaction_sources import prepare
class BuyerGateTests(unittest.TestCase):
 def test_canonical_csv_oracle_and_recent_order(self):
  es=expectation(Path(__file__).resolve().parents[1]/'data')
  self.assertEqual(STEPS,[e['name'] for e in es])
  self.assertEqual(6,es[0]['rows']);self.assertEqual(6,es[0]['addresses'])
  chosen=es[6]['selected'];self.assertEqual('ANG MO KIO|509|ANG MO KIO AVE 8',chosen['key'])
  self.assertEqual(390000,chosen['medianPrice']);self.assertEqual(81,chosen['minimumArea'])
  self.assertEqual('HDB-34',chosen['recent'][0]['id'])
  self.assertEqual('62 years 05 months',chosen['recent'][0]['lease'])
  self.assertEqual(24,len(es[6]['trend']['Points']));self.assertEqual(1,es[6]['trend']['Sales'])
  self.assertEqual(390000,next(p['MedianPrice']for p in es[6]['trend']['Points']if p['Count']))
  self.assertEqual(23,sum(p['MedianPrice']is None for p in es[6]['trend']['Points']))
  self.assertEqual(0,es[8]['rows']);self.assertIsNone(es[8]['selected']);self.assertIsNone(es[12]['selected'])
 def test_shard_oracle_preserves_null_and_bounds_provenance_to_recent_rows(self):
  base=Path(__file__).resolve().parents[1]/'data'
  with (base/'transactions.csv').open(newline='') as f: canonical=list(csv.DictReader(f))
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);sources=[]
   for name,present in [('absent',False),('years',True)]:
    fields=[k for k in canonical[0] if k!='source_row' and (present or k!='remaining_lease')]
    path=root/(name+'.csv')
    with path.open('w',newline='') as f:
     writer=csv.DictWriter(f,fields);writer.writeheader()
     for repeat in range(20):
      for row in canonical:
       values={k:row[k] for k in fields}
       if present:values['remaining_lease']='62'
       writer.writerow(values)
    sources.append((name,path))
   pack=root/'pack';prepare(sources,base,pack)
   es=expectation(pack)
   self.assertEqual(240,es[0]['rows'])
   for e in es:
    self.assertLessEqual(len(e['sourceEvidence']),15)
    self.assertEqual({r['id'] for r in e['selected']['recent']} if e['selected'] else set(),set(e['sourceEvidence']))
   self.assertEqual(40,es[6]['selected']['count'])
   self.assertEqual(15,len(es[6]['selected']['recent']))
   self.assertTrue(all(r['lease'] is None for r in es[6]['selected']['recent']))
   self.assertIsNotNone(es[6]['leaseMinimum'])
 def test_strict_runner_requires_every_step_and_teardown(self):
  good='\n'.join('HDB_BUYER_STEP '+s+' ms=1' for s in STEPS)+'\nHDB_BUYER_PASS\nHDB_GATE_EXIT\n'
  verify(0,good)
  for bad in [good.replace('HDB_GATE_EXIT',''),good.replace('HDB_BUYER_STEP budget','HDB_BUYER_STEP missing'),good+'\nTypeError\n',good+'\nHDB_BUYER_PASS\n']:
   with self.assertRaises(RuntimeError):verify(0,bad)
  with self.assertRaises(RuntimeError):verify(1,good)
if __name__=='__main__':unittest.main()
