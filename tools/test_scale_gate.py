import unittest
from pathlib import Path
from scale_gate import verify, STEPS, EXTENDED_STEPS, REENTRANT_STEPS
class ScaleGateTests(unittest.TestCase):
 def log(self):return ''.join('qml: HDB_SCALE_STEP '+s+' ms=1 rows=0 delegates=0\n' for s in STEPS)+'qml: HDB_SCALE_PASS\nHDB_GATE_EXIT\n'
 def test_reentrant_sequence_cannot_be_substituted_with_classic(self):
  log=''.join('qml: HDB_SCALE_STEP '+s+'\n' for s in REENTRANT_STEPS)+'HDB_SCALE_PASS\nHDB_GATE_EXIT\n'
  verify(0,log,reentrant=True)
  with self.assertRaises(RuntimeError):verify(0,log)
 def test_extended_sequence_cannot_be_substituted_with_classic(self):
  log=''.join('qml: HDB_SCALE_STEP '+s+'\n' for s in EXTENDED_STEPS)+'HDB_SCALE_PASS\nHDB_GATE_EXIT\n'
  verify(0,log,extended=True)
  with self.assertRaises(RuntimeError):verify(0,log)
 def test_ordered_transitions_and_clean_exit(self):verify(0,self.log())
 def test_crash_omitted_transition_stale_state_and_teardown_are_rejected(self):
  cases=[(1,self.log()),(0,self.log().replace('HDB_SCALE_STEP all-prices','HDB_SCALE_STEP wrong')),
   (0,self.log().replace('HDB_GATE_EXIT','')),(0,self.log()+'qml: HDB_GATE_FAIL scale phase 4\n'),
   (0,self.log()+'warning: qrc:/Main.qml: TypeError: stale model\n')]
  for code,log in cases:
   with self.subTest(code=code):
    with self.assertRaises(RuntimeError):verify(code,log)

class ModelLifetimeContractTests(unittest.TestCase):
 def test_qml_model_has_persistent_reference_and_gate_only_gc_regression(self):
  root=Path(__file__).resolve().parents[1]
  main=(root/'src/HdbResale.App/Main.qml').read_text()
  gate=(root/'src/HdbResale.App/ScaleGate.qml').read_text()
  self.assertIn('readonly property var locatedMapModel:',main)
  self.assertIn('window.locatedMapModel',main)
  self.assertIn('incubateDelegates: false',main)
  self.assertIn('drop-model-anchor',main)
  self.assertNotIn('gc()',main)
  self.assertIn('HDB_MODEL_LIFETIME_GC',gate)
  self.assertIn('gc()',gate)

if __name__=='__main__':unittest.main()
