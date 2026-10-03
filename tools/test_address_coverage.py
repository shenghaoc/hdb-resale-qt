import csv,json,tempfile,unittest
from pathlib import Path
from address_coverage import strongest,summarize
from prepare_address_coverage import generate,sha

class AddressCoverageTests(unittest.TestCase):
 def test_failure_reason_requires_exact_asserted_postal_and_preserves_entity_rule(self):
  from collections import defaultdict
  features=defaultdict(list)
  features[('1','123456')]=[(1,{'properties':{'ENTITYID':0},'geometry':{'type':'Polygon'}})]
  row={'Block':'1','Reason':'MissingFootprint','Evidence':{'PostalAssertions':[{'PostalCode':'123456'}]}}
  self.assertEqual('SourceFootprintRejectedNonpositiveEntityId',strongest(row,features))
  row['Evidence']['PostalAssertions'][0]['PostalCode']='654321'
  self.assertEqual('NoSourceFootprintForBlockPostal',strongest(row,features))
  self.assertEqual([],features[('1','654321')])
 def test_address_and_transaction_denominators_are_separate(self):
  rows=[{'Quality':'Ambiguous','Coordinates':'Missing','Reason':'ConflictingPostals','Transactions':91},
        {'Quality':'Unmatched','Coordinates':'Missing','Reason':'MissingProperty','Transactions':3}]
  result=summarize(rows,{})
  self.assertEqual(2,result['Addresses']);self.assertEqual(94,result['Transactions'])
  self.assertEqual({'Addresses':1,'Transactions':91},result['MatchQuality']['Ambiguous'])
 def test_complete_projection_keeps_duplicates_conflicts_and_malformed_raw_postals(self):
  with tempfile.TemporaryDirectory() as d:
   root=Path(d);base=root/'base';sources=root/'sources';output=root/'out';base.mkdir();sources.mkdir()
   (base/'transactions.csv').write_text('source_row,block,street_name\n'+''.join(f'{n+2},{n},ST\n' for n in range(9755)))
   (base/'address-evidence.csv').write_text('source_row,blk_no,street\n2,1,ST\n')
   (base/'postal-address-evidence.csv').write_text('source_row,block,street_name,postal_code\n')
   (base/'building-evidence.geojson').write_text('{"type":"FeatureCollection","features":[]}')
   (base/'manifest.json').write_text(json.dumps({'derived_sha256':{p.name:sha(p) for p in base.iterdir()}}))
   entries=[];selected=[]
   for i in range(27):
    label=str(i);dataset='d_'+f'{i:032x}';path=sources/(label+'.csv')
    path.write_text('block,street_name,postal_code,excluded\n1,ST,123456,private field\n1,ST,123456,other\n1,ST,1234,other\n1,ST,654321,other\n')
    entries.append({'letter':label,'dataset_id':dataset,'sha256':sha(path),'source_rows':4})
    for n,postal in enumerate(['123456','123456','1234','654321'],2):selected.append({'source_dataset':dataset,'source_row':n,'block':'1','street_name':'ST','postal_code':postal})
   expected=root/'expected.csv'
   from sample_coverage import write_csv
   write_csv(expected,['source_dataset','source_row','block','street_name','postal_code'],selected)
   manifest=root/'manifest.json';manifest.write_text(json.dumps({'complete':True,'sources':entries,'projection_sha256':sha(expected)}))
   result=generate(base,sources,manifest,output)
   self.assertEqual(108,result['postal_assertions']);self.assertEqual(expected.read_bytes(),(output/'postal-address-evidence.csv').read_bytes())
   self.assertNotIn('private field',(output/'postal-address-evidence.csv').read_text())
   (sources/'0.csv').write_text('tampered')
   with self.assertRaisesRegex(ValueError,'Source hash mismatch'):generate(base,sources,manifest,root/'bad')
if __name__=='__main__':unittest.main()
