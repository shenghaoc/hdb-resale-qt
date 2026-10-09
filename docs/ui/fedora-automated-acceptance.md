# Fedora acceptance tooling — 2026-10-09

This is a focused tooling follow-up to draft PR #20. Full Fedora native acceptance
remains incomplete; the historical physical record in draft PR #19 is unchanged.
No product fix, Worker change, deployment, database mutation, snapshot publication,
review request or merge is included.

## Sources and environment

- Frozen UI: Stage 3 `6ca00e51038811c305b77ab2db32061cf4650871`, tree
  `29a15748f8e272ccc1a424fa34ef8f1547e6b856`; Stage 2
  `c880920f76604337070ecc2ea376f7e29baa0b55` is its ancestor.
- Record/helper: merged Mac #18 ref `f5cfc0f6a8fe1855a09d87762509663adcd32880`;
  helper blob `1d5d1205aa8400bc012a982a0adefc81e10fe484`. Both fixture trees are
  `ea671adbe6c0c3122f2daa8f3978c6ba44dacaea`. No Mac PASS carries into Fedora.
- Host: Fedora 45 KDE prerelease x86_64, kernel 7.2.9-300.fc45, KWin 6.7.5,
  active logged-in Wayland session. System Qt 6.11.2; pinned vendor Qt 6.12.0.
  Session style override is unset; HDB uses Fusion, BreezeLight palette,
  Noto Sans 10, scale 1, OpenGL/Mesa Intel UHD 630.
- Pinned SDK 10.0.401/runtime 10.0.12 and Linux x64 Bridge 0.4.0-beta. Required
  official Qt modules and Wayland plugin dependencies are present. Build tasks
  and pins are unchanged. Frozen executable SHA-256:
  `eba3a53826afdb2253117239f04fea9b65eeab87346aa77d65b2de7d19dc2fc1`.
