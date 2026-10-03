#!/usr/bin/env python3
"""Reproducible all-ref generic publication audit, without a private reference.

This cannot certify exact private display-name/coordinate-pair absence. That
requires the separately controlled reference and is deliberately not claimed.
"""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
M9='bdc35e67cdff7ec52d2a9cc6cb144d9065c138df'
M10='ff5cc7d1f8a61664408f35455e19a7fb8fff5227'
LEGACY_IMPORT_SHA='88118bdb3a9a68f59ea47cddae27568623f136cb7b7d5320a376f8f752dc6cc5'
M11_DOMAIN_FILES=frozenset('src/HdbResale.Domain/'+name+'.cs' for name in ('ResaleTransaction','CsvImport','BlockSummary','ExplorerState'))
M11_CONTRACT='docs/product-rc/data-contract.json'

def validate_buyer_contract(changed,record,read_blob):
 issues=[]
 if record.get('contract_version')!=2 or record.get('base')!=M10:issues.append('M11 data-contract version/base mismatch.')
 if record.get('legacy_import_sha256')!=LEGACY_IMPORT_SHA:issues.append('M11 prior-field digest proof is not the frozen M10 value.')
 current=record.get('expanded_import_v2_sha256','')
 if not re.fullmatch(r'[0-9a-f]{64}',current) or current==LEGACY_IMPORT_SHA:issues.append('M11 must record a distinct verified v2 full import digest.')
 pins=record.get('allowed_domain_sha256',{})
 if set(pins)!=M11_DOMAIN_FILES:issues.append('M11 Domain allowlist must contain exactly the four authorized source files.')
 for name in M11_DOMAIN_FILES:
  if pins.get(name)!=hashlib.sha256(read_blob(name)).hexdigest():issues.append('M11 Domain contract/source hash mismatch: '+name)
 if set(changed)-M11_DOMAIN_FILES:issues.append('Evidence, canonical fixture, source pins or unrelated Domain files changed outside the M11 allowlist.')
 return issues

RAW_SHA='8daf79156eb574be19815bbe7b6c77c98593230735e71bb192133a9585893ec4'
UNSAFE='843df64'
SECRET=re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-(?:proj-)?[A-Za-z0-9_-]{30,}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.')
LOCAL=re.compile(r'(^|/)(\.local|private-onemap|bin|obj|build|\.env|\.aws|\.codex|node_modules)(/|$)|geocode-cache|\.pem$|\.p12$|\.bundle$|\.so(?:\.|$)|\.dll$|\.exe$')
def git(*args):return subprocess.check_output(['git',*args])
def audit(base=M9,m11_buyer_contract=False):
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
 changed=git('diff','--name-only',base,head,'--',*protected).decode().splitlines()
 unchanged=not changed
 contract_record=None
 if m11_buyer_contract:
  if base!=M10:raise ValueError('M11 buyer-contract audit requires the exact M10 base.')
  try:
   contract_record=json.loads(git('show',head+':'+M11_CONTRACT))
   issues.extend(validate_buyer_contract(changed,contract_record,lambda name:git('show',head+':'+name)))
  except (subprocess.CalledProcessError,json.JSONDecodeError) as error:issues.append('M11 committed data-contract evidence unavailable: '+str(error))
 elif not unchanged:issues.append('Protected importer/evidence/canonical/pin content changed since baseline.')
 evidence_unchanged=not git('diff','--name-only',base,head,'--',*protected[1:]).strip()

 project=git('show',head+':src/HdbResale.App/HdbResale.App.csproj').decode()
 if '<QtBridgePackageVersion>0.4.0.22-beta</QtBridgePackageVersion>' not in project or ">0.4.0-beta</QtBridgePackageVersion>" not in project:issues.append('Bridge platform pin changed.')
 old_blobs=set(git('rev-list','--objects',base).decode().split())
 new={sha:sorted(names) for sha,names in blobs.items() if sha not in old_blobs}
 result={'head':head,'base':base,'reachable_commits':len(commits),'reachable_unique_blobs':len(blobs),'reachable_paths':len(paths),'new_unique_blobs':len(new),'new_blobs':new,'excluded_commit_reachable':unsafe_reachable,'protected_content_unchanged':unchanged,'evidence_canonical_pins_unchanged':evidence_unchanged,'domain_contract_evolution':contract_record,'m11_buyer_contract_mode':m11_buyer_contract,'issues':issues,'exact_private_reference_rescan':False,'scope':'All-ref generic hash/header, credential-shaped-content, excluded-path/size, minimized-historical-field and protected-evidence checks. Optional M11 mode checks a narrow four-file Domain evolution against committed v2/legacy-digest evidence; it does not label changed Domain content unchanged. Inherits M9 and earlier exact-reference evidence. Private raw reference was not available, transferred, read, fetched or reconstructed; exact private display-name/coordinate-pair absence is not newly certified.'}
 return result
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--base',default=M9);p.add_argument('--output',type=Path,required=True);p.add_argument('--m11-buyer-contract',action='store_true',help='Exact M10 base only: check the committed four-file buyer-facts evolution and legacy/v2 digest evidence.');a=p.parse_args()
 result=audit(a.base,a.m11_buyer_contract);a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='new_blobs'},indent=2))
 raise SystemExit(1 if result['issues'] else 0)
