#!/usr/bin/env python3
"""Explicit offline preparation: public ACRA downloads, exact frozen hashes only.

No authentication, no retries, no runtime use. Current endpoints may have moved
on from these monthly snapshots; fail closed rather than substituting new data.
"""
import argparse,json,time,urllib.request
from pathlib import Path
from prepare_address_coverage import sha

def acquire(manifest_path,output):
 manifest=json.loads(manifest_path.read_text())
 if not manifest.get('complete') or len(manifest['sources'])!=27:raise ValueError('Complete pinned manifest required.')
 output.mkdir(parents=True,exist_ok=True)
 last_call=None
 for source in manifest['sources']:
  dataset=source['dataset_id'];expected_url='https://api-open.data.gov.sg/v1/public/api/datasets/'+dataset+'/poll-download'
  if source['download_api_url']!=expected_url:raise ValueError('Unexpected public API URL.')
  path=output/(source['letter']+'.csv')
  if path.exists():
   if sha(path)!=source['sha256']:raise ValueError('Existing source hash mismatch: '+source['letter'])
   continue
  # Public download API permits 2 requests/10 sec. No key or auth fallback.
  if last_call is not None:time.sleep(max(0,5.5-(time.monotonic()-last_call)))
  last_call=time.monotonic()
  with urllib.request.urlopen(expected_url,timeout=60) as response:result=json.load(response)
  if result.get('code')!=0 or result.get('data',{}).get('status')!='DOWNLOAD_SUCCESS':raise ValueError('Public download not ready; no retry performed.')
  url=result['data']['url']
  if not url.startswith('https://'):raise ValueError('HTTPS source download required.')
  temporary=path.with_suffix('.partial')
  try:
   with urllib.request.urlopen(url,timeout=240) as response,temporary.open('xb') as f:
    while chunk:=response.read(1024*1024):f.write(chunk)
   if sha(temporary)!=source['sha256']:raise ValueError('Current bytes differ from frozen source: '+source['letter'])
   temporary.replace(path)
  finally:
   if temporary.exists():temporary.unlink()
  print(source['letter']+' verified '+source['sha256'])
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();acquire(a.manifest,a.output)
