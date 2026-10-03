#!/usr/bin/env python3
"""Bounded Cocoa scale gate; logs C# construction/reset and QML readiness separately."""
import argparse,os,re,subprocess,time,json
from pathlib import Path
from native_gate import FORBIDDEN
STEPS=['loaded','filtered','budget-filtered','selected','empty-cleared','reset','zoomed','panned','recentered']
def run(executable,data,log):
 start=time.monotonic()
 env={**os.environ,'HDB_DATA_DIRECTORY':str(data.resolve()),'HDB_SCALE_GATE':'1'}
 with subprocess.Popen([str(executable.resolve())],cwd=executable.resolve().parent,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True) as child:
  try:output,_=child.communicate(timeout=25)
  except subprocess.TimeoutExpired:
   child.kill();output,_=child.communicate();log.write_text(output);raise RuntimeError('Scale gate exceeded bounded 25-second process deadline.')
 log.write_text(output)
 if child.returncode or FORBIDDEN.search(output):raise RuntimeError(output)
 if re.findall(r'HDB_SCALE_STEP ([a-z-]+)',output)!=STEPS or output.count('HDB_SCALE_PASS')!=1 or output.count('HDB_GATE_EXIT')!=1:raise RuntimeError('Incomplete scale transitions: '+output)
 return {'seconds':round(time.monotonic()-start,2),'log':output}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for name in ('executable','data','log'):p.add_argument('--'+name,type=Path,required=True)
 a=p.parse_args();print(json.dumps(run(a.executable,a.data,a.log),indent=2))
