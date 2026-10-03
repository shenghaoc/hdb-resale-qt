# M10 large-map presentation investigation

M10 starts from clean exact M9 `bdc35e67cdff7ec52d2a9cc6cb144d9065c138df`.
Its workload is **7,618 mapped addresses**, 241,920 transactions and 188,573
located transactions. This is a presentation investigation; evidence, importer,
matcher, source hashes and coverage are unchanged. The original M6–M9 history
and branches are preserved, and nothing is pushed.

## Reproduced M9 and experiment A

[Initial machine-readable observations](initial-comparison.json) distinguish
filtering, aggregation, edit planning, synchronous Qt/Bridge notifications,
delegate events, QML state completion and startup stages. Native measurements
use the real Linux desktop and pinned .NET 10.0.401, Qt 6.12.0 and Linux Bridge
0.4.0-beta. The macOS 0.4.0.22-beta pin is unchanged and not executed here.

| Observation | Exact M9 | A: retained 7,618 delegates |
| --- | ---: | ---: |
| Classic process | 18.83 s | 30.44 s |
| Extended process | 36.44 s | 59.36 s |
| Classic selection-only state | 27 ms | 502 ms |
| Classic budget state | 542 ms | 1,983 ms |
| Classic empty state | 236 ms | 1,987 ms |
| Extended repeat small-to-full | 4,919 ms | 3,820 ms |
| Full startup process | 8.585 s | 9.070 s |
| Post-GC working set | 1,121.1 MB | 1,121.0 MB |

M9's reentrant process took 27.49 seconds, with a **6,623 ms** completed state;
its maximum observed notification interval was 4,294.341 ms. The old expanded
10-second correctness allowance remains explicitly a performance warning above
five seconds. It is not a responsiveness claim.

A keeps every delegate alive, hides inactive addresses, and uses targeted
membership/fact-role notifications. Lifecycle counters confirm 7,618 initial
objects and zero subsequent construction/destruction while active addresses
range from zero to 7,618. It improves one repopulation observation but regresses
several common filters and selection, retains roughly the same working set,
and nearly consumes the extended process limit. **A is rejected.** Its
[reproducible isolated patch](../../experiments/large-map/retention.patch) is not
production acceptance; removal-triggered FIFO tests do not apply to that spike.

## Candidate B, before final acceptance

B retains the complete C# filtered-address truth, projects viewport rows in C#,
and groups low-zoom in-view addresses by stable 64-pixel Web-Mercator cells at
integer zoom levels. Group numbers mean **addresses**, individual numbers mean
**transactions**. At zoom 15 and above, every in-view address has its own marker.
There is no arbitrary top-N point limit. Changing the viewport never edits the
filter or evidence. Selection is shown as an individual marker if in view and
remains selected with an explicit outside-view notice if panned away; a domain
filter hiding the selected transaction clears it as before.

First exploratory Release run: all 22 presentation transitions completed in
13.18 seconds; maximum state 856 ms. Small-to-full was 438 ms, empty-to-full
313 ms and selection-only 141 ms. At the full view, 23 groups represented all
7,618 addresses; zoom 16 showed 127 individual in-view addresses, and the
explicit offscreen view showed zero while preserving selection. Full native
startup was 6.139 seconds, QML-ready 419 ms and post-GC working set 690.9 MB
(about 410 MiB below the M9 observation). Retained managed heap was effectively
unchanged, consistent with retaining the same evidence/transactions.

These are sequential observations, not statistical estimates. The B presentation
scenario includes different additional states from M9's classic/extended gates,
so total process times are not a like-for-like speedup ratio. QML completion
includes timer cadence and assertions; none of these numbers isolates GPU,
network, native paint, layout or Bridge marshalling. First B measurements precede
the stronger independent Qt-projection oracle and are not final acceptance.

B uses the public default MapItemView incubation behavior. M9's synchronous
`incubateDelegates: false` was a QML-exposed but publicly undocumented Qt 6.12
property. The candidate avoids that version-coupled override while preserving
the explicit QML wrapper lifetime anchor. Its viewport submissions coalesce into
one pending event-loop callback reading the latest camera, without sleeps;
mutations then use the existing UI-thread FIFO queue.

