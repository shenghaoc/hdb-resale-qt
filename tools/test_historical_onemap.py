"""Synthetic UNIT checks of historical web-key semantics and frozen input rejection."""
import unittest,tempfile
from pathlib import Path
from derive_historical_onemap import key,derive
class HistoricalProjectionTests(unittest.TestCase):
 def test_exact_key_not_alias_or_fuzzy(self):
  self.assertEqual('queenstown-81-c-wealth-cl',key(' queenstown ','81',"C'WEALTH   CL"))
  self.assertNotEqual(key('T','1','TEST AVE 1'),key('T','1','TEST AVENUE 1'))
 def test_wrong_raw_is_rejected_before_output(self):
  with tempfile.TemporaryDirectory() as d:
   raw=Path(d)/'raw.csv';sample=Path(d)/'sample.csv';out=Path(d)/'out.json';raw.write_text('synthetic wrong bytes');sample.write_text('synthetic wrong sample')
   with self.assertRaises(ValueError):derive(raw,sample,out)
   self.assertFalse(out.exists())
