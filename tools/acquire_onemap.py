#!/usr/bin/env python3
"""User-run, token-in-environment OneMap Search acquisition. No login or credentials saved."""
import argparse,datetime,hashlib,json,os,sys,time,urllib.request,urllib.error,urllib.parse
from pathlib import Path
ENDPOINT='https://www.onemap.gov.sg/api/common/elastic/search'
FROZEN_SAMPLE='875aceb10230d4a52ea6159397cd40a69cbe81346094f6bb1a81423c6e1b5b76'
class AcquisitionError(Exception):
 def __init__(self,kind,status=None,page=1,cause=None):
  self.kind=kind;self.status=status;self.page=page;self.cause=cause
  super().__init__(kind+((' / '+cause) if cause else '')+' at page '+str(page))
class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,*args,**kwargs):return None

def request_page(query,page,token):
 params=urllib.parse.urlencode({'searchVal':query,'returnGeom':'N','getAddrDetails':'Y','pageNum':page})
 req=urllib.request.Request(ENDPOINT+'?'+params,headers={'Authorization':token,'Accept':'application/json','User-Agent':'HdbResaleQt/0.1 (bounded address evidence study)'})
 try:
  with urllib.request.build_opener(NoRedirect()).open(req,timeout=30) as response:return response.status,response.read()
 except urllib.error.HTTPError as e:return e.code,e.read()
 except (urllib.error.URLError,TimeoutError,OSError):raise AcquisitionError('NetworkError',page=page) from None

def parse_page(status,body,page):
 if status==429:raise AcquisitionError('RateLimited',status,page)
 if status==403:raise AcquisitionError('AuthorizationDenied',status,page)
 if status not in (200,401):raise AcquisitionError('HttpFailure',status,page)
 try:data=json.loads(body)
 except (ValueError,UnicodeDecodeError):
  raise AcquisitionError('InvalidToken' if status==401 else 'MalformedResponse',status,page) from None
 if status==401 or (isinstance(data,dict) and 'error' in data):
  # Inspect only to classify; never emit/cache response messages or credentials.
  text=str(data.get('error','')).lower() if isinstance(data,dict) else ''
  kind='ExpiredToken' if 'expired' in text else 'InvalidToken' if status==401 or 'invalid token' in text else 'ApiPayloadError'
  raise AcquisitionError(kind,status,page)
 if not isinstance(data,dict) or any(type(data.get(k)) is not int or data[k]<0 for k in ('found','totalNumPages','pageNum')) or not isinstance(data.get('results'),list):
  raise AcquisitionError('MalformedResponse',status,page)
 found,pages=data['found'],data['totalNumPages']
 if (found==0 and (pages!=0 or data['results'])) or (found>0 and (pages<1 or not data['results'])) or (data['pageNum']!=page and not (page==1 and found==0 and data['pageNum']==0)):
  raise AcquisitionError('PaginationInconsistent',status,page)
 candidates=[]
 for index,r in enumerate(data['results'],1):
  if not isinstance(r,dict) or any(not isinstance(r.get(k),str) for k in ('BLK_NO','ROAD_NAME','POSTAL','BUILDING')):
   raise AcquisitionError('MalformedResponse',status,page)
  candidates.append({'Page':page,'Index':index,'Block':r['BLK_NO'],'Road':r['ROAD_NAME'],'Postal':r['POSTAL'],'Building':r['BUILDING']})
 return found,pages,candidates