- KDE driver: [official repository](https://invent.kde.org/sdk/selenium-webdriver-at-spi),
  unchanged ref `50e41aa9913b16e5d0a6fec921c963485aa125b4`;
  [current maintained documentation](https://community.kde.org/Selenium).
  Protocol source: official plasma-wayland-protocols ref
  `e14966e3151dc9273e4aa069f0bfbf8840bde5b4`; `fake-input.xml` SHA-256
  `89f4ada4a1f09168a4528f47c0495e3f89dc2a25e8b23f4539ed3f5110eff708`.

## Boundaries and reproducible command

Existing QtTest gates are preserved. `AcceptanceSuite.qml` drives input inside
Qt and assigns application dimensions; it does not deliver compositor input or
resize a Plasma frame. Only selectors and opt-in test loaders instrument product
QML. Normal launches do not load either acceptance gate. The existing independent
GI/AT-SPI reader and inspector-only pixel verifier remain separate oracles.

`kde_external.py` runs KDE's unchanged calculator/input examples first, then a
plain Qt reduction and **ordinary frozen HDB launches**. It unsets
`HDB_PACKAGE_SMOKE`, `HDB_API_GATE` and production API/tile overrides. Fixture
launches use only the record helper's printed endpoint. Production uses GET-only
requests, pins manifests around summaries/final observations and saves detail.
It refuses changed production snapshots until cases are derived again.

The driver uses these distinct transports:

| Operation | Actual transport |
|---|---|
| `.click()` | AT-SPI Action (SetFocus/Press/Toggle/ShowMenu when exposed) |
| Regular `.send_keys()` / `.clear()` | AT-SPI EditableText |
| Upstream `SetValueCommand` | AT-SPI Value, including external scrollbar movement |
| W3C key/pointer/wheel/touch actions | KDE helper → KWin fake-input protocol |

A returned click/action response is not a delivered event or successful selection.
Selection is checked using the actual **selected** state, not the driver's
`is_selected()` method, which also accepts focus. No AX result establishes speech.

Preparation: use a task-local Python venv with system GI/pyatspi/lxml and official
PyPI Appium-Python-Client 4.5.1, Flask 3.0.0, Selenium 4.29.0 and numpy 2.5.3.
KCalc 26.08.1 and system/vendor `qml` are existing prerequisites. The full upstream
CMake build lacks KF6 development dependencies on this host. The small
`kde-input/CMakeLists.txt` builds the **unchanged upstream helper sources** with
existing system Qt/Wayland/xkbcommon dependencies; no host packages are installed:

```sh
cmake -S hdb-fedora-tooling/tools/native_acceptance/kde-input \
  -B external/inputsynth-build \
  -DKDE_SOURCE="$PWD/external/selenium-webdriver-at-spi" \
  -DFAKE_INPUT_XML="$PWD/external/fake-input.xml"
cmake --build external/inputsynth-build --parallel 1
```

From the task workspace, this is the single external-run command:

```sh
DOTNET_ROOT=/home/sheng/.local/share/hdb-qt-toolchain/dotnet \
QtDir=/home/sheng/Qt/6.12.0/gcc_64 \
external/atspi-venv/bin/python \
  hdb-fedora-tooling/tools/native_acceptance/kde_external.py \
  --kde-source external/selenium-webdriver-at-spi \
  --input-bin external/inputsynth-build \
  --qml /usr/lib64/qt6/bin/qml \
  --qml /home/sheng/Qt/6.12.0/gcc_64/bin/qml \
  --executable hdb-fedora-ui/src/HdbResale.App/bin/Release/net10.0/HdbResale.App \
  --fixture-helper hdb-fedora-record/tools/api_fixture_server.py \
  --output evidence/kde-external/new-run
```

Output directories must be new. The runner preserves session style, uses
`QT_QPA_PLATFORM=wayland` and child-only accessibility enablement, and stops only
owned process groups. It never replaces an existing server at port 4723. It does
not use upstream `run.rb`'s permission-check bypasses, register desktop grants or
start a nested host compositor. Nonzero results preserve upstream failures,
reduced Qt failures and HDB failures. Screenshots, XML, logs and API data stay local.

## External prerequisite and reduction results

| Unchanged upstream check | System Qt 6.11.2 | Vendor Qt 6.12.0 |
|---|---|---|
| KCalc example | 6/6 PASS (system Qt) | — |
| Value test | PASS | PASS |
| Text-input tests | FAIL: 3 errors | FAIL: 3 errors |
| Pointer/wheel/touch tests | FAIL: 3 errors | FAIL: 3 errors |

The logged session's registry does not advertise `org_kde_kwin_fake_input` to the
helper. No keyboard/pointer request is delivered. The helper can exit zero when
inactive and the driver ignores its return status; therefore command success is
not acceptance. Direct EditableText insertion/readback and Value changes work.
No grant or security check is altered to make compositor input pass.

`ListRepro.qml` reproduces missing external rows on **both** Qt versions: scroll,
replace the model, resize while the list is hidden, show it, then externally scroll
again. Rows 13–16 are visibly rendered in the PNG and reported by a read-only
geometry observer, but absent from KDE's tree and a **fresh independent GI reader
with caches disabled/cleared**. Initial scrolling and End expose their rows.
Filtering alone, hidden model replacement alone and resizing alone pass.
This isolates a Qt accessibility/lifecycle trigger below HDB business logic;
the exact internal Qt defect is not established and no workaround is applied.

## Gate-disabled HDB observations

Production remained coherent at `2026-10-04T15:30:00.000Z`: 9,730 summaries,
9,297 default-eligible. Exact 30 BALAM RD/30 CASSIA CRES precede 301/302/304/305
UBI AVE 1 prefix matches. Postal case: 121 ANG MO KIO AVE 3/560121. Fixture cases
are six default rows, bedok-res ordering 748A/748B/747A and postal 472748/748B.

| External AT-SPI operation | Fixture | Production |
|---|---|---|
| Ordinary load / settled count from saved summaries | PASS | PASS |
| EditableText ranked search | PASS | PASS |
| Row click actually selects postal address | FAIL | FAIL |
| About Action open/close, snapshot/query/button focus | PASS | PASS |
| Value scroll / external row exposure at wide size | PASS | PASS |
| Compact frame resize, physical input, actual speech | UNVERIFIED | UNVERIFIED |

These are limited AT-SPI behavior observations; **external native acceptance is
not promoted** while delivery prerequisites fail. Both postal rows expose an
empty Action list, and `.click()` leaves them unselected. The frozen ItemDelegate
uses `Qt.NoFocus` and no explicit `Accessible.onPressAction`. The inspected
[Qt accessibility source](https://code.qt.io/cgit/qt/qtdeclarative.git/tree/src/quick/accessible/qaccessiblequickitem.cpp)
supplies no default Press for ListItem; this is consistent with the observed empty
Action list (a source-based inference, not a patched-binary proof).
This is a separate Stage 2 action-exposure
finding; preserve list-held keyboard focus when investigating an explicit action.
The unchanged driver also produced a retained XPath path/index exception during
model changes; exact-name lookup avoids that client race without patching KDE.

The five original missing production rows are present after ordinary **wide**
Value scrolling. This does not erase the compact/hide/resize QtTest failure or
plain-Qt reproduction. The production-only changed wait was validated separately,
referencing completed upstream prerequisites rather than duplicating them.

## Preserved QtTest results and expectation corrections

First results at `ce381488` remain in Git/local evidence: four assertions failed
per dataset, plus production row exposure. Following the user's clarified
expectations, readable wrapping is now an explicit observation; clipping,
row geometry and the 40% cap remain assertions. “Nearest MRT” still wraps at width
79 into two readable lines. About checks now cover both clicked-button return
focus and content-origin list return focus. Neither correction changes product
behavior or suppresses the compact-toggle/row failures.

| Current application-level QtTest check | Fixture | Production |
|---|---|---|
| Wayland QPA, search/keyboard/selection/filter/refinement | PASS | PASS |
| Compact Back/filter focus and widening preserves selection | PASS | PASS |
| Compact Map/Addresses clicks move focus to target pane | FAIL (2) | FAIL (2) |
| About isolation, query retention and both focus origins | PASS | PASS |
| Fact geometry, no truncation, readable wrap, chart/palette | PASS | PASS |
| Inspector wheel/keys/drag and list End/PageUp/Home | PASS | PASS |
| External selected row/groups/facts/registrations once | PASS | PASS |
| External rows after QtTest wheel | PASS | FAIL |
| Inspector opacity/clipping/link-pixel checks | PASS | PASS |

Both suites return **1**. Production has five successful external checkpoints out
of six; fixture has six. `HDB_ACCEPTANCE_COMPLETE` means sequence completion, not
PASS. QtTest selection PASS and external Action selection FAIL are distinct.
The Stage 2 toggle handler changes view index but leaves focus on the clicked
toggle. The production wheel checkpoint again lacks the visible 40 TANGLIN HALT
RD row and others. No product correction is included.

Release build passes with zero MSBuild warnings/errors; Qt deployment notes
missing translation locales. Previous 298 C#, 38 Python and two native C++ tests,
six readiness modes and PoC pass remain recorded. The affected Release build,
both suites, pixel checks, new Python compilation and upstream-helper CMake build
were run for this follow-up. No readiness mode establishes physical acceptance.

## Isolated VM and remaining limits

Only task-owned rootless containers in `external/openqa-storage` are used.
Existing `/dev/kvm` access succeeds (API 12); no group/device/SELinux changes.
Official runner digest `sha256:6cdfea91f06d2627c78da3be1690b1352346fbc595925cc845ece7915b449966`,
Initial QEMU 11.0.3, unchanged [os-autoinst](https://github.com/os-autoinst/os-autoinst)
ref `fa267c159a95c274f98514d4810e0250d02cce22` built with `-j1`.
The [official Fedora 45 KDE beta ISO](https://fedoraproject.org/kde/download/beta/)
passes Fedora signature and SHA-256 verification
(`cd2321a8537667e0017efc3371bac191d7747f834da8fc8c30c88f00a480811e`).
The guest remains limited to two CPUs and 2,304 MiB RAM; only one VM runs at a
time. Initial host available memory was about 3.8 GiB with swap full.

Boot-loader/needle setup failures and quoted direct-kernel arguments are retained
as harness failures. Ordinary GRUB boot plus guest serial logging mounts the live
root and reaches Fedora 45/kernel 7.2.0-61.fc45 login. Engine exit zero is not a
desktop PASS. The resumed runs retain these separate outcomes:

| Attempt | Observed outcome |
|---|---|
| 11 | Interrupted UI evidence excluded; completed serial diagnostics retained |
| 12–14 | Plasma startup timeouts, EGL/DRI failures or black output; standard VGA had no render node |
| 15 | Runner prerequisite failure: the egl-headless display module was missing |
| 16 | Virgl rendered the greeter/wallpaper; the 3 GiB container recorded one QEMU OOM kill; ordinary app retry interrupted, engine exit 1/canceled |
| 17 | Normal live-user login reached active tty2 Wayland; ordinary frozen HDB resolved Fusion, created OpenGL/virgl QRhi and fetched fixture summaries/manifest with HTTP 200; no usable visible Plasma/HDB window |
| 18 | Focused basic-desktop probe only: non-GL virtio-vga, graphical startup before serial login; QEMU screendump succeeds, but native framebuffer and VNC both remain black with a cursor; no visible launcher response |

For virgl, only the already accessible host `renderD128` was mapped into a new
task-owned rootless container. No privileged mode, device/group permission or
SELinux change was used. Official `qemu-ui-opengl` installation inside that
container also updated its runner to QEMU **11.1.1**. Run 17 used a 4 GiB container
cap with no swap after the owned failed VM exited and about 5.2 GiB was available;
guest RAM, CPU count and concurrency stayed unchanged. Its memory peak was
3,876,286,464 bytes, and `oom_kill` stayed at the run-16 baseline of one.

The guest uses PlasmaLoginManager, KWin 6.7.4 and system Qt 6.11.1. Initial KWin
startup could not determine an active graphical session/open DRM; the ordinary
blank-password live-user login subsequently established a session. Failed
user-level Plasma components were retried without changing their configuration.
Guest app staging failures are retained separately: missing runtime PATH, then
missing matching Qt libraries. The frozen apphost hash stayed unchanged; official
Qt 6.12 Wayland plugins and QML library dependencies were staged from the pinned
host toolchain. Interrupted extraction exhausted the live overlay and was retried
after removing only the verified guest transfer archive (local original retained).

Run 17's VNC/openQA capture remained black with a cursor, and the independent
[documented QEMU screendump](https://www.qemu.org/docs/master/interop/qemu-qmp-ref.html#command-screendump)
returned `GenericError: no surface`. This alone does not establish an unusable
desktop: QEMU's current [screendump implementation](https://raw.githubusercontent.com/qemu/qemu/master/ui/ui-qmp-cmds.c)
requires a pixel surface, and [console.c](https://raw.githubusercontent.com/qemu/qemu/master/ui/console.c)
returns none for other scanout kinds, including GL. These are upstream source
observations, not proof of the exact installed binary's failure cause.
KWin's debug-console request did not yield a
viewable Windows tab; `xlsclients` is absent in the guest too. The exact app log
also retains a real QML diagnostic at `Main.qml:303`: `Unable to assign [undefined]
to QGeoMapType`; the copied guest payload lacks the pinned GeoServices plugins.
This attempt cannot establish a clean native load or attribute that warning to
the frozen UI. No full-session assertion or actual Orca speech check ran.

### Focused graphics diagnosis after run 17

The user selected the isolated VM route; completion does not depend on granting
access to the everyday host desktop. The maintained
[Fedora KDE desktop_browser job 7140696](https://openqa.stg.fedoraproject.org/tests/7140696)
passed on 2026-10-09. Its actual log sends `super`, recognizes the launcher,
clicks it, types a Fedora Accounts URL and recognizes the resulting browser page.
The downloaded screenshot independently shows that page and Plasma panel.
Its machine uses `virtio-vga`, q35/secure UEFI, two CPUs and 4,096 MiB, and boots
an installed disk from the current Fedora 45 compose. Configuration and test
source were also read from the official Fedora repository, pinned at
`9c442ba4c32f3b6b78fdc29f63f37e0d4f9ee242`.
That is upstream evidence; the same full example has not passed locally.
The generic os-autoinst example's no-media path only matches firmware and exits,
so it cannot establish KDE compositor input.

Earlier local non-GL attempts were identified before retrying. Run 18 specifically
tests the serial-first startup hypothesis: it leaves graphical startup alone,
uses the maintained VNC/QMP backend, and logs into the serial console only after
the black-screen/basic-input observations. No HDB staging or launch occurs.
Actual QEMU topology has one `virtio-vga` device with one output; VNC is connected
to that same VM on internal port 5999. Both native QMP framebuffer capture and
openQA VNC capture show black output with a cursor. The `super` key produces no
visible launcher. Thus a wrong capture endpoint is not supported by this probe;
successful capture is not successful compositor-delivered desktop input.

Guest logs show Plasma startup failures before serial login: ksplash/kcminit
timeouts, EGL/DRI failures, portal timeouts and a temporarily hanging KWin thread.
KWin reports Wayland, OpenGL/llvmpipe, Virtual-1 at 1024×768 and scale 1. DRM card
and render nodes exist. Active Wayland and active compositing do not establish a
usable desktop. The guest reports 849 MiB of zram swap use; container memory peaks
at 4,055,547,904 bytes with zero `max`, `oom` and `oom_kill` events and no container
swap. This run is a guest startup/rendering failure, not an HDB product failure
or proof that VM input is unsupported. The exact cause remains unresolved.

The source-grounded next step is a faithful replay of the maintained Fedora job's
installed-image/firmware configuration when resources permit its 4 GiB guest,
while preserving the agreed single-VM/two-CPU scope. The local Beta 1.3 live media,
BIOS machine and 2,304 MiB differ from that passing baseline. No further equivalent
boot, backend override, package update or host security change was attempted.
The staged Qt GeoServices repair is deferred until basic desktop capture and
input work, as requested. No frozen source, toolchain pin or Bridge task changes.

Run 18 ends with explicit TERM cleanup, engine exit 1 and result `canceled`;
its VM exits before the idle owned container is stopped. Full-session S2/S3,
F1–F10/platform, appearance/scale, actual Orca speech and gestures remain
UNVERIFIED. Previously completed app tests were not repeated.

Exact guest logs, captures, resource counters and the explicit cancellation result
were saved before scoped cleanup. The owned HDB service, single VM and transfer
container are stopped; runs 17 and 18's exit 1/canceled are intentional cleanup, not a
product failure. Full native desktop, appearance/scale, speech and RPM/DEB checks
remain UNVERIFIED. No host appearance, scale, reader, security or input source
changed, so none needed restoration. Gesture replay is never physical
pinch/momentum acceptance. The historical F1–F10/platform rows in #19 remain
incomplete; this tooling record adds no physical PASS.

## Local evidence

Workspace `/home/sheng/Documents/Codex/2026-10-09/task/`, under
`evidence/kde-external/`: `focused-final-4` (upstream, reduction, fixture),
`hdb-production-verified` (affected ordinary production validation),
`qt-gate-fixture-final`, `qt-gate-production-final`, `isolate-*`,
`openqa-vm-*`, signed-media verification and helper/build logs. Earlier failed
harness attempts remain separately named. Run 18 adds native/VNC captures,
QEMU topology/endpoint logs, guest journals, KWin support information, resource
counters and cancellation results; `upstream-fedora-*` preserves the passing
official example's actual configuration, input log and browser screenshot.
Original app-level evidence remains
under `evidence/acceptance-*-final`. Frozen UI/record worktrees, unrelated local
work and all oxpinyin lanes are preserved. Screenshots are not committed.
