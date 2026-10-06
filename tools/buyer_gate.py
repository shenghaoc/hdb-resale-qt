#!/usr/bin/env python3
"""Native buyer flow with independently computed CSV cohorts/medians/recent rows."""
import argparse,csv,json,os,re,subprocess,time,tempfile
from decimal import Decimal, ROUND_CEILING
from pathlib import Path
from native_gate import FORBIDDEN
from scale_gate import validate_expanded_workload
STEPS=['loaded','full','town','type','minimum','budget','list-selected','recent-window','empty','reset','address-detail','map-selected','hidden-address','reentry','viewport-burst']
def median(values):
 values=sorted(values);n=len(values);return None if not n else (values[(n-1)//2]+values[n//2])/2

def expectation(data):
 rows=[]
 manifest_path=data/'transaction-sources.json'
 sources=json.loads(manifest_path.read_text())['sources'] if manifest_path.is_file() else [dict(path='transactions.csv')]
 for source in sources:
  with (data/source['path']).open(newline='',encoding='utf-8-sig') as f:
   for row in csv.DictReader(f):
    row['id']='HDB-'+(source['sourceIdentity']+'-' if 'sourceIdentity' in source else '')+str(int(row['source_row']))
    if 'sourceIdentity' in source:row['_source']=source
    rows.append(row)
 def month(r):return int(r['month'][:4])*12+int(r['month'][5:])-1
 def key(r):return '|'.join(r[x] for x in ('town','block','street_name'))
 for r in rows:
  r['p']=Decimal(r['resale_price']);r['a']=Decimal(r['floor_area_sqm']);r['q']=r['p']/r['a']
 latest=max(r['month'] for r in rows);end=max(map(month,rows));maximum=int(max(Decimal(1000000),max(r['p'] for r in rows)))
 town=rows[0]['town'];other=sorted({r['town'] for r in rows}-{town})[0]
 chosen=next((key(r) for r in rows if r['town']==town and r['block']=='509' and r['flat_type']=='3 ROOM'),key(rows[0]))
 # Numeric values stay unrounded until JSON transport; comparison tolerance is
 # only for decimal -> ECMAScript double conversion, never display rounding.
 def summary(rs,k):
  g=[r for r in rs if key(r)==k]
  if not g:return None
  recent=sorted(g,key=lambda r:(-month(r),r['flat_type'],r['storey_range'],r['a'],r['p'],r['q'],r['flat_model'],(r.get('remaining_lease') is not None,r.get('remaining_lease') or ''),r['id']))[:15]
  return dict(key=k,address=g[0]['block']+' '+g[0]['street_name'],town=g[0]['town'],count=len(g),latest=max(r['month'] for r in g),minimumPrice=min(r['p'] for r in g),maximumPrice=max(r['p'] for r in g),medianPrice=median([r['p'] for r in g]),medianPricePerSqm=median([r['q'] for r in g]),minimumArea=min(r['a'] for r in g),maximumArea=max(r['a'] for r in g),types=sorted({r['flat_type'] for r in g}),leaseYears=sorted({int(r['lease_commence_date']) for r in g}),recent=[dict(id=r['id'],month=r['month'],type=r['flat_type'],storey=r['storey_range'],area=r['a'],price=r['p'],pricePerSqm=r['q'],model=r['flat_model'],lease=r.get('remaining_lease'),leaseYear=int(r['lease_commence_date'])) for r in recent])
 filters=[('All towns','All flat types',0,1000000,0),('All towns','All flat types',0,maximum,0),(town,'All flat types',0,maximum,0),(town,'3 ROOM',0,maximum,0),(town,'3 ROOM',300000,maximum,0),(town,'3 ROOM',300000,700000,0),(town,'3 ROOM',300000,700000,0),(town,'3 ROOM',300000,700000,12),(town,'3 ROOM',300000,0,12),('All towns','All flat types',0,1000000,0),('All towns','All flat types',0,maximum,0),('All towns','All flat types',0,maximum,0),(other,'All flat types',0,maximum,0),('All towns','All flat types',0,maximum,0),('All towns','All flat types',0,maximum,0)]
 result=[]
 for i,(t,typ,lo,hi,months) in enumerate(filters):
  g=[r for r in rows if(t=='All towns'or r['town']==t)and(typ=='All flat types'or r['flat_type']==typ)and lo<=r['p']<=hi and(months==0 or end-month(r)<months)]
  selected=summary(g,chosen) if i in(6,7,10,11) else None
  selected_rows=[r for r in g if key(r)==chosen] if selected else []
  lease_observations=[]
  whole_year_observations=False
  for r in selected_rows:
   raw=r.get('remaining_lease') or ''
   m=re.fullmatch(r'([0-9]{1,3}) years?(?: ([0-9]{1,2}) months?)?',raw)
   if re.fullmatch(r'[0-9]{1,3}',raw):lease_observations.append(int(raw)*12-(end-month(r)));whole_year_observations=True
   elif m and int(m[2]or 0)<=11:lease_observations.append(int(m[1])*12+int(m[2]or 0)-(end-month(r)));whole_year_observations |= m[2] is None
  trend={'Points':[],'Start':'','End':'','ObservedMonths':0,'Sales':0,'MinimumY':0,'MaximumY':1}
  if selected:
   points=[]
   for x in range(24):
    index=end-23+x; label=f'{index//12:04d}-{index%12+1:02d}'
    prices=[r['p'] for r in selected_rows if r['month']==label];value=median(prices)
    points.append(dict(Month=label,X=x,Count=len(prices),MedianPrice=value,PriceThousands=None if value is None else value/1000))
   observed=[p for p in points if p['Count']]
   low=0 if not observed else (min(p['MedianPrice'] for p in observed)//50000)*50
   high=1 if not observed else (max(p['MedianPrice'] for p in observed)/50000).to_integral_value(rounding=ROUND_CEILING)*50
   if observed:low=max(0,low-50);high+=50
   trend=dict(Points=points,Start=points[0]['Month'],End=points[-1]['Month'],ObservedMonths=len(observed),Sales=sum(p['Count']for p in observed),MinimumY=low,MaximumY=high)
  recent_ids={r['id'] for r in selected['recent']} if selected else set()
  # Only rendered detail rows need provenance expectations (at most 15 per
  # state). Never repeat the full corpus's provenance across all 15 states.
  source_evidence={r['id']:dict(identity=r['_source']['sourceIdentity'],sha256=r['_source']['rawSha256'],row=int(r['source_row'])) for r in selected_rows if r['id'] in recent_ids and '_source' in r}
  result.append(dict(leaseWholeYears=whole_year_observations,sourceEvidence=source_evidence,trend=trend,addressIndex=sorted({key(r)for r in g}).index(chosen) if selected else -1,leaseMinimum=min(lease_observations) if lease_observations else None,leaseMaximum=max(lease_observations) if lease_observations else None,expectedFullMapped=7533 if len(rows)==241920 and (data/'address-normalization.txt').is_file() and (data/'address-normalization.txt').read_text()=='terminal-road-types-v1\n' else 6 if len(rows)==6 else None,name=STEPS[i],town=t,type=typ,minimum=lo,maximum=hi,months=months,rows=len(g),addresses=len({key(r)for r in g}),latest=latest,selected=selected,key=chosen))
 return result

def verify(code,output):
 if code or FORBIDDEN.search(output):raise RuntimeError(output)
 if re.findall(r'HDB_BUYER_STEP ([a-z-]+)',output)!=STEPS or output.count('HDB_BUYER_PASS')!=1 or output.count('HDB_GATE_EXIT')!=1:raise RuntimeError('Incomplete buyer transitions: '+output)

def run(executable,data,log,expanded=False):
 if expanded:validate_expanded_workload(data)
 expected=expectation(data)
 with tempfile.TemporaryDirectory(prefix='hdb-buyer-gate-') as d:
  path=Path(d)/'expected.json';path.write_text(json.dumps(expected,default=lambda v:float(v) if isinstance(v,Decimal) else v,separators=(',',':')))
  env={**os.environ,'HDB_DATA_DIRECTORY':str(data.resolve()),'HDB_BUYER_GATE':'1','HDB_BUYER_EXPECTATION':str(path),'HDB_SCALE_GATE':'1','HDB_SCALE_EXPANDED_COVERAGE':'1' if expanded else '0'}
  start=time.monotonic();deadline=60 if expanded else 45
  with subprocess.Popen([str(executable.resolve())],cwd=executable.resolve().parent,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True) as child:
   try:output,_=child.communicate(timeout=deadline)
   except subprocess.TimeoutExpired:
    child.kill();output,_=child.communicate();log.write_text(output);raise RuntimeError('Buyer process timeout')
 log.write_text(output);verify(child.returncode,output)
 return dict(seconds=round(time.monotonic()-start,2),expanded_coverage=expanded,state_budget_ms=10000 if expanded else 5000,process_budget_seconds=deadline,log=output)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for n in('executable','data','log'):p.add_argument('--'+n,type=Path,required=True)
 p.add_argument('--expanded-coverage',action='store_true');a=p.parse_args();print(json.dumps(run(a.executable,a.data,a.log,a.expanded_coverage),indent=2))