def collect(query,token,transport=request_page,pace=lambda:None,origin='OneMapSearch'):
 if not token:raise AcquisitionError('MissingToken')
 record={**query,'Origin':origin,'SourceUrl':ENDPOINT,'RetrievedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(timespec='seconds'),
  'Found':0,'TotalPages':0,'Candidates':[],'Pages':[],'Error':None}
 page=1
 while True:
  try:
   pace();status,body=transport(query['Query'],page,token)
   record['Pages'].append({'Page':page,'Sha256':hashlib.sha256(body).hexdigest()})
   if token.encode() in body:raise AcquisitionError('ApiPayloadError',status,page)
   found,pages,candidates=parse_page(status,body,page)
   if page==1:record['Found']=found;record['TotalPages']=pages
   elif found!=record['Found'] or pages!=record['TotalPages']:raise AcquisitionError('PaginationInconsistent',status,page)
   record['Candidates'].extend(candidates)
   if page>=max(1,pages):
    if len(record['Candidates'])!=found:raise AcquisitionError('PaginationInconsistent',status,page)
    break
   page+=1
  except AcquisitionError as e:
   record['Error']={'Kind':e.kind if page==1 else 'PartialPagination','Cause':e.cause if page==1 else e.kind,'HttpStatus':e.status,'Page':page};break
 return record

def load_queries(path):
 data=json.loads(path.read_text());queries=data.get('Queries',[])
 if data.get('SampleSha256')!=FROZEN_SAMPLE or len(queries)!=378:raise ValueError('Requires the frozen 378-query C# manifest.')
 ids=set()
 for q in queries:
  if set(q)!=set(('QueryId','Block','Road','Query')) or q['Query']!=q['Block']+' '+q['Road'] or q['QueryId']!=hashlib.sha256(q['Query'].encode()).hexdigest() or q['QueryId'] in ids:raise ValueError('Invalid query identity/order manifest.')
  ids.add(q['QueryId'])
 return queries

def acquire(queries_path,cache,rate=30,retry_errors=False):
 token=os.environ.get('ONEMAP_ACCESS_TOKEN')
 if not token:raise AcquisitionError('MissingToken')
 if '\n' in token or '\r' in token:raise AcquisitionError('InvalidToken')
 queries=load_queries(queries_path);cache.mkdir(parents=True,exist_ok=True)
 next_request=0.0
 def pace():
  nonlocal next_request
  now=time.monotonic()
  if now<next_request:time.sleep(next_request-now) # Explicit request-budget pacing, never a correctness retry.
  next_request=time.monotonic()+60/rate
 for q in queries:
  path=cache/(q['QueryId']+'.json')
  if path.exists():
   previous=json.loads(path.read_text())
   if previous.get('Origin')!='OneMapSearch' or any(previous.get(k)!=q[k] for k in q):raise ValueError('Untrusted cached identity/origin.')
   if previous.get('Error') is None:
    pages=previous.get('Pages',[]);candidates=previous.get('Candidates',[])
    if previous.get('SourceUrl')!=ENDPOINT or previous.get('Found')!=len(candidates) or [p.get('Page') for p in pages]!=list(range(1,max(1,previous.get('TotalPages',0))+1)) or any(len(p.get('Sha256',''))!=64 for p in pages):raise ValueError('Incomplete cached response.')
    continue
   if not retry_errors:raise AcquisitionError('CachedApiError',cause=previous['Error']['Kind'])
  record=collect(q,token,pace=pace)
  temporary=path.with_suffix('.tmp');temporary.write_text(json.dumps(record,ensure_ascii=False,separators=(',',':'))+'\n');temporary.replace(path)
  if record['Error']:
   e=record['Error'];raise AcquisitionError(e['Kind'],e['HttpStatus'],e['Page'],e['Cause'])
 print('Complete: '+str(len(queries))+' sanitized OneMap query caches. No geometry, raw response, token or headers saved.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--queries',type=Path,required=True);p.add_argument('--cache',type=Path,required=True)
 p.add_argument('--requests-per-minute',type=int,default=30,choices=range(1,61));p.add_argument('--retry-errors',action='store_true');a=p.parse_args()
 try:acquire(a.queries,a.cache,a.requests_per_minute,a.retry_errors)
 except (AcquisitionError,ValueError,OSError):
  # Only fixed error classifications are safe to show; file/payload exception messages could contain secrets.
  e=sys.exc_info()[1]
  print('Acquisition blocked: '+(str(e) if isinstance(e,AcquisitionError) else 'Invalid input/cache or file IO error')+'. No failure is a no-match.',file=sys.stderr);sys.exit(1)
