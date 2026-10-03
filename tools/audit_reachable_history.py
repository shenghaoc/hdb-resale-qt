#!/usr/bin/env python3
"""Reproducible all-ref generic publication audit, without a private reference.

This cannot certify exact private display-name/coordinate-pair absence. That
requires the separately controlled reference and is deliberately not claimed.
"""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
M9='bdc35e67cdff7ec52d2a9cc6cb144d9065c138df'
RAW_SHA='8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4'
UNSAFE='843df64'
SECRET=re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-(?:proj-)?[A-Za-z0-9_-]{30,}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.')
LOCAL=re.compile(r'(^|/)(\.local|private-onemap|bin|obj|build|\.env|\.aws|\.codex|node_modules)(/|$)|geocode-cache|\.pem$|\.p12$|\.bundle$|\.so(?:\.|$)|\.dll$|\.exe$')
def git(*args):return subprocess.check_output(['git',*args])
def audit(base=M9):
 head=git('rev-parse','HEAD').decode().strip()
 if subprocess.run(['git','merge-base','--is-ancestor',base,head]).returncode:raise ValueError('Required baseline is not an ancestor.')
 commits=git('rev-list','--all').decode().splitlines();blobs={};paths=set();issues=[]
 for commit in commits:
  for line in git('ls-tree','-rz','--full-tree',commit).split(b'\0'):
   if not line:continue
   meta,path=line.split(b'\t',1);_,kind,sha=meta.decode().split();name=path.decode();paths.add(name)
   if kind=='blob':blobs.setdefault(sha,set()).add(name)
 unsafe_reachable=any(commit.startswith(UNSAFE) for commit in commits)
 if unsafe_reachable:issues.append('Excluded commit is reachable.')
 for sha,names in blobs.items():
  content=git('cat-file','blob',sha);text=content.decode('utf-8',errors='replace')
  if hashlib.sha256(content).hexdigest()==RAW_SHA or ','.join(['cache_key','search_value','postal_code','display_name','lat','lng','updated_at']).encode() in content:issues.append('Raw export hash/header in '+sha)
  if any(LOCAL.search(name) for name in names):issues.append('Excluded local/binary path in '+sha)
  if len(content)>5_000_000:issues.append('Unexpected large blob '+sha)
  if SECRET.search(text):issues.append('Credential-shaped content in '+sha)
  if 'docs/coverage/onemap/historical/benchmark.json' in names:
   if any(set(e)!={'CacheKey','SearchValue','Status','Postal','UpdatedAt'} for e in json.loads(text)['Entries']):issues.append('Historical field expansion in '+sha)
 protected=['src/HdbResale.Domain','data','docs/coverage/sample','docs/coverage/results.json','global.json','Directory.Build.props']
 unchanged=not git('diff','--name-only',base,head,'--',*protected).strip()
 if not unchanged:issues.append('Protected importer/evidence/canonical/pin content changed since M9.')
 project=git('show',head+':src/HdbResale.App/HdbResale.App.csproj').decode()
 if '<QtBridgePackageVersion>0.4.0.22-beta</QtBridgePackageVersion>' not in project or ">0.4.0-beta</QtBridgePackageVersion>" not in project:issues.append('Bridge platform pin changed.')
 old_blobs=set(git('rev-list','--objects',base).decode().split())
 new={sha:sorted(names) for sha,names in blobs.items() if sha not in old_blobs}
 result={'head':head,'base':base,'reachable_commits':len(commits),'reachable_unique_blobs':len(blobs),'reachable_paths':len(paths),'new_unique_blobs':len(new),'new_blobs':new,'excluded_commit_reachable':unsafe_reachable,'protected_content_unchanged':unchanged,'issues':issues,'exact_private_reference_rescan':False,'scope':'All-ref generic hash/header, credential-shaped-content, excluded-path/size, minimized-historical-field and unchanged-M9-evidence checks. Inherits M9 and earlier exact-reference evidence. Private raw reference was not available, transferred, read, fetched or reconstructed; exact private display-name/coordinate-pair absence is not newly certified.'}
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--base',default=M9);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 result=audit(a.base);a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='new_blobs'},indent=2))
 raise SystemExit(1 if result['issues'] else 0)
