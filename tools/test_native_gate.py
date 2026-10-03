import unittest
from native_gate import verify,STEPS
class NativeGateTests(unittest.TestCase):
 def log(self):return ''.join('debug: HDB_GATE_STEP '+s+'\n' for s in STEPS)+'debug: HDB_GATE_PASS\nHDB_GATE_EXIT\n'
 def test_ordered_transitions_and_clean_exit(self):verify(0,self.log())
 def test_runtime_mutation_crash_stale_and_teardown_failures_are_rejected(self):
  cases=[(0,self.log().replace('HDB_GATE_STEP selection-cleared','HDB_GATE_STEP stale-selection')),
   (-6,self.log()),(0,self.log().replace('HDB_GATE_EXIT','')),
   (0,self.log()+'warning: qrc:/Main.qml: TypeError: undefined model\n'),
   (0,self.log()+'critical: qrc:/Main.qml: Error: failed load\n'),
   (0,self.log()+'HDB_GATE_FAIL state timeout phase 5\n')]
  for code,log in cases:
   with self.subTest(code=code,log=log[-90:]):
    with self.assertRaises(RuntimeError):verify(code,log)
if __name__=='__main__':unittest.main()
