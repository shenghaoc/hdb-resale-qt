#!/usr/bin/env python3
"""Full mechanical normalization audit and deterministic 64-address outcome sample."""
import argparse,hashlib,json,re
from collections import defaultdict,Counter
from pathlib import Path
from address_coverage import key,digest
from audit_address_coverage import run,read,outcome,compact,write
from prepare_address_coverage import expanded_address

def context(row):
 tokens=row['Street'].split();numeric=len(tokens)>1 and tokens[-1].isascii() and tokens[-1].isdigit();index=len(tokens)-2 if numeric else len(tokens)-1
 if index>0 and tokens[index] in ('ST','RD','DR','CRES') and not(tokens[index]=='CRES' and numeric):return tokens[index]+('BeforeNumeric' if numeric else 'Terminal')
 return 'NoNewRule'
def rank(row):return hashlib.sha256(('hdb-m9-normalization-outcomes-v1\0'+'|'.join(key(row))).encode()).hexdigest()
def audit(original,current_public,current_final,public,final,directory,footprints,output):
 summary=run(original,public,final,directory,footprints,output,expanded_address)
 oldp={key(r):r for r in json.loads(current_public.read_bytes())['Rows']};oldf={key(r):r for r in json.loads(current_final.read_bytes())['Rows']}
 newp={key(r):r for r in json.loads(public.read_bytes())['Rows']};newf={key(r):r for r in json.loads(final.read_bytes())['Rows']}
 assert oldp.keys()==oldf.keys()==newp.keys()==newf.keys()
 periods=defaultdict(set)
 from sample_coverage import period
 for r in read(directory/'transactions.csv'):periods[(r['town'],r['block'],r['street_name'])].add(period(r['month']))
 rules=defaultdict(lambda:defaultdict(lambda:{'Addresses':0,'Transactions':0}));candidates=[];changes=[]
 def add(rule,stat,r):rules[rule][stat]['Addresses']+=1;rules[rule][stat]['Transactions']+=r['Transactions']
 for k,a in newf.items():
  b=oldf[k];rule=context(a);p=newp[k];previous_public=oldp[k]
  if rule!='NoNewRule':
   add(rule,'Applicable',a)
   if b['Evidence']!=a['Evidence']:add(rule,'EvidenceChanged',a)
   if previous_public['Coordinates']=='Missing' and p['Coordinates']=='BlockApproximation':add(rule,'NewlyLocatedPublic',a)
   if b['Coordinates']=='Missing' and a['Coordinates']=='BlockApproximation':add(rule,'NewlyLocatedFinal',a)
   if a['Quality']=='Ambiguous':add(rule,'FinalAmbiguous',a)
   if a['Quality']=='Ambiguous' and b['Quality']!='Ambiguous':add(rule,'NewlyAmbiguous',a)
   if len({x['PostalCode'] for x in a['Evidence']['PostalAssertions']})>1:add(rule,'MultipleRawPostalValues',a)
   if a['Reason']=='InvalidPostalAssertion':add(rule,'MalformedPostalBlocker',a)
   if b['Quality'] in ('ExactAddress','NormalizedAddress') and outcome(b)!=outcome(a):add(rule,'PreviouslyMatchedOutcomeChanged',a)
  if outcome(b)!=outcome(a) or b['Evidence']!=a['Evidence']:changes.append(compact(b,a,rule))
  if rule!='NoNewRule' and previous_public['Coordinates']=='Missing' and p['Coordinates']=='BlockApproximation' and a['Coordinates']=='BlockApproximation':
   r=compact(b,a,rule)
   tags={'Town:'+r['Town'],'Rule:'+rule,*('Period:'+x for x in periods[k])}
   if re.search('[A-Za-z]$',r['Block']):tags.add('Form:LetterSuffixBlock')
   if re.search("[^A-Z0-9 ]",r['Street']):tags.add('Form:PunctuatedStreet')
   if r['Transactions']<=3:tags.add('Form:RareThreeOrFewerTransactions')
   if r['Transactions']>=100:tags.add('Form:AtLeast100Transactions')
   candidates.append((r,tags))
 universe=set().union(*(tags for _,tags in candidates));uncovered=set(universe);selected={}
 while uncovered and len(selected)<64:
  r,tags=min((x for x in candidates if key(x[0]) not in selected),key=lambda x:(-len(x[1]&uncovered),rank(x[0])))
  selected[key(r)]=r;uncovered-=tags
 assert not uncovered,('Uncovered deterministic sample strata',uncovered)
 for r,_ in sorted(candidates,key=lambda x:rank(x[0])):
  if len(selected)>=64:break
  selected[key(r)]=r
 write(output/'manual-sample.csv',sorted(selected.values(),key=key))
 # Complete changed-address membership stays checked in; detailed all-candidate
 # JSON/projection remains reproducible locally instead of copying megabytes of
 # repeated source-row lists into the ledger.
 def concise(r):
  r=dict(r);r['PostalCandidates']={postal:{'Assertions':sum(len(rows) for rows in sources.values()),'Datasets':len(sources),'ReferencesSha256':hashlib.sha256(json.dumps(sources,separators=(',',':')).encode()).hexdigest()} for postal,sources in r['PostalCandidates'].items()};return r
 write(output/'normalization-changes.csv',[concise(r) for r in changes])
 original_rows={key(r):r for r in json.loads(original.read_bytes())['Rows']}
 all_changes=[compact(original_rows[k],a,'OutcomeChange' if outcome(original_rows[k])!=outcome(a) else 'ProvenanceOnly') for k,a in newf.items() if original_rows[k]['Evidence']!=a['Evidence'] or original_rows[k]['Location']!=a['Location']]
 write(output/'changed-addresses.csv',[concise(r) for r in all_changes])
 final_historical=[a for a in newf.values() if a['Coordinates']=='BlockApproximation' and not a['Evidence']['PostalAssertions']]
 norm={'Profile':'terminal-road-types-v1','PerRuleContext':rules,'NormalizationChanges':len(changes),'SampleAddresses':len(selected),'SampleCandidateAddresses':len(candidates),'SampleCoveredTags':sorted(universe),'SampleMethod':'SHA256 hdb-m9-normalization-outcomes-v1+NUL+town|block|street. Greedy maximize uncovered town/period/rule-context/rare-form tags with hash tie-break, then hash-fill to64, sorted output. Eligible: newly located vs unchanged-rule public stage and still located in final. No success cherry-picking within eligible outcomes.','HistoricalOnlyLocatedAddresses':len(final_historical),'HistoricalOnlyLocatedTransactions':sum(r['Transactions'] for r in final_historical),'OriginalCurrentRuleReportSha256':{p.name:digest(p) for p in [current_public,current_final]},'ArtifactsSha256':{p.name:digest(p) for p in sorted(output.glob('*.csv'))}}
 (output/'normalization-impact.json').write_text(json.dumps(norm,indent=2,ensure_ascii=False)+'\n')
 summary['ManualSampleAddresses']=len(selected);summary['SampleMethod']=norm['SampleMethod'];summary['ArtifactsSha256']=norm['ArtifactsSha256'];summary['ManualReviewStatus']='Required final sets generated; separate reviewed ledgers required.'
 (output/'comparison.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False)+'\n')
 return norm
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__)
 for n in ['original','current-public','current-final','public','final','directory','footprints','output']:p.add_argument('--'+n,type=Path,required=True)
 a=p.parse_args();print(json.dumps(audit(a.original,a.current_public,a.current_final,a.public,a.final,a.directory,a.footprints,a.output),indent=2,ensure_ascii=False))
