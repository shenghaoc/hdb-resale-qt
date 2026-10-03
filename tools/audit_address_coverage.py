#!/usr/bin/env python3
"""Independent complete-address source-chain checks and deterministic review sets."""
import argparse,csv,hashlib,json,re
from collections import defaultdict,Counter
from pathlib import Path
from address_coverage import key,source_features,summarize,digest,write_inventory
from sample_coverage import address,period

def producer(r):return re.sub('[^a-z0-9]+','-', '-'.join(' '.join(r[k].upper().split()) for k in ('Town','Block','Street')).lower()).strip('-')
def outcome(r):return (r['Quality'],r['Coordinates'],r['Location']['Point'],[f['Identity']['ObjectId'] for f in r['Evidence']['FootprintCandidates']])
def usable(r):
 h=r['Evidence'].get('HistoricalOneMap')
 return bool(h and h['Status']=='HistoricalFirstHit' and h['CacheKey']==producer(r) and h['SearchValue']==' '.join((r['Block']+' '+r['Street']+' SINGAPORE').upper().split()) and re.fullmatch('[0-9]{6}',h['Postal'] or ''))
def read(path):
 with path.open(encoding='utf-8-sig',newline='') as f:return list(csv.DictReader(f))
def verify_rows(rows,properties,postals,features,historical=None,normalize=address):
 pi=defaultdict(list);ai=defaultdict(list)
 for p in read(properties):pi[normalize(p['blk_no'],p['street'])].append({'SourceRow':int(p['source_row']),'Block':p['blk_no'],'Street':p['street']})
 for p in read(postals):ai[normalize(p['block'],p['street_name'])].append({'SourceRow':int(p['source_row']),'Block':p['block'],'Street':p['street_name'],'PostalCode':p['postal_code'],'SourceDataset':p['source_dataset']})
 for r in rows:
  k=normalize(r['Block'],r['Street']);e=r['Evidence'];p=pi[k];a=ai[k]
  assert e.get('HistoricalOneMap')==(historical or {}).get(producer(r)),('exact minimized historical assertion',key(r))
  assert e['PropertyCandidates']==p,('property assertions',key(r))
  assert e['PostalAssertions']==a,('complete postal assertions',key(r))
  codes={p['PostalCode'] for p in a};q=None;expected=[]
  if not p:q='Unmatched'
  elif len(p)>1:q='Ambiguous'
  elif any(not re.fullmatch('[0-9]{6}',s) for s in codes):q='Ambiguous'
  elif len(codes)>1:q='Ambiguous'
  elif usable(r) and codes and r['Evidence']['HistoricalOneMap']['Postal'] not in codes:q='Ambiguous'
  else:
   if usable(r):codes.add(r['Evidence']['HistoricalOneMap']['Postal'])
   if not codes:q='Unmatched'
   else:
    expected=[f for _,f in features[(k[0],next(iter(codes)))] if f['properties']['ENTITYID']>0]
    if not expected:q='Unmatched'
    elif len(expected)>1:q='Ambiguous'
    else:
     exact=not(usable(r) and not a) and all(x['Block']==r['Block'] and x['Street']==r['Street'] for x in p+a) and expected[0]['properties']['BLK_NO']==r['Block']
     q='ExactAddress' if exact else 'NormalizedAddress'
  assert r['Quality']==q,('quality',key(r),q,r['Quality'])
  assert [f['Identity']['ObjectId'] for f in e['FootprintCandidates']]==[f['properties']['OBJECTID'] for f in expected],('footprint candidates',key(r))
  point=None
  if q in ('ExactAddress','NormalizedAddress'):
   geom=expected[0]['geometry']
   if geom is not None:
    assert geom['type'] in ('Polygon','MultiPolygon')
    polygons=[geom['coordinates']] if geom['type']=='Polygon' else geom['coordinates']
    exterior=[v for poly in polygons for v in poly[0]]
    assert all(poly[0][0][:2]==poly[0][-1][:2] and len(poly[0])>=4 for poly in polygons)
    point={'Latitude':round(((min(v[1] for v in exterior)+max(v[1] for v in exterior))/2)*1e10)/1e10,'Longitude':round(((min(v[0] for v in exterior)+max(v[0] for v in exterior))/2)*1e10)/1e10}
  assert r['Location']['Point']==point,('independent footprint point',key(r),point,r['Location']['Point'])
  assert r['Coordinates']==('Missing' if point is None else 'BlockApproximation')
 return {'AddressesChecked':len(rows),'TransactionsChecked':sum(r['Transactions'] for r in rows),'CompletePropertyCandidates':True,'CompletePublicPostalCandidates':True,'RawMalformedAssertionsRetained':True,'IndependentOutcomeAndFootprintPoint':True,'ExactHistoricalProjectionAssertions':True}

