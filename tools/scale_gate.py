#!/usr/bin/env python3
"""Bounded native desktop scale gate; logs C# construction/reset and QML readiness separately."""
import argparse,os,re,subprocess,time,json,hashlib
from pathlib import Path
from native_gate import FORBIDDEN
STEPS=['loaded','filtered','budget-filtered','selected','empty-cleared','reset','zoomed','panned','recentered','all-prices']
EXTENDED_STEPS=['loaded','full','subset','different','empty','reset','full-restored','selected','hidden-selection-cleared','full-again','repeat-subset','repeat-different','repeat-full','zoomed','panned','recentered']
REENTRANT_STEPS=['loaded','selected-retained-address','selected-in-town','hidden-transaction-cleared','reset','selected-for-burst','reentrant-burst-drained','final-reset']
def verify(code, output, extended=False, reentrant=False):
 if code or FORBIDDEN.search(output):raise RuntimeError(output)
 if re.findall(r'HDB_SCALE_STEP ([a-z-]+)',output)!=(REENTRANT_STEPS if reentrant else EXTENDED_STEPS if extended else STEPS) or output.count('HDB_SCALE_PASS')!=1 or output.count('HDB_GATE_EXIT')!=1:raise RuntimeError('Incomplete scale transitions: '+output)
def validate_expanded_workload(data):
 manifest=json.loads((data/'manifest.json').read_text())
 if manifest.get('normalization')!='terminal-road-types-v1' or (data/'address-normalization.txt').read_bytes()!=b'terminal-road-types-v1\n':
  raise ValueError('Expanded acceptance is restricted to the explicit M9 full-coverage profile.')
 pinned=json.loads((Path(__file__).resolve().parents[1]/'docs/scale/manifest.json').read_text())['derived_sha256']['transactions.csv']
 h=hashlib.sha256()
 with (data/'transactions.csv').open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 if h.hexdigest()!=pinned:raise ValueError('Expanded acceptance requires the unchanged pinned full transaction corpus.')
def run(executable,data,log,measurement=False,extended=False,reentrant=False,expanded_coverage=False):
 if expanded_coverage and measurement:raise ValueError("Choose expanded acceptance or diagnostic measurement, not both.")
 if expanded_coverage:validate_expanded_workload(data)
 start=time.monotonic()
 env={**os.environ,'HDB_DATA_DIRECTORY':str(data.resolve()),'HDB_SCALE_GATE':'1','HDB_SCALE_EXPANDED_COVERAGE':'1' if expanded_coverage else '0','HDB_MAP_MEASUREMENT':'1' if measurement else '0','HDB_MAP_TRANSITIONS':'1' if extended else '0','HDB_MAP_REENTRANT':'1' if reentrant else '0'}
 deadline=60 if (measurement or expanded_coverage) and extended else 45 if measurement or expanded_coverage or extended else 25
 with subprocess.Popen([str(executable.resolve())],cwd=executable.resolve().parent,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True) as child:
  try:output,_=child.communicate(timeout=deadline)
  except subprocess.TimeoutExpired:
   child.kill();output,_=child.communicate();log.write_text(output);raise RuntimeError(f'Scale gate exceeded bounded {deadline}-second process deadline.')
 log.write_text(output)
 verify(child.returncode,output,extended,reentrant)
 return {'seconds':round(time.monotonic()-start,2),'measurement_only':measurement,'expanded_coverage':expanded_coverage,'state_budget_ms':10000 if measurement or expanded_coverage else 5000,'process_budget_seconds':deadline,'extended':extended,'reentrant':reentrant,'log':output}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for name in ('executable','data','log'):p.add_argument('--'+name,type=Path,required=True)
 p.add_argument('--measurement',action='store_true',help='Diagnostic only: 10-second states/45-second process. Does not pass the ordinary acceptance deadline.')
 p.add_argument('--expanded-coverage',action='store_true',help='Explicit pinned full-corpus acceptance only: 10-second states, 60-second extended or 45-second classic/reentrant process. Original default gates remain 5 seconds.')
 p.add_argument('--extended',action='store_true',help='Sixteen repeated/retained-identity transitions, same 5-second state bounds; 45-second process.')
 p.add_argument('--reentrant',action='store_true',help='Inject a four-intent burst during BeginRemoveRows and verify retained-address selection clearing; 5/25-second bounds.')
 a=p.parse_args()
 if a.extended and a.reentrant:p.error('Choose one scenario: --extended or --reentrant.')
 print(json.dumps(run(a.executable,a.data,a.log,a.measurement,a.extended,a.reentrant,a.expanded_coverage),indent=2))
