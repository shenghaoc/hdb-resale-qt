#!/usr/bin/env python3
"""Independent full-source mechanical audit; cache geometry is audit-only, never an input to matching."""
import argparse,csv,hashlib,json,math,collections
from pathlib import Path
from derive_historical_onemap import RAW_SHA,key,normalize
PROPERTY_SHA='a6e3105d02c4b59929371d1f7e4f8c0ed7939cdc081701d7e9197790bb78f4b1'
FOOTPRINT_SHA='7c987511548de3a82da403cabca02702031e02ade8703a9e401417883dfeb702'
def street(value):
 t=normalize(value).split()
 if len(t)>1 and t[-1].isascii() and t[-1].isdigit():t[-2]={'AVE':'AVENUE','CTRL':'CENTRAL'}.get(t[-2],t[-2])
 return ' '.join(t)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def distance(a,b):
 lat1,lat2=map(math.radians,(a[0],b[0]));dl=math.radians(b[1]-a[1]);x=math.sin((lat2-lat1)/2)**2+math.cos(lat1)*math.cos(lat2)*math.sin(dl/2)**2;return 6371000*2*math.atan2(math.sqrt(x),math.sqrt(1-x))
def audit(report,raw,properties,footprints,output):
 if (sha(raw),sha(properties),sha(footprints))!=(RAW_SHA,PROPERTY_SHA,FOOTPRINT_SHA):raise ValueError('Pinned source hash mismatch')
 r=json.loads(report.read_text());cache={x['cache_key']:x for x in csv.DictReader(raw.open())};prop=collections.defaultdict(list);feet=collections.defaultdict(list)
 for i,x in enumerate(csv.DictReader(properties.open()),2):prop[(normalize(x['blk_no']),street(x['street']))].append(i)
 for f in json.loads(footprints.read_text())['features']:
  p=f['properties'];feet[(normalize(str(p.get('BLK_NO',''))),str(p.get('POSTAL_COD','')))].append(f)
 rows=[];checks=0;distances=[]
 for t in r['AllRows']:
  f=t['Facts'];e=t['Evidence'];h=e['HistoricalOneMap'];address=(normalize(f['Block']),street(f['Street']));changed=t['Before']!=t['ExperimentalAfter'] or t['BeforeCoordinates']!=t['ExperimentalCoordinates']
  if h['Status']=='HistoricalFirstHit':
   c=cache[h['CacheKey']];assert c['search_value']==h['SearchValue'] and c['postal_code']==h['Postal'] and c['updated_at']==h['UpdatedAt']
  if changed:
   assert t['Before']=='Unmatched' and t['ExperimentalAfter']=='NormalizedAddress' and len(prop[address])==1 and len(e['PostalAssertions'])==0
   fs=feet[(normalize(f['Block']),h['Postal'])];assert len(fs)==1 and len(e['FootprintCandidates'])==1
   fp=fs[0];assert fp['properties']['OBJECTID']==e['FootprintCandidates'][0]['Identity']['ObjectId'];ring=fp['geometry']['coordinates'][0]
   point=(round((min(p[1] for p in ring)+max(p[1] for p in ring))/2,10),round((min(p[0] for p in ring)+max(p[0] for p in ring))/2,10))
   actual=e['FootprintCandidates'][0]['Location']['Point'];assert point==(actual['Latitude'],actual['Longitude']);checks+=1
   d=distance(point,(float(c['lat']),float(c['lng'])));distances.append((d,t['Id'])) # Observation only; never selects/corrects identities.
  if t['Id']=='HDB-6769':
   assert t['ExperimentalAfter']=='Ambiguous' and len(e['PostalAssertions'])==74 and any(p['SourceRow']==60428 and p['PostalCode']=='530836' for p in e['PostalAssertions'])
  rows.append({'Id':t['Id'],'Town':f['Town'],'Month':f"{f['Month']['Year']:04}-{f['Month']['Month']:02}",'Block':f['Block'],'Street':f['Street'],'Before':t['Before'],'ExperimentalAfter':t['ExperimentalAfter'],'Coordinates':t['ExperimentalCoordinates'],'CacheKey':h['CacheKey'],'CacheStatus':h['Status'],'HistoricalPostal':h['Postal'],'AcraAssertions':len(e['PostalAssertions']),'AcraPostals':'|'.join(sorted({p['PostalCode'] for p in e['PostalAssertions']})),'FootprintObjectIds':'|'.join(str(p['Identity']['ObjectId']) for p in e['FootprintCandidates']),'ChangedMechanicalCheck':changed})
 assert len(rows)==416 and checks==340
 output.mkdir(parents=True,exist_ok=True)
 with (output/'rows.csv').open('w',newline='') as file:
  w=csv.DictWriter(file,fieldnames=list(rows[0]));w.writeheader();w.writerows(rows)
 summary={k:r[k] for k in ('EvidenceStrength','RawSha256','ProjectionSha256','CacheOutcomes','Transitions')}
 summary.update({'BaselineMatch':r['Baseline']['MatchQuality'],'ExperimentalMatch':r['ExperimentalPostalAssisted']['MatchQuality'],'BaselineCoordinates':r['Baseline']['CoordinateQuality'],'ExperimentalCoordinates':r['ExperimentalPostalAssisted']['CoordinateQuality'],'ExperimentalReasons':r['ExperimentalPostalAssisted']['Reasons'],'AllRows':416,'ChangedRowsMechanicalChecksPassed':checks,'ChangedIdentities':len({t['Evidence']['HistoricalOneMap']['CacheKey'] for t in r['AllRows'] if t['Before']!=t['ExperimentalAfter']}),'ReportSha256':sha(report),'RowsSha256':sha(output/'rows.csv'),'RawCoordinateAuditOnly':{'CheckedChangedRows':len(distances),'MaximumDistanceMeters':round(max(distances)[0],2),'MaximumDistanceRow':max(distances)[1],'RowsOver100Meters':sum(d>100 for d,_ in distances)},'RemainingUnmatchedIds':[t['Id'] for t in r['RemainingUnmatchedAudit']]})
 (output/'results.json').write_text(json.dumps(summary,ensure_ascii=False,separators=(',',':'))+'\n');print(json.dumps(summary))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for name in ('report','raw','properties','footprints','output'):p.add_argument('--'+name,type=Path,required=True)
 a=p.parse_args();audit(a.report,a.raw,a.properties,a.footprints,a.output)
