#!/usr/bin/env python3
"""Native production presentation gate, with the existing explicitly scoped budgets."""
import argparse,json,os,re,subprocess,time
from pathlib import Path
from native_gate import FORBIDDEN
from scale_gate import validate_expanded_workload
STEPS=['loaded','full','town','different','budget','small-to-full','empty','empty-to-full','selection-only','individuals','selection-offscreen','selection-returned','domain-hidden-cleared','recentered','zoom-twelve','zoom-thirteen','zoom-fourteen','zoom-fifteen','before-burst','rapid-reentrant','rapid-viewport','reset']
def verify(code,output):
 if code or FORBIDDEN.search(output):raise RuntimeError(output)
 if re.findall(r'HDB_PRESENTATION_STEP ([a-z-]+)',output)!=STEPS or output.count('HDB_PRESENTATION_PASS')!=1 or output.count('HDB_GATE_EXIT')!=1:raise RuntimeError('Incomplete presentation transitions: '+output)
def run(executable,data,log,expanded=False):
 if expanded:validate_expanded_workload(data)
 env={**os.environ,'HDB_DATA_DIRECTORY':str(data.resolve()),'HDB_SCALE_GATE':'1','HDB_PRESENTATION_GATE':'1','HDB_SCALE_EXPANDED_COVERAGE':'1' if expanded else '0'}
 start=time.monotonic();deadline=60 if expanded else 45
 with subprocess.Popen([str(executable.resolve())],cwd=executable.resolve().parent,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True) as child:
  try:output,_=child.communicate(timeout=deadline)
  except subprocess.TimeoutExpired:
   child.kill();output,_=child.communicate();log.write_text(output);raise RuntimeError(f'Presentation gate exceeded bounded {deadline}-second process deadline.')
 log.write_text(output);verify(child.returncode,output)
 return {'seconds':round(time.monotonic()-start,2),'expanded_coverage':expanded,'state_budget_ms':10000 if expanded else 5000,'process_budget_seconds':deadline,'log':output}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for name in ('executable','data','log'):p.add_argument('--'+name,type=Path,required=True)
 p.add_argument('--expanded-coverage',action='store_true')
 a=p.parse_args();print(json.dumps(run(a.executable,a.data,a.log,a.expanded_coverage),indent=2))
