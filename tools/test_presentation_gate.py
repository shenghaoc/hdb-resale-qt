import unittest
from pathlib import Path
from presentation_gate import STEPS,verify
class PresentationGateTests(unittest.TestCase):
 def log(self):return ''.join('qml: HDB_PRESENTATION_STEP '+s+' ms=1\n' for s in STEPS)+'HDB_PRESENTATION_PASS\nHDB_GATE_EXIT\n'
 def test_all_ordered_states_and_teardown_required(self):
  verify(0,self.log())
  for code,log in [(1,self.log()),(0,self.log().replace('selection-offscreen','skipped')),(0,self.log().replace('HDB_GATE_EXIT','')),(0,self.log()+'HDB_GATE_FAIL projection mismatch')]:
   with self.subTest(code=code):
    with self.assertRaises(RuntimeError):verify(code,log)
 def test_viewport_oracle_is_independent_and_final_camera_is_asserted(self):
  root=Path(__file__).resolve().parents[1]
  gate=(root/'src/HdbResale.App/PresentationGate.qml').read_text()
  self.assertIn('targetMap.fromCoordinate',gate)
  self.assertIn('for (const key in expectedInView) if (!addresses[key]) return false',gate)
  self.assertIn('Resales.mapViewportZoom-targetMap.zoomLevel',gate)
  self.assertIn('townControl.currentIndex !== Resales.townIndex',gate)
  model=(root/'src/HdbResale.App/LocatedMapModel.cs').read_text()
  self.assertIn('"drop-presentation"',model)
if __name__=='__main__':unittest.main()
