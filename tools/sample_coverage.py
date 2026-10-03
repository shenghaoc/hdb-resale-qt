#!/usr/bin/env python3
"""Offline bounded study; pinned snapshots only, standard library, no retrieval."""
import argparse,csv,hashlib,json
from collections import defaultdict
from pathlib import Path

SEED='hdb-m4-coverage-v1'
PERIODS=('2017-2019','2020-2022','2023-2025','2026')
SOURCES={
 'resales':('d_8b84c4ee58e3cfc0ece0d773c8ca6abc','3c3d8bf9b12adb88919fa74869922fe05cdb144f5f69d93a0de4345d047cc7d4'),
 'properties':('d_17f5382f26140b1fdae0ba2ef6239d2f','a6e3105d02c4b59929371d1f7e4f8c0ed7939cdc081701d7e9197790bb78f4b1'),
 'postals':('d_3a3807c023c61ddfba947dc069eb53f2','a790f1a5082afcd6b5bad133f3949e3977e59cc8e33552897206b1e5f90cdab7'),
 'buildings':('d_16b157c52ed637edd6ba1232e026258d','7c987511548de3a82da403cabca02702031e02ade8703a9e401417883dfeb702')}
def digest(data): return hashlib.sha256(data).hexdigest()
def rank(value): return digest((SEED+'\0'+value).encode())
def period(month):
 year=int(month[:4])
 if 2017<=year<=2019:return PERIODS[0]
 if 2020<=year<=2022:return PERIODS[1]
 if 2023<=year<=2025:return PERIODS[2]
 if year==2026:return PERIODS[3]
 raise ValueError('Source outside pinned period range: '+month)
def address(block,street):
 t=street.upper().split()
 if len(t)>1 and t[-1].isascii() and t[-1].isdigit():
  t[-2]={'AVE':'AVENUE','CTRL':'CENTRAL'}.get(t[-2],t[-2])
 return block.strip().upper(),' '.join(t)
def select(rows):
 strata=defaultdict(list)
 for r in rows:strata[(r['town'],period(r['month']))].append(r)
 selected=[]
 for stratum,group in sorted(strata.items()):
  types=defaultdict(list)
  for r in sorted(group,key=lambda r:(rank(r['source_row']),int(r['source_row']))): types[r['flat_type']].append(r)
  type_order=sorted(types,key=lambda t:(rank('|'.join(stratum)+'|'+t),t))
  chosen=[];addresses=set()
  while len(chosen)<4:
   progress=False
   for flat in type_order:
    while types[flat]:
     r=types[flat].pop(0); key=(r['block'],r['street_name'])
     if key in addresses:continue
     chosen.append(r);addresses.add(key);progress=True;break
    if len(chosen)==4:break
   if not progress:break
  selected.extend(chosen)
 return sorted(selected,key=lambda r:int(r['source_row']))
def read_csv(path):
 with path.open(encoding='utf-8-sig',newline='') as f:
  reader=csv.DictReader(f);fields=reader.fieldnames;rows=[]
  for r in reader:
   r={'source_row':str(reader.line_num),**r};rows.append(r)
  return ['source_row',*fields],rows
def write_csv(path,fields,rows):
 with path.open('w',encoding='utf-8',newline='') as f:
  w=csv.DictWriter(f,fieldnames=fields,lineterminator='\n');w.writeheader();w.writerows(rows)
def generate(inputs,output):
 for name,path in inputs.items():
  if digest(path.read_bytes())!=SOURCES[name][1]:raise ValueError('Unrecognized snapshot: '+name)
 output.mkdir(parents=True,exist_ok=True)
 fields,resales=read_csv(inputs['resales']);sample=select(resales)
 keys={address(r['block'],r['street_name']) for r in sample}
 _,properties=read_csv(inputs['properties']);_,postals=read_csv(inputs['postals'])
 properties=[{k:r[k] for k in ('source_row','blk_no','street')} for r in properties if address(r['blk_no'],r['street']) in keys]
 postals=[{k:r[k] for k in ('source_row','block','street_name','postal_code')} for r in postals if address(r['block'],r['street_name']) in keys]
 postal_keys={(r['block'].strip().upper(),r['postal_code']) for r in postals}
 features=json.loads(inputs['buildings'].read_text())['features']
 selected_features=[f for f in features if (str(f['properties']['BLK_NO']).strip().upper(),str(f['properties']['POSTAL_COD'])) in postal_keys]
 write_csv(output/'transactions.csv',fields,sample)
 write_csv(output/'address-evidence.csv',['source_row','blk_no','street'],properties)
 write_csv(output/'postal-address-evidence.csv',['source_row','block','street_name','postal_code'],postals)
 (output/'building-evidence.geojson').write_text(json.dumps({'type':'FeatureCollection','features':selected_features},ensure_ascii=False,separators=(',',':'))+'\n')
 counts=lambda field:dict(sorted((v,sum(r[field]==v for r in sample)) for v in {r[field] for r in sample}))
 manifest={'algorithm':SEED,'selection':'Four address-distinct rows per town/period; SHA-256 seed+NUL+source_row rank within flat type; flat types seed-ranked per stratum and round-robin; final source-row ascending. No matching evidence consulted.',
  'retrieved_utc':'2026-10-03','source_count':len(resales),'sample_count':len(sample),'periods':list(PERIODS),
  'towns':counts('town'),'flat_types':counts('flat_type'),'unique_addresses':len(keys),'unique_blocks':len({r['block'] for r in sample}),'unique_streets':len({r['street_name'] for r in sample}),
  'evidence_counts':{'properties':len(properties),'postal_assertions':len(postals),'footprints':len(selected_features)},
  'sources':{n:{'dataset_id':v[0],'url':'https://data.gov.sg/datasets/'+v[0]+'/view','sha256':v[1]} for n,v in SOURCES.items()},
  'derived_sha256':{p.name:digest(p.read_bytes()) for p in sorted(output.iterdir()) if p.suffix in ('.csv','.geojson')}}
 (output/'manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n')
 return manifest
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for name in SOURCES:p.add_argument('--'+name,type=Path,required=True)
 p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 print(json.dumps(generate({n:getattr(a,n) for n in SOURCES},a.output),indent=2))
