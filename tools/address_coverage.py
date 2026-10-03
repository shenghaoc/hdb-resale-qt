#!/usr/bin/env python3
"""Deterministic full-address inventory from the actual C# matcher report.

No network or address inference. Rejected source features refine the runtime
failure reason only after an exact block + asserted-postal match.
"""
import argparse,csv,hashlib,json
from collections import Counter,defaultdict
from pathlib import Path

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def key(r):return r['Town'],r['Block'],r['Street']
def source_features(path):
 out=defaultdict(list)
 for n,f in enumerate(json.loads(path.read_bytes())['features'],1):
  p=f['properties'];out[(str(p.get('BLK_NO','')).strip().upper(),p.get('POSTAL_COD'))].append((n,f))
 return out

def strongest(row,features):
 reason=row['Reason']
 if reason=='MissingPostalCorroboration':return 'NoPostalAssertionUnderCurrentNormalization'
 if reason=='MissingProperty':return 'NoPropertyUnderCurrentNormalization'
 if reason!='MissingFootprint':return reason
 postals={p['PostalCode'] for p in row['Evidence']['PostalAssertions']}
 historical=row['Evidence'].get('HistoricalOneMap')
 if historical and historical['Status']=='HistoricalFirstHit':postals.add(historical['Postal'])
 fs=[(n,f) for p in postals for n,f in features[(row['Block'].strip().upper(),p)]]
 if not fs:return 'NoSourceFootprintForBlockPostal'
 if all(f['properties'].get('ENTITYID',0)<=0 for _,f in fs):return 'SourceFootprintRejectedNonpositiveEntityId'
 if all(f['geometry'] and f['geometry']['type']=='MultiPolygon' for _,f in fs):return 'SourceFootprintUnsupportedMultiPolygon'
 return 'SourceFootprintRejectedOther'

def summarize(rows,features):
 result={'Addresses':len(rows),'Transactions':sum(r['Transactions'] for r in rows)}
 for name,get in [('MatchQuality',lambda r:r['Quality']),('CoordinateQuality',lambda r:r['Coordinates']),('StrongestReason',lambda r:strongest(r,features))]:
  result[name]={v:{'Addresses':sum(get(r)==v for r in rows),'Transactions':sum(r['Transactions'] for r in rows if get(r)==v)} for v in sorted({get(r) for r in rows})}
 return result

def write_inventory(rows,features,path):
 fields=['town','block','street','transactions','match_quality','coordinate_quality','strongest_evidenced_reason','property_source_rows','postal_assertions','accepted_footprint_objectids','exact_source_footprint_candidates']
 with path.open('w',newline='',encoding='utf-8') as f:
  w=csv.DictWriter(f,fields,lineterminator='\n');w.writeheader()
  for r in sorted(rows,key=key):
   e=r['Evidence'];postals={p['PostalCode'] for p in e['PostalAssertions']}
   if (h:=e.get('HistoricalOneMap')) and h['Status']=='HistoricalFirstHit':postals.add(h['Postal'])
   candidates=[{'feature':n,'objectid':x['properties']['OBJECTID'],'entityid':x['properties']['ENTITYID'],'postal':x['properties']['POSTAL_COD'],'geometry':x['geometry']['type'] if x['geometry'] else None} for p in sorted(postals) for n,x in features[(r['Block'].strip().upper(),p)]]
   w.writerow(dict(zip(fields,[r['Town'],r['Block'],r['Street'],r['Transactions'],r['Quality'],r['Coordinates'],strongest(r,features),';'.join(str(p['SourceRow']) for p in e['PropertyCandidates']),json.dumps(e['PostalAssertions'],separators=(',',':')),','.join(str(p['Identity']['ObjectId']) for p in e['FootprintCandidates']),json.dumps(candidates,separators=(',',':'))])))

def baseline(report,footprints,output):
 data=json.loads(report.read_bytes());rows=data['Rows'];features=source_features(footprints)
 if len({key(r) for r in rows})!=len(rows):raise ValueError('Duplicate raw address identities.')
 if sum(r['Transactions'] for r in rows)!=data['Transactions']:raise ValueError('Transactions not conserved.')
 output.mkdir(parents=True,exist_ok=True)
 write_inventory([r for r in rows if r['Coordinates']=='Missing'],features,output/'baseline-failures.csv')
 result={'Algorithm':'hdb-m9-address-coverage-v1','BaselineCommit':'a27657099623c211bf207e1e4cc4b057ead4c953','ReportSha256':digest(report),'FootprintsSha256':digest(footprints),'Summary':summarize(rows,features),'FailureInventorySha256':digest(output/'baseline-failures.csv'),'Diagnostics':data['Diagnostics'],'ReasonPrecedence':'Runtime property, postal conflict/corroboration, footprint multiplicity/geometry; failed footprint additionally classified by exact block+asserted postal in the complete source. No evidence guessed from block alone.'}
 (output/'baseline.json').write_text(json.dumps(result,indent=2)+'\n')
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--baseline',type=Path,required=True);p.add_argument('--footprints',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 print(json.dumps(baseline(a.baseline,a.footprints,a.output),indent=2))
