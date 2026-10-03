# Milestone 4 bounded coverage study

**This measures the pinned ACRA B register extract with M3's conservative
normalization. Missing corroboration here is not a wrong address, broader ACRA
absence, historical discrepancy or a demonstrated matcher defect.** It is not
a probability estimate for all HDB resales. Sampling balances strata/types rather
than weighting transaction volume. Matching rules are unchanged.

The study uses 416 real resale rows from the pinned 241,920-row source: four
per each of 26 towns × four periods (2017–2019, 2020–2022, 2023–2025, 2026).
Seven flat types, 378 distinct block/street addresses, 315 blocks and 239 streets
are represented. The original six-row startup correctness fixture is untouched;
these study files are not packaged into the app or loaded at normal startup.

## Exact offline selection and provenance

[manifest.json](sample/manifest.json) pins four source dataset IDs, direct URLs,
retrieval date 2026-10-03, full-source SHA-256, counts and every derived input
hash. Sources are the same established HDB resale/property/building and ACRA B
snapshots used in M3; URLs alone do not identify source versions. The generator
rejects different source bytes. Source CSV `source_row` is the physical ending
line reported by Python csv.DictReader including header; selected records have
no embedded newlines. Source facts remain verbatim, and no company/person/UEN/
unit fields are retained from ACRA. Footprint features preserve original objects.
Only the source-row field, selected projections, sample selection, bounding-box
coordinates and report classifications are derived.

Algorithm `hdb-m4-coverage-v1`, in [sample_coverage.py](../../tools/sample_coverage.py):

1. Group every resale by exact town and the four inclusive year periods above.
2. Rank rows inside each flat type by lowercase SHA-256 of UTF-8
   `hdb-m4-coverage-v1` + NUL (`0x00`) + decimal source-row text; tie-break by
   numeric source row. No matching evidence is inspected.
3. In each stratum, rank flat types by SHA-256 of the same seed + NUL +
   `town|period|flat_type`, tie-break by ordinal flat-type text. Round-robin in
   this fixed order; take the first remaining row with an unused **raw**
   block/street pair. Skip duplicate addresses within that stratum. Stop at four
   or exhaustion. The same address may occur in different periods.
4. Sort all chosen records by numeric source row. CSV UTF-8/LF uses the original
   source header order after added source_row. Project every property and ACRA
   assertion matching the chosen normalized keys; retain every building feature
   on any projected block/postal, preserving source order. No candidate winner
   is selected during extraction. JSON uses Python's deterministic field/source
   order and specified separators; the manifest is indented with trailing LF.
5. Feed every selected transaction and all retained evidence through **the
   existing C# importer/matcher**, not a Python reimplementation of matching.
   Python's two existing street aliases only bound evidence extraction; tests
   check important distinctions and C# checks the resulting evidence/counts.

All five generated input/manifest files were independently regenerated and
byte-compared using full pinned sources. Python tests check hash encoding,
period boundaries, order independence, distinctness, flat-type round-robin,
stratification and selection blindness to matchability. C# tests pin input
hashes, real conflict behavior, quality counts, cross-tab conservation and
canonical-six regressions.

## Results and evidence audit

[results.json](results.json) pins the derived report/audit hashes and unchanged
M3 matcher revision. [report.json](report.json) retains every failure, source evidence and town ×
period × match × coordinate cells. Matched identity counts sum independently
from coordinate counts:

| Measure | Count |
| --- | ---: |
| Total accepted | 416 |
| ExactAddress | 22 |
| NormalizedAddress | 48 |
| Ambiguous | 1 |
| Unmatched | 345 |
| BlockApproximation | 70 |
| Missing | 346 |
| Rejected / diagnostics / matched without geometry | 0 / 0 / 0 |

| Period | Exact | Normalized | Ambiguous | Unmatched |
| --- | ---: | ---: | ---: | ---: |
| 2017–2019 | 3 | 9 | 1 | 91 |
| 2020–2022 | 6 | 13 | 0 | 85 |
| 2023–2025 | 5 | 15 | 0 | 84 |
| 2026 | 8 | 11 | 0 | 85 |

Evidence-backed failure reasons: **345 MissingPostalCorroboration**, **1
ConflictingPostals**; zero missing/multiple property, missing/multiple footprint
or matched-without-geometry outcomes in this sample. Historical differences,
normalization-insufficient and unknown-error causes are not assigned without
proof. A missing assertion can reflect B-only coverage or an unsupported spelling;
this study does not distinguish those causes or generalize to other ACRA letters.

[audit.json](audit.json) and [audit_coverage.py](../../tools/audit_coverage.py)
verify every failure against the full property/ACRA snapshots. Manual review was
bounded to the deterministic six lowest hash-ranked missing cases per period
(24), plus the sole conflict; representative IDs/addresses/source rows are
retained. All 24 have direct HDB property agreement and no corroboration under
current normalized keys in B. This is not a manual audit of all 345 missing
rows' semantic address equivalence; their full-source absence checks are automated.

The conflict is **HDB-6769, 446 HOUGANG AVE 8**, property row 7522: 74 ACRA
assertions, 73 with postal 530446 and one (row 60428) with 530836. The original
building source maps 530446 to block 446 and 530836 to block 836. This supports
an inconsistent registered-address assertion; it does not justify deleting the
minority or selecting the majority. M3 correctly retains Ambiguous/Missing.
No matcher change or percentage optimization was made; before/after results
are identical for all 416 rows. A real-source regression test locks this behavior.

## Reproduction

Obtain the four exact official snapshot bytes identified by the manifest (the
existing data.gov.sg poll-download process is documented in data/README).
Full sources and expiring signed URLs are intentionally not tracked. Moving
current endpoints may no longer return these bytes; hash mismatch fails clearly.
Then, from repository root:

```sh
python3 tools/sample_coverage.py \
  --resales /path/resales.csv --properties /path/property.csv \
  --postals /path/acra-b.csv --buildings /path/buildings.geojson \
  --output /tmp/hdb-study
# With README's toolchain PATH and QtDir exports:
dotnet run --project src/HdbResale.App/HdbResale.App.csproj --no-build -- \
  --coverage /tmp/hdb-study /tmp/hdb-report.json
python3 tools/audit_coverage.py --report /tmp/hdb-report.json \
  --properties /path/property.csv --postals /path/acra-b.csv \
  --buildings /path/buildings.geojson --output /tmp/hdb-audit.json
python3 -m unittest discover -s tools -p 'test_*.py' -v
```

The offline report command returns before loading QML. It does not change
normal app data, enable a pipeline, fetch at startup or provide geocoding.
Current polygons/registered addresses cannot establish historical flat identity;
bounding-box points remain approximate and can be outside an irregular polygon.
