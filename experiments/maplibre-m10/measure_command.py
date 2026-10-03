#!/usr/bin/env python3
import json,resource,subprocess,sys,time
start=time.monotonic()
result=subprocess.run(sys.argv[1:])
r=resource.getrusage(resource.RUSAGE_CHILDREN)
print('M10_BUILD_RESOURCE '+json.dumps({'command':sys.argv[1:],'exitCode':result.returncode,'wallSeconds':time.monotonic()-start,'userSeconds':r.ru_utime,'systemSeconds':r.ru_stime,'peakChildRssKiB':r.ru_maxrss}),flush=True)
sys.exit(result.returncode)
