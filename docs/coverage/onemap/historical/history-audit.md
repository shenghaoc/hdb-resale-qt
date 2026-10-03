# Publication history audit — 2026-10-03

Audited every implementation commit reachable from verified M5HEAD
1eaeca051bdbc726731928dfedd9bf3e2eee28af:11commits,122unique blobs,72paths.
This includes the existing publicly published M1–M4 history, the fresh-search tools
checkpoint and all three unpublished minimized historical/audit commits. The final
documentation-only checkpoint is separately checked before the authorized main push.

Checks: enumerated Git commit trees and unique blobs; compared bytes/hash with the
raw export; reviewed paths for raw/private/auth/env files; inspected historical
projection schemas in every version; searched actual source-name literals and
exact full-precision raw coordinate pairs; scanned private-key/GitHub/OpenAI secret
patterns and reviewed credential-related source lines. Credential references are
environment variable names and clearly synthetic unit values,not live credentials.
These checks support an inspected-source conclusion,not a universal secret-scanner
guarantee for arbitrary unknown formats.

- No full10333row export,private materialized file,credential file or secret found.
- Every reachable historical projection has exactly cachekey/search/postal/status/
  write-time fields plus source/sample/hash provenance; no display-name/geometry.
- No actual cached coordinate pair found. HDB footprint geometry/derived points
  and explicitly synthetic test geometry are separate permitted sources.
- Actual-name literal scanning found only a shared substring in legitimate HDB
  street/SearchValue/Road fields. Those fields derive from the frozen public HDB
  sample,not from cached display names. Schema/source review confirms the distinction.
- The310address ledger and32row sample contain generalized review flags/source IDs,
  no cached name strings or coordinates. Raw names were audit-only private inputs.

Former local843df64 contains display names in its benchmark blob,no cached geometry.
It was replaced by92a072e before publication. It is **not an ancestor** of finalHEAD
or any known origin ref,so its blob is not reachable from the proposed main update.
It remains an unreachable local Git/reflog object; nothing was garbage-collected,
reflog-expired or silently rewritten again. User expressly requires it remain
unreachable,not that it be deleted. The full raw export never entered Git.

User's latest publication target is **origin/main only**; no further M5 branch
publication. Repository clean and revised acceptance checks pass locally. Execute
fetch/verify no unexpected main divergence,local --ff-only merge of final verified
HEAD,push main,then compare localmain/originmain/GitHub defaultHEAD. If approval
review rejects that action,stop and report its exact target/reason; do not switch
publication mechanisms. Historical first-result uncertainty remains explicit.
