#!/usr/bin/env python3
"""Run the native QML/Bridge interaction gate with bounded process teardown."""
import argparse,os,re,subprocess,sys
from pathlib import Path
STEPS=['loaded','filtered','selected','selection-cleared','remaining-selected','empty','reset','zoomed','panned','recentered']
FORBIDDEN=re.compile(r'TypeError|ReferenceError|Binding loop|Unable to assign|is not defined|HDB_GATE_FAIL|ASSERT|fatal|Aborted|QQmlApplicationEngine failed|Cannot assign|Cannot read property|Unhandled exception|(?:qrc:|\.qml).*Error',re.I)
def verify(code,log):
 if code!=0:raise RuntimeError('Native process failed: '+str(code)+'\n'+log)
 if FORBIDDEN.search(log):raise RuntimeError('Runtime error signature:\n'+log)
 observed=re.findall(r'HDB_GATE_STEP ([a-z-]+)',log)
 if observed!=STEPS:raise RuntimeError('Missing/out-of-order transitions: '+repr(observed)+'\n'+log)
 if log.count('HDB_GATE_PASS')!=1 or log.count('HDB_GATE_EXIT')!=1:raise RuntimeError('Missing pass/clean C# teardown:\n'+log)
def run(executable,output):
 env={**os.environ,'HDB_RUNTIME_GATE':'1','QT_MESSAGE_PATTERN':'%{type}: %{message}'}
 # Use the desktop's native platform (Cocoa on macOS, xcb/Wayland on Linux).
 # No offscreen provider or tile-render assertion.
 with subprocess.Popen([str(executable.resolve())],cwd=executable.resolve().parent,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True) as child:
  try:log,_=child.communicate(timeout=25)
  except subprocess.TimeoutExpired:
   child.kill();log,_=child.communicate();output.write_text(log)
   raise RuntimeError('Native gate process/state/teardown timeout:\n'+log)
 output.write_text(log);verify(child.returncode,log);return log
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--executable',type=Path,required=True);p.add_argument('--log',type=Path,required=True);a=p.parse_args()
 try:print(run(a.executable,a.log),end='')
 except (RuntimeError,OSError) as e:print(e,file=sys.stderr);sys.exit(1)
