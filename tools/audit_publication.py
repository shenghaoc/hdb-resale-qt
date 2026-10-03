#!/usr/bin/env python3
"""Audit every blob reachable from a candidate ref; never print private matches.

Private reference stays outside Git. Existing HDB street substring collisions are
accepted only in already reviewed M5 blobs, not by globally allowing a name.
"""
import argparse,csv,hashlib,json,re,subprocess
from pathlib import Path
RAW_SHA='8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4'
UNSAFE='843df64'
# M5 individually reviewed source street/query collisions.
REVIEWED={'bc23c60fb6948592ccf3b84aa9c4e568f0daa9a6','c51d55c0baa94cfde7b9caf02337ca01cbf88c37','30b89d3116c3da89e2b1461907c608cf6c623671','6ef4fda5d85eaf0d16b280ef0e49dae91a73a892','1835fdd76dd421ffeabdc4468d584fad92a25960','acbba4ec87159c3260e8e2c0d839100299157616'}
def git(*args):return subprocess.check_output(['git',*args])
def audit(ref,private):
 raw=private.read_bytes()
 if hashlib.sha256(raw).hexdigest()!=RAW_SHA:raise ValueError('Private reference hash mismatch.')
 with private.open(newline='',encoding='utf-8') as f:rows=list(csv.DictReader(f))
 if len(rows)!=10333:raise ValueError('Private reference count mismatch.')
 names={r['display_name'].strip() for r in rows if r['display_name'].strip() not in ('','NIL')}
 pairs={(r['lat'],r['lng']) for r in rows if r['lat'] and r['lng']}
 commits=git('rev-list',ref).decode().splitlines();blobs={};paths=set();issues=[];collisions=0
 if subprocess.run(['git','merge-base','--is-ancestor',UNSAFE,ref],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0:issues.append('Excluded commit is reachable.')
 for commit in commits:
  for line in git('ls-tree','-rz','--full-tree',commit).split(b'\0'):
   if not line:continue
   meta,path=line.split(b'\t',1);mode,kind,sha=meta.decode().split();name=path.decode();paths.add(name)
   if kind=='blob':blobs.setdefault(sha,set()).add(name)
 for sha,blobpaths in blobs.items():
  content=git('cat-file','blob',sha);text=content.decode('utf-8',errors='replace')
  if hashlib.sha256(content).hexdigest()==RAW_SHA or ','.join(['cache_key','search_value','postal_code','display_name','lat','lng','updated_at']).encode() in content:issues.append('Raw export in reachable blob '+sha)
  if any(re.search(r'(^|/)(\.local|private-onemap|bin|obj|\.env|\.aws|\.codex)(/|$)|geocode-cache|\.pem$|\.p12$',p) for p in blobpaths):issues.append('Excluded local path in blob '+sha)
  if len(content)>5_000_000:issues.append('Unexpected large publication blob '+sha)
  for name in names:
   if not re.search(r"(?<![A-Za-z0-9])"+re.escape(name)+r"(?![A-Za-z0-9])",text):continue
   if name=='RESERVOIR'+' VIEW' and sha in REVIEWED:collisions+=1
   else:issues.append('Private display-name match in blob '+sha);break
  numeric=set(re.findall(r'(?<![\w.])-?\d+\.\d{8,}(?![\w.])',text))
  if any(lat in numeric and lng in numeric for lat,lng in pairs):issues.append('Private full-precision coordinate pair in blob '+sha)
  if re.search(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-(?:proj-)?[A-Za-z0-9_-]{30,}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.',text):issues.append('Credential-shaped content in blob '+sha)
  for p in blobpaths:
   if p=='docs/coverage/onemap/historical/benchmark.json':
    entries=json.loads(text)['Entries']
    if any(set(e)!={'CacheKey','SearchValue','Status','Postal','UpdatedAt'} for e in entries):issues.append('Historical projection field expansion '+sha)
 result={'ref':git('rev-parse',ref).decode().strip(),'commits':len(commits),'unique_blobs':len(blobs),'paths':len(paths),'reviewed_hdb_street_collisions':collisions,'issues':issues,'excluded_commit_reachable':False if not any('Excluded commit' in x for x in issues) else True}
 if issues:raise ValueError(json.dumps(result))
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--ref',default='HEAD');p.add_argument('--private-reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 result=audit(a.ref,a.private_reference);a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