def compact(before,after,kind):
 counts=defaultdict(lambda:defaultdict(list))
 for p in after['Evidence']['PostalAssertions']:counts[p['PostalCode']][p.get('SourceDataset','ACRA-B')].append(p['SourceRow'])
 return {'Town':after['Town'],'Block':after['Block'],'Street':after['Street'],'Transactions':after['Transactions'],'Before':before['Quality'],'After':after['Quality'],'BeforeCoordinates':before['Coordinates'],'AfterCoordinates':after['Coordinates'],'ReviewClass':kind,'PropertyRows':[p['SourceRow'] for p in after['Evidence']['PropertyCandidates']],'PostalCandidates':dict(counts),'Historical':after['Evidence'].get('HistoricalOneMap'),'Footprints':[p['Identity'] for p in after['Evidence']['FootprintCandidates']],'Point':after['Location']['Point']}
def write(path,rows):
 fields=['Town','Block','Street','Transactions','Before','After','BeforeCoordinates','AfterCoordinates','ReviewClass','PropertyRows','PostalCandidates','Historical','Footprints','Point']
 with path.open('w',encoding='utf-8',newline='') as f:
  w=csv.DictWriter(f,fields,lineterminator='\n');w.writeheader()
  for r in rows:w.writerow({k:json.dumps(v,separators=(',',':'),ensure_ascii=False) if isinstance(v,(list,dict)) else v for k,v in r.items()})
