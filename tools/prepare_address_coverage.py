#!/usr/bin/env python3
"""Regenerate M9 address-only public evidence from complete hash-pinned snapshots."""
import argparse,csv,hashlib,json,shutil
from pathlib import Path
from sample_coverage import address,write_csv

def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
 return h.hexdigest()

def generate(base,source_directory,source_manifest,output,historical=None):
 manifest=json.loads(source_manifest.read_text())
 if not manifest.get('complete') or len(manifest['sources'])!=27:raise ValueError('Complete 27-partition manifest required.')
 baseline=json.loads((base/'manifest.json').read_text())
 for filename,expected in baseline['derived_sha256'].items():
  if sha(base/filename)!=expected:raise ValueError('Baseline hash mismatch: '+filename)
 if len({s['dataset_id'] for s in manifest['sources']})!=27:raise ValueError('Duplicate dataset IDs.')
 with (base/'transactions.csv').open(encoding='utf-8',newline='') as f:
  keys={address(r['block'],r['street_name']) for r in csv.DictReader(f)}
 if len(keys)!=9755:raise ValueError('Full corpus required.')
 selected=[];source_counts={}
 for source in manifest['sources']:
  path=source_directory/(source['letter']+'.csv')
  if sha(path)!=source['sha256']:raise ValueError('Source hash mismatch: '+source['letter'])
  count=0
  with path.open(encoding='utf-8-sig',newline='') as f:
   reader=csv.DictReader(f)
   for r in reader:
    count+=1
    if address(r['block'],r['street_name']) in keys:
     selected.append({'source_dataset':source['dataset_id'],'source_row':reader.line_num,**{k:r[k] for k in ('block','street_name','postal_code')}})
  if count!=source['source_rows']:raise ValueError('Source count mismatch: '+source['letter'])
  source_counts[source['dataset_id']]=count
 output.mkdir(parents=True,exist_ok=True)
 for filename in ['transactions.csv','address-evidence.csv','building-evidence.geojson']:shutil.copyfile(base/filename,output/filename)
 write_csv(output/'postal-address-evidence.csv',['source_dataset','source_row','block','street_name','postal_code'],selected)
 if sha(output/'postal-address-evidence.csv')!=manifest['projection_sha256']:raise ValueError('Projection hash mismatch.')
 files=['transactions.csv','address-evidence.csv','building-evidence.geojson','postal-address-evidence.csv']
 if historical is not None:
  if sha(historical)!='7d8af54d5cae591455e5161535218b66c9f85b9718d9a00d730602f72086ccd5':raise ValueError('Historical projection hash mismatch.')
  shutil.copyfile(historical,output/'historical-postal-evidence.json');files.append('historical-postal-evidence.json')
 elif (output/'historical-postal-evidence.json').exists():raise ValueError('Use clean output: unexpected historical evidence exists.')
 result={'algorithm':'hdb-m9-public-address-only-v1','normalization':'unchanged-m3-ave-ctrl-before-numeric-final-suffix','source_manifest_sha256':sha(source_manifest),'base_manifest_sha256':sha(base/'manifest.json'),'source_counts':source_counts,'postal_assertions':len(selected),'historical_projection':historical is not None,'derived_sha256':{name:sha(output/name) for name in files}}
 (output/'manifest.json').write_text(json.dumps(result,indent=2)+'\n')
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for n in ['base','source-directory','source-manifest','output']:p.add_argument('--'+n,type=Path,required=True)
 p.add_argument('--historical',type=Path);a=p.parse_args();print(json.dumps(generate(a.base,a.source_directory,a.source_manifest,a.output,a.historical),indent=2))
