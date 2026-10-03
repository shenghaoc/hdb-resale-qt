#!/usr/bin/env python3
"""Generate ignored full-corpus inputs from the four frozen M4 official snapshots."""
import argparse,csv,hashlib,json
from pathlib import Path
from sample_coverage import SOURCES,address,write_csv

def generate(inputs,output,limit=None):
 for name,path in inputs.items():
  if hashlib.sha256(path.read_bytes()).hexdigest()!=SOURCES[name][1]:raise ValueError('Unrecognized snapshot: '+name)
 if limit is not None and limit<1:raise ValueError("Limit must be positive.")
 output.mkdir(parents=True,exist_ok=True)
 totals={name:0 for name in inputs}
 def records(name):
  path=inputs[name]
  with path.open(encoding='utf-8-sig',newline='') as f:
   reader=csv.DictReader(f)
   for r in reader:
    totals[name]+=1
    yield {'source_row':str(reader.line_num),**r}
 rows=list(records('resales'))
 if limit:rows=rows[:limit]
 keys={address(r['block'],r['street_name']) for r in rows}
 write_csv(output/'transactions.csv',list(rows[0]),rows)
 for name,fields,out in [('properties',['source_row','blk_no','street'],'address-evidence.csv'),('postals',['source_row','block','street_name','postal_code'],'postal-address-evidence.csv')]:
  block,street=('blk_no','street') if name=='properties' else ('block','street_name')
  selected=({k:r[k] for k in fields} for r in records(name) if address(r[block],r[street]) in keys)
  write_csv(output/out,fields,selected)
 # Use every official footprint; no historical cache or cached coordinates.
 building_bytes=inputs['buildings'].read_bytes()
 totals['buildings']=len(json.loads(building_bytes)['features'])
 (output/'building-evidence.geojson').write_bytes(building_bytes)
 manifest={'algorithm':'hdb-m6-source-order-v1','retrieved_utc':'2026-10-03','transactions':len(rows),'limit':limit,'source_counts':totals,'derived_bytes':{p.name:p.stat().st_size for p in sorted(output.iterdir()) if p.suffix in ('.csv','.geojson')},'sources':{k:{'dataset_id':v[0],'url':'https://data.gov.sg/datasets/'+v[0]+'/view','sha256':v[1]} for k,v in SOURCES.items()},'derived_sha256':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(output.iterdir()) if p.suffix in ('.csv','.geojson')}}
 (output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
 return manifest
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for name in SOURCES:p.add_argument('--'+name,type=Path,required=True)
 p.add_argument('--output',type=Path,required=True);p.add_argument('--limit',type=int);a=p.parse_args()
 print(json.dumps(generate({n:getattr(a,n) for n in SOURCES},a.output,a.limit),indent=2))
