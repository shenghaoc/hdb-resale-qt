"""Offline audit selection checks against the unchanged 416-row benchmark metadata."""
import csv,unittest
from pathlib import Path
from sample_historical_audit import select,period,road_case
class RevisedHistoricalAuditTests(unittest.TestCase):
 def test_order_independent_unique_stratified_selection(self):
  root=Path(__file__).resolve().parents[1];base=root/'docs/coverage/onemap/historical'
  with (base/'manual-review.csv').open(newline='') as file:ledger={x['CacheKey']:x for x in csv.DictReader(file)}
  rows=[]
  with (base/'rows.csv').open(newline='') as file:
   for t in csv.DictReader(file):
    rows.append({'Id':t['Id'],'Before':t['Before'],'ExperimentalAfter':t['ExperimentalAfter'],'Facts':{'Town':t['Town'],'Month':{'Year':int(t['Month'][:4])},'Block':t['Block'],'Street':t['Street']},'Evidence':{'HistoricalOneMap':{'CacheKey':t['CacheKey']}}})
  chosen=select(rows,ledger);reverse=select(list(reversed(rows)),ledger)
  self.assertEqual([t['Id'] for t in chosen],[t['Id'] for t in reverse]);self.assertEqual(32,len(chosen))
  self.assertEqual(32,len({t['Evidence']['HistoricalOneMap']['CacheKey'] for t in chosen}));self.assertEqual(26,len({t['Facts']['Town'] for t in chosen}))
  self.assertEqual({'2017-2019','2020-2022','2023-2025','2026'},{period(t['Facts']['Month']['Year']) for t in chosen})
  self.assertEqual({'NumericAVE','NumericCTRL','OtherRoad'},{road_case(t['Facts']['Street']) for t in chosen})
  self.assertTrue(all(t['Before']=='Unmatched' and t['ExperimentalAfter']=='NormalizedAddress' for t in chosen))
