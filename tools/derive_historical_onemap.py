#!/usr/bin/env python3
"""Project the supplied historical D1 first-hit cache onto the frozen M4 cohort only."""
import argparse,csv,hashlib,json,re
from pathlib import Path
RAW_SHA='8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4'
SAMPLE_SHA='875aceb10230d4a52ea6159397cd40a69cbe81346094f6bb1a81423c6e1b5b76'
COLUMNS=['cache_key','search_value','postal_code','display_name','lat','lng','updated_at']
def normalize(value):return ' '.join(value.strip().upper().split())
def key(town,block,street):return re.sub('[^a-z0-9]+','-','-'.join(map(normalize,(town,block,street))).lower()).strip('-')
def derive(raw,sample,output):
 data=raw.read_bytes();sample_data=sample.read_bytes()
 if hashlib.sha256(data).hexdigest()!=RAW_SHA or hashlib.sha256(sample_data).hexdigest()!=SAMPLE_SHA:raise ValueError('Frozen raw/sample hash mismatch')
 reader=csv.DictReader(data.decode('utf-8-sig').splitlines());rows=list(reader)
 if reader.fieldnames!=COLUMNS or len(rows)!=10333 or len({r['cache_key'] for r in rows})!=len(rows):raise ValueError('Raw schema/count/duplicate mismatch')
 cache={r['cache_key']:r for r in rows};identities={}
 for t in csv.DictReader(sample_data.decode().splitlines()):
  k=key(t['town'],t['block'],t['street_name']);query=f"{normalize(t['block'])} {normalize(t['street_name'])} SINGAPORE";c=cache.get(k)
  status='MissingKey' if c is None else 'SearchMismatch' if c['search_value']!=query else 'InvalidPostal' if not re.fullmatch('[0-9]{6}',c['postal_code']) else 'HistoricalFirstHit'
  entry={'CacheKey':k,'SearchValue':query,'Status':status,'Postal':c['postal_code'] if c else None,'UpdatedAt':c['updated_at'] if c else None}
  if k in identities and identities[k]!=entry:raise ValueError('Colliding source identity')
  identities[k]=entry
 result={'Origin':'HistoricalOneMapFirstHit','RawSha256':RAW_SHA,'RawRows':len(rows),'SampleSha256':SAMPLE_SHA,'EvidenceStrength':'Page 1 results[0] only; returned block/road, other candidates and raw postal unavailable. Not exhaustive unique identity evidence.','Entries':[identities[k] for k in sorted(identities)]}
 output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(result,ensure_ascii=False,separators=(',',':'))+'\n')
 print(json.dumps({'identities':len(identities),'status_counts':{s:sum(e['Status']==s for e in identities.values()) for s in sorted({e['Status'] for e in identities.values()})},'derived_sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'coordinates_in_projection':False}))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--raw',type=Path,required=True);p.add_argument('--sample',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();derive(a.raw,a.sample,a.output)
