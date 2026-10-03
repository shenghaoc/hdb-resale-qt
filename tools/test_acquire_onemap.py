"""Synthetic UNIT-test OneMap responses only. No API calls or acquired evidence."""
import json,unittest,tempfile,os
from pathlib import Path
from unittest.mock import patch
from acquire_onemap import collect,parse_page,AcquisitionError,acquire,NoRedirect
QUERY={'QueryId':'unit-test','Block':'1','Road':'TEST ROAD','Query':'1 TEST ROAD'}
def page(number=1,found=1,pages=1,postal='123456'):
 return {'found':found,'totalNumPages':pages,'pageNum':number,'results':[{'BLK_NO':'1','ROAD_NAME':'TEST ROAD','POSTAL':postal,'BUILDING':'NIL','LATITUDE':'99','LONGITUDE':'999','access_token':'synthetic-sensitive-extra'}] if found else []}
class SyntheticOneMapAcquisitionTests(unittest.TestCase):
 def test_all_pages_and_sanitization_preserve_duplicate_evidence(self):
  calls=[]
  def transport(query,n,token):calls.append(n);return 200,json.dumps(page(n,2,2)).encode()
  r=collect(QUERY,'synthetic-test-token',transport,origin='SyntheticUnitTest')
  self.assertEqual([1,2],calls);self.assertEqual(2,len(r['Candidates']));self.assertIsNone(r['Error'])
  self.assertEqual('SyntheticUnitTest',r['Origin']);text=json.dumps(r)
  for secret in ['LATITUDE','LONGITUDE','access_token','Authorization','synthetic-sensitive-extra','synthetic-test-token']:self.assertNotIn(secret,text)
 def test_legitimate_empty_is_distinct_from_payload_auth_and_http_errors(self):
  r=collect(QUERY,'synthetic-test-token',lambda *a:(200,json.dumps(page(found=0,pages=0)).encode()),origin='SyntheticUnitTest')
  self.assertEqual([],r['Candidates']);self.assertIsNone(r['Error'])
  cases=[(401,b'{}','InvalidToken'),(403,b'{}','AuthorizationDenied'),(429,b'{}','RateLimited'),(500,b'{}','HttpFailure'),
   (200,b'{"error":"Authentication token expired. synthetic-private-value","found":0,"results":[]}','ExpiredToken'),
   (200,b'{"error":"unexpected synthetic-private-value"}','ApiPayloadError'),(200,b'{bad','MalformedResponse')]
  for code,body,kind in cases:
   with self.subTest(kind=kind):
    r=collect(QUERY,'synthetic-test-token',lambda *a:(code,body),origin='SyntheticUnitTest')
    self.assertEqual(kind,r['Error']['Kind']);self.assertNotIn('synthetic-private-value',json.dumps(r))
 def test_partial_pagination_and_metadata_changes_never_become_no_match(self):
  def transport(q,n,t):return (200,json.dumps(page(n,2,2)).encode()) if n==1 else (500,b'failure')
  r=collect(QUERY,'synthetic-test-token',transport,origin='SyntheticUnitTest')
  self.assertEqual('PartialPagination',r['Error']['Kind']);self.assertEqual('HttpFailure',r['Error']['Cause']);self.assertEqual(1,len(r['Candidates']))
  def expired(q,n,t):return (200,json.dumps(page(n,2,2)).encode()) if n==1 else (200,b'{"error":"Authentication token expired.","found":0,"totalNumPages":0,"pageNum":0,"results":[]}')
  r=collect(QUERY,'synthetic-test-token',expired,origin='SyntheticUnitTest');self.assertEqual('ExpiredToken',r['Error']['Cause']);self.assertEqual('PartialPagination',r['Error']['Kind'])
  def inconsistent(q,n,t):return 200,json.dumps(page(n,2 if n==1 else 3,2)).encode()
  r=collect(QUERY,'synthetic-test-token',inconsistent,origin='SyntheticUnitTest');self.assertEqual('PaginationInconsistent',r['Error']['Cause'])
 def test_missing_token_fails_before_network_or_cache_creation(self):
  with tempfile.TemporaryDirectory() as d,patch.dict(os.environ,{},clear=True),patch('acquire_onemap.request_page') as request:
   cache=Path(d)/'cache'
   with self.assertRaises(AcquisitionError) as e:acquire(Path(d)/'missing-queries.json',cache)
   self.assertEqual('MissingToken',e.exception.kind);request.assert_not_called();self.assertFalse(cache.exists())
  self.assertIsNone(NoRedirect().redirect_request(None,None,None,None,None,None))
if __name__=='__main__':unittest.main()
