#!/usr/bin/env python3
"""Deterministic revised 32-row historical evidence audit sample, not a new resale cohort."""
import argparse,csv,hashlib,json,re,collections
from pathlib import Path
SEED='hdb-m5-revised-audit-v1'
def period(year):return '2017-2019' if year<=2019 else '2020-2022' if year<=2022 else '2023-2025' if year<=2025 else '2026'
def road_case(street):
 t=street.split()
 return 'NumericAVE' if len(t)>1 and t[-1].isdigit() and t[-2]=='AVE' else 'NumericCTRL' if len(t)>1 and t[-1].isdigit() and t[-2]=='CTRL' else 'OtherRoad'
def select(rows,ledger):
 pool=[t for t in rows if t['Before']=='Unmatched' and t['ExperimentalAfter']=='NormalizedAddress']
 def identity(t):return t['Evidence']['HistoricalOneMap']['CacheKey']
 def rank(t):return hashlib.sha256((SEED+'\0'+t['Id']).encode()).hexdigest()
 chosen=[];keys=set()
 def add(t):chosen.append(t);keys.add(identity(t))
 for town in sorted({t['Facts']['Town'] for t in pool}):add(min((t for t in pool if t['Facts']['Town']==town and identity(t) not in keys),key=rank))
 requirements=[('Period',p) for p in ('2017-2019','2020-2022','2023-2025','2026')]+[('RoadCase',c) for c in ('NumericAVE','NumericCTRL','OtherRoad')]+[('Suffix',True),('Punctuation',True),('Institution',True)]
 def value(t,kind):
  f=t['Facts'];flag=ledger[identity(t)]['FirstHitFlag']
  return period(f['Month']['Year']) if kind=='Period' else road_case(f['Street']) if kind=='RoadCase' else bool(re.fullmatch('[0-9]+[A-Z]+',f['Block'])) if kind=='Suffix' else bool(re.search('[^A-Z0-9 ]',f['Street'])) if kind=='Punctuation' else flag not in ('NamedFirstHitOnly','NoReturnedName')
 for kind,wanted in requirements:
  if not any(value(t,kind)==wanted for t in chosen):add(min((t for t in pool if identity(t) not in keys and value(t,kind)==wanted),key=rank))
 if len(chosen)>32:raise ValueError('Coverage requirements exceed sample budget')
 while len(chosen)<32:
  cells=collections.Counter((t['Facts']['Town'],period(t['Facts']['Month']['Year'])) for t in chosen);towns=collections.Counter(t['Facts']['Town'] for t in chosen)
  add(min((t for t in pool if identity(t) not in keys),key=lambda t:(cells[(t['Facts']['Town'],period(t['Facts']['Month']['Year']))],towns[t['Facts']['Town']],rank(t))))
 return sorted(chosen,key=lambda t:(t['Facts']['Town'],period(t['Facts']['Month']['Year']),t['Id']))
def sample(report,manual_ledger,output):
 r=json.loads(report.read_text())
 with manual_ledger.open(newline='') as file:ledger={x['CacheKey']:x for x in csv.DictReader(file)}
 chosen=select(r['AllRows'],ledger);rows=[]
 for t in chosen:
  f=t['Facts'];h=t['Evidence']['HistoricalOneMap'];l=ledger[h['CacheKey']]
  rows.append({'Id':t['Id'],'Town':f['Town'],'Period':period(f['Month']['Year']),'Block':f['Block'],'Street':f['Street'],'RoadCase':road_case(f['Street']),'BlockSuffix':bool(re.fullmatch('[0-9]+[A-Z]+',f['Block'])),'StreetPunctuation':bool(re.search('[^A-Z0-9 ]',f['Street'])),'CacheKey':h['CacheKey'],'HistoricalPostal':h['Postal'],'HdbPropertySourceRows':l['HdbPropertySourceRows'],'FootprintObjectId':l['FootprintObjectId'],'FootprintEntityId':l['FootprintEntityId'],'FirstHitFlag':l['FirstHitFlag'],'PreservedChainReview':l['PreservedChainReview'],'ReturnedIdentityReview':'Unverified','CandidateUniquenessReview':'Unverified'})
 output.parent.mkdir(parents=True,exist_ok=True)
 with output.open('w',newline='') as f:w=csv.DictWriter(f,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
 print(json.dumps({'seed':SEED,'rows':len(rows),'unique_addresses':len({t['CacheKey'] for t in rows}),'towns':len({t['Town'] for t in rows}),'periods':dict(collections.Counter(t['Period'] for t in rows)),'road_cases':dict(collections.Counter(t['RoadCase'] for t in rows)),'block_suffix_rows':sum(t['BlockSuffix'] for t in rows),'punctuation_rows':sum(t['StreetPunctuation'] for t in rows),'sha256':hashlib.sha256(output.read_bytes()).hexdigest()}))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for n in ('report','ledger','output'):p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();sample(a.report,a.ledger,a.output)