The next comparison is an isolated, serious MapLibre Native Qt source build and
one-source/style-layer renderer using the same 7,618 points. No renderer
migration is preselected or claimed here. The final decision and exact-source
acceptance will be added after those measurements and independent review.

## Independently checked B candidate

A second review strengthened the gate to derive every in-view truth address
independently through native Qt `Map.fromCoordinate`, rather than trusting the
C# presentation plan as its own viewport oracle. It asserts exact address-set
accounting, no duplicates, cluster cell membership, transaction sums, stable
individual coordinates/IDs, all eight roles, current camera metadata, town
index/text/model agreement, lifetime retention and selection/FIFO behavior.
A deliberate `drop-presentation` fault removes coverage from both the C# model
and its serialized presentation expectation; the independent Qt oracle fails it.

The refined Release candidate passes canonical plus all old classic/extended/
reentrant scenarios and the 22-step presentation scenario. The old gates now
assert complete mapped-address truth separately from the number of presentation
objects. Camera steps wait for final delegate readiness before taking identity
snapshots. The first adapted classic gate exposed that missing wait; its genuine
failure was corrected, not accepted or given longer limits. Reentrant retained-
address selection is additionally checked at individual-marker zoom. Default
asynchronous incubation may cancel an intermediate queued creation, so lifecycle
accounting checks the exact created-minus-destroyed live-object balance rather
than assuming every temporary requested row is instantiated.

The [candidate verification record](candidate-verification.json) contains
all stage observations, three successful failure-sensitivity checks and the
alternating importer samples. Presentation/classic/extended/reentrant processes
took 14.28/8.10/9.48/7.68 seconds; maximum completed states were respectively
820/340/436/859 ms. Native synchronous notifications were at most 51.275 ms in
these four observations, compared with the reproduced M9 maximum 4,294.341 ms.
This is a measured workload result, not an isolated marshalling/GPU assertion.
Ten new linked pure projection/diff/FIFO tests bring the C# suite to 144; 28 Python
tests passed independently in the candidate before final root audit tests.

The quiet-window alternating profiles use the unchanged frozen baseline and
candidate, with native compilers paused. M9 warm samples were
2,755 / 2,813 / 4,057 / 3,524 ms; B was 3,169 / 2,648 / 2,703 / 2,509 ms. They
show substantial process variance and no stable importer-regression signal;
they do not guarantee every warm run remains inside the older 2.65–2.90-second
interval. All counts remain exact, and the entire Domain/importer source is
unchanged. A renderer-only workload cannot justify silently changing import or
matching semantics to make a timing number look better.

## Architecture decision

**Select B: retain Qt Location/OneMap and use C# viewport/grid presentation.**
A failed the responsiveness/memory goal. C is now a credible working MapLibre
Native Qt source-layer option, rather than the old M7 binary-loader blocker:
a source-built plugin renders the same 7,618 points at the matched 871×529 map
viewport. Its measured subsequent complete-source calls were 42.7–85.8 ms across the two matched runs,
with a narrower submission-to-settled-observer interval of 60.9–554.9 ms;
selection-only was about 332 ms, zoom 16 was 959–971 ms and pan was 632–639 ms.
These narrower renderer observations cannot be compared as if they included
B's complete filtering/aggregation/Bridge/assertion sequence. They do not prove
B is a faster renderer, nor establish whole-app superiority for MapLibre.

B already meets the practical workload goal while preserving qualified C#
selection, FIFO, stable identities, native controls and map accessibility.
The isolated MapLibre host still needs in-process C#→Bridge/source integration,
native feature hit-testing and equivalent accessibility/labels. Three edge-tile
image warnings also require qualification. Its source build, matching data,
viewport verification, licensing/dependency details and limitations are retained
as [separate experiment evidence](../../experiments/maplibre-m10/README.md); it is not added as a production dependency.

The unused all-address experimental bypass was removed after review. Production
has one projection path, avoiding an untested alternate mode that could call an
offscreen selection “in view.” The existing reset diagnostic remains. The full
final root build/test/native matrix and physical Release check run on this
cleaned source, not on an earlier copied candidate runtime.
