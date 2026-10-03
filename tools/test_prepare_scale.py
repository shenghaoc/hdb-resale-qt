import hashlib,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import prepare_scale
class ScalePreparationTests(unittest.TestCase):
 def test_pinned_source_order_and_minimized_projection_are_reproducible(self):
  with tempfile.TemporaryDirectory() as root:
   root=Path(root)
   text={'resales':'month,town,flat_type,block,street_name,resale_price\n2024-01,T,3 ROOM,1,TEST AVE 1,100\n2024-02,T,4 ROOM,1,TEST AVE 1,200\n','properties':'blk_no,street,other\n1,TEST AVENUE 1,excluded\n','postals':'block,street_name,postal_code,company\n1,TEST AVENUE 1,123456,excluded\n','buildings':'{"type":"FeatureCollection","features":[]}\n'}
   inputs={}
   for k,v in text.items():inputs[k]=root/k;inputs[k].write_text(v)
   pins={k:('synthetic',hashlib.sha256(p.read_bytes()).hexdigest()) for k,p in inputs.items()}
   with patch.object(prepare_scale,'SOURCES',pins):
    a=prepare_scale.generate(inputs,root/'a');b=prepare_scale.generate(inputs,root/'b')
    self.assertEqual(a,b);self.assertEqual(2,a['transactions'])
    self.assertNotIn('company',(root/'a'/'postal-address-evidence.csv').read_text())
    self.assertNotIn('excluded',(root/'a'/'postal-address-evidence.csv').read_text())
    for name in a['derived_sha256']:self.assertEqual((root/'a'/name).read_bytes(),(root/'b'/name).read_bytes())
    c=prepare_scale.generate(inputs,root/'c',1);self.assertEqual(1,c['transactions'])
    inputs['resales'].write_text('changed')
    with self.assertRaisesRegex(ValueError,'Unrecognized'):prepare_scale.generate(inputs,root/'bad')
    self.assertFalse((root/'bad').exists())