def run(baseline,public,final,directory,footprints,output,normalize=address):
 reports=[json.loads(p.read_bytes()) for p in [baseline,public,final]];bs,ps,fs=[{key(r):r for r in j['Rows']} for j in reports]
 assert bs.keys()==ps.keys()==fs.keys()
 features=source_features(footprints)
 historical_path=directory/'historical-postal-evidence.json'
 historical={h['CacheKey']:h for h in json.loads(historical_path.read_bytes())['Entries']} if historical_path.exists() else {}
 if historical_path.exists():assert digest(historical_path)=='7d8af54d5cae591455e5161535218b66c9f85b9718d9a00d730602f72086ccd5'
 verified=verify_rows(list(fs.values()),directory/'address-evidence.csv',directory/'postal-address-evidence.csv',features,historical,normalize)
 periods=defaultdict(set)
 for r in read(directory/'transactions.csv'):periods[(r['town'],r['block'],r['street_name'])].add(period(r['month']))
 transitions=defaultdict(lambda:Counter());coordinate_transitions=defaultdict(lambda:Counter());changed=[];required=[];ordinary=[];provenance=[];marginal=[]
 for k,a in fs.items():
  b=bs[k];p=ps[k]
  assert b['Ids']==p['Ids']==a['Ids'] and b['Transactions']==p['Transactions']==a['Transactions'],('unchanged transaction membership',k)
  transitions[b['Quality']+'→'+a['Quality']]['Addresses']+=1;transitions[b['Quality']+'→'+a['Quality']]['Transactions']+=a['Transactions']
  coordinate_transitions[b['Coordinates']+'→'+a['Coordinates']]['Addresses']+=1;coordinate_transitions[b['Coordinates']+'→'+a['Coordinates']]['Transactions']+=a['Transactions']
  changed_outcome=outcome(b)!=outcome(a)
  if outcome(p)!=outcome(a):marginal.append(compact(p,a,'HistoricalMarginal'))
  kind='OutcomeChange' if changed_outcome else 'ProvenanceOnly'
  if b['Evidence']!=a['Evidence'] or b['Location']!=a['Location']:changed.append(compact(b,a,kind))
  if not changed_outcome and b['Evidence']!=a['Evidence']:provenance.append(compact(b,a,kind))
  mandatory=a['Quality']=='Ambiguous' or len({x['PostalCode'] for x in a['Evidence']['PostalAssertions']})>1 or (b['Quality'] in ('ExactAddress','NormalizedAddress') and changed_outcome)
  if mandatory:required.append(compact(b,a,'ConflictOrPreviouslyMatchedOutcomeChange'))
  elif a['Coordinates']=='BlockApproximation' and changed_outcome:ordinary.append(compact(b,a,'NewLocatedHistorical' if usable(a) and not a['Evidence']['PostalAssertions'] else 'NewLocatedPublic'))
  elif b['Quality'] in ('ExactAddress','NormalizedAddress') and b['Evidence']!=a['Evidence']:ordinary.append(compact(b,a,'AgreeingProvenanceAddition'))
 strata=defaultdict(list)
 for r in ordinary:
  for t in periods[key(r)]:strata[(r['Town'],t,r['ReviewClass'])].append(r)
 chosen={}
 for stratum,group in sorted(strata.items()):
  r=min(group,key=lambda r:hashlib.sha256(('hdb-m9-manual-v1\0'+'|'.join(stratum)+'\0'+'|'.join(key(r))).encode()).hexdigest());chosen[key(r)]=r
 output.mkdir(parents=True,exist_ok=True)
 for name,rows in [('changed-addresses.csv',changed),('manual-required.csv',required),('manual-sample.csv',sorted(chosen.values(),key=key)),('historical-marginal.csv',marginal)]:write(output/name,rows)
 write_inventory([r for r in fs.values() if r['Coordinates']=='Missing'],features,output/'remaining-failures.csv')
 summary={'Algorithm':'hdb-m9-independent-audit-v1','Baseline':summarize(list(bs.values()),features),'PublicOnly':summarize(list(ps.values()),features),'PublicPlusHistorical':summarize(list(fs.values()),features),'Transitions':dict(transitions),'CoordinateTransitions':dict(coordinate_transitions),'Mechanical':verified,'ChangedEvidenceAddresses':len(changed),'ProvenanceOnlyAddresses':len(provenance),'OutcomeChangedAddresses':sum(outcome(bs[k])!=outcome(v) for k,v in fs.items()),'PreviouslyMatchedOutcomeChanges':sum(bs[k]['Quality'] in ('ExactAddress','NormalizedAddress') and outcome(bs[k])!=outcome(v) for k,v in fs.items()),'ManualRequiredAddresses':len(required),'ManualSampleAddresses':len(chosen),'HistoricalMarginalOutcomeAddresses':len(marginal),'HistoricalMarginalOutcomeTransactions':sum(r['Transactions'] for r in marginal),'ManualReviewStatus':'Required sets generated; not a claim of completed manual review.','SampleMethod':'SHA256 hdb-m9-manual-v1+NUL+town|period|class+NUL+town|block|street minimum per town/period/change-class, deduplicated addresses.','ReportSha256':{p.name:digest(p) for p in [baseline,public,final]},'ArtifactsSha256':{p.name:digest(p) for p in sorted(output.glob('*.csv'))}}
 (output/'comparison.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False)+'\n')
 return summary
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for n in ['baseline','public','final','directory','footprints','output']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();print(json.dumps(run(a.baseline,a.public,a.final,a.directory,a.footprints,a.output),indent=2,ensure_ascii=False))
