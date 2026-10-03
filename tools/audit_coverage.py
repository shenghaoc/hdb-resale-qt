#!/usr/bin/env python3
"""Evidence audit of every failure, plus fixed-ranked representatives; no matching changes."""
import argparse,json,csv,hashlib
from pathlib import Path
from sample_coverage import address,rank,period,SOURCES,digest

def audit(report,properties,postals,buildings):
 for name,path in [('properties',properties),('postals',postals),('buildings',buildings)]:
  if digest(path.read_bytes())!=SOURCES[name][1]:raise ValueError('Snapshot hash mismatch: '+name)
 def records(path):
  with path.open(encoding='utf-8-sig',newline='') as f:
   reader=csv.DictReader(f)
   return {reader.line_num:r for r in reader}
 props=records(properties);postal=records(postals)
 keys={address(r['block'],r['street_name']) for r in postal.values()}
 failure=json.loads(report.read_text())['Failures'];missing=[];conflicts=[]
 for f in failure:
  for p in f['Evidence']['PropertyCandidates']:
   raw=props[p['SourceRow']]
   assert raw['blk_no']==p['Block'] and raw['street']==p['Street']
  if f['Reason']=='MissingPostalCorroboration':
   assert address(f['Block'],f['Street']) not in keys;missing.append(f)
  elif f['Reason']=='ConflictingPostals':
   for a in f['Evidence']['PostalAssertions']:
    raw=postal[a['SourceRow']]
    assert all(raw[k]==a[v] for k,v in [('block','Block'),('street_name','Street'),('postal_code','PostalCode')])
   conflicts.append(f)
  else:raise ValueError('New failure reason requires explicit audit: '+f['Reason'])
 representatives=[]
 for p in ['2017-2019','2020-2022','2023-2025','2026']:
  group=sorted((f for f in missing if period(f['Month'])==p),key=lambda f:(rank(f['Id']),f['Id']))[:6]
  representatives.extend({'id':f['Id'],'month':f['Month'],'town':f['Town'],'block':f['Block'],'street':f['Street'],'property_rows':[p['SourceRow'] for p in f['Evidence']['PropertyCandidates']]} for f in group)
 shapes=json.loads(buildings.read_text())['features']
 return {'audited_failures':len(failure),'missing_under_current_normalization':len(missing),
  'all_failure_evidence_checked_against_full_sources':True,'manual_representative_rule':'six smallest SHA-256(seed+NUL+local ID) per period; 24 missing cases, plus every conflict',
  'representatives':representatives,'conflicts':[{'id':f['Id'],'assertion_count':len(f['Evidence']['PostalAssertions']),
    'postal_counts':{p:sum(a['PostalCode']==p for a in f['Evidence']['PostalAssertions']) for p in sorted({a['PostalCode'] for a in f['Evidence']['PostalAssertions']})},
    'footprints_at_conflicting_postals':[{'objectid':s['properties']['OBJECTID'],'block':s['properties']['BLK_NO'],'postal':s['properties']['POSTAL_COD']} for s in shapes if s['properties']['POSTAL_COD'] in {a['PostalCode'] for a in f['Evidence']['PostalAssertions']}]} for f in conflicts],
  'limit':'Absence is only under current conservative normalization in the pinned ACRA B snapshot. No wrong-address, historical-difference, broader-register absence or unapproved alias equivalence is proved.'}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for k in ['report','properties','postals','buildings','output']:p.add_argument('--'+k,type=Path,required=True)
 a=p.parse_args();result=audit(a.report,a.properties,a.postals,a.buildings)
 a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
