import unittest
from sample_coverage import select,period,rank,address
class SamplingTests(unittest.TestCase):
 def row(self,n,flat,block,month='2017-01',town='T'):
  return {'source_row':str(n),'town':town,'month':month,'flat_type':flat,'block':block,'street_name':'TEST AVE 1'}
 def test_rank_encoding_and_period_boundaries(self):
  import hashlib
  self.assertEqual(rank('123'),hashlib.sha256(b'hdb-m4-coverage-v1\x00123').hexdigest())
  self.assertEqual([period(s) for s in ['2017-01','2019-12','2020-01','2022-12','2023-01','2025-12','2026-01']],['2017-2019']*2+['2020-2022']*2+['2023-2025']*2+['2026'])
  with self.assertRaises(ValueError):period('2027-01')
 def test_selection_is_order_independent_distinct_and_stratified(self):
  rows=[self.row(i,('2 ROOM','3 ROOM','4 ROOM')[i%3],str(i%7),m,t) for i in range(2,32) for m in ['2017-01','2026-02'] for t in ['A','B']]
  a=select(rows);self.assertEqual(a,select(list(reversed(rows))))
  for t in ['A','B']:
   for m in ['2017-01','2026-02']:
    group=[r for r in a if r['town']==t and r['month']==m]
    self.assertEqual(4,len(group));self.assertEqual(4,len({(r['block'],r['street_name']) for r in group}));self.assertEqual(3,len({r['flat_type'] for r in group}))
  # Selection never reads location or matchability evidence.
  altered=[{**r,'match':'bad','coordinates':None} for r in rows]
  self.assertEqual([r['source_row'] for r in a],[r['source_row'] for r in select(altered)])
 def test_source_address_normalization_preserves_distinctions(self):
  self.assertEqual(address(' 509a ','ANG MO KIO AVE 8'),('509A','ANG MO KIO AVENUE 8'))
  self.assertNotEqual(address('0509','ST 1'),address('509','STREET 1'))
  self.assertNotEqual(address('1','AVE. 8'),address('1','AVENUE 8'))
if __name__=='__main__':unittest.main()
