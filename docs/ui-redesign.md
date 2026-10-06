# UI/UX redesign: audit, principles and PR stack

Evidence screenshots contain OneMap tiles and are therefore kept out of the
repository (local `~/hdb-ui-evidence/<pr>/`).

Status: living document for the stacked redesign that follows the native
foundation in PR #1. Evidence screenshots were produced with the opt-in
`HDB_SCREENSHOT_DIR` gate (`src/HdbResale.App/ScreenshotGate.qml`) from the
bundled six-row development sample. Layout iteration uses Qt's `offscreen`
platform; native checks use the real KDE Plasma/Wayland session.

## Audit of the PR #1 baseline

**Primary task.** Find where resale transactions are, judge typical prices
there, narrow by a few criteria, and inspect one address with an honest sense of
how reliable its location is.

| Area | Finding | Consequence |
| --- | --- | --- |
| Composition | Everything is one vertical stack: title, data-mode caption, filter grid, summary, tabs, then content, then a licence line. Each band has equal weight. | No dominant element; the map, the actual subject, starts a third of the way down the window. |
| Breakpoint | Compact mode begins below `70 × text height` ≈ 1190 px. At an ordinary 1100 × 760 window the list and details are hidden behind a tab. | A common laptop window shows a map-only app; selecting a result shows nothing visible. |
| Filters | Six controls with labels above, wrapping to 2–3 columns, plus a Reset button that floats at the bottom of the grid. | Filters consume 25–35 % of the height and the active state is not summarised near the results. |
| Hierarchy | Headings are bold body text; secondary text is identical to primary; key values (price, count) are not emphasised; “Data and import…” sits alone at the top right. | Flat, form-like, “developer tool” appearance. |
| Side pane | List, a hairline, heading, status, button and details share one column; the list height is fixed by a text-height multiple. | Cramped list, detail text is a single paragraph block of preformatted strings. |
| Data quality | “Address evidence” prints internal strings (`Identity: …`, `Coordinates: …`). | Location uncertainty is accurate but not legible to a buyer. |
| Map overlays | Toolbar uses text buttons, summary and status labels have square window-coloured backgrounds, markers use three hard-coded colours with no relation to the palette, attribution is a separate white strip. | Overlays look unrelated to one another and to the platform. |
| Keyboard/a11y | Good after PR #1 (Actions, focus restore, accessible names). Remaining: focus rings are hand-drawn rectangles, list focus/selection are not distinguishable without colour, hit targets of map buttons are text-height. | Addressed in the dedicated accessibility PR. |

## Principles used to resolve conflicting guidance

1. Accessibility and explicit system preferences win (system font, palette,
   text scale, reduced motion).
2. Then the host desktop's convention (Windows/Fluent, macOS HIG, KDE HIG,
   GNOME HIG). The *information architecture and commands are shared*; only
   presentation differs, via Qt styles rather than separate front ends.
3. Then shared desktop principles: one dominant task surface, persistent
   state visible, secondary content on demand, density suited to pointer +
   keyboard, resizable panes (also the HarmonyOS PC window-adaptation guidance).
4. Then product identity — kept deliberately small: typographic roles, a
   spacing rhythm and map-overlay treatment.

Official references are listed in [native-ui.md](native-ui.md). Those pages
were read for PR #1; this redesign applies their shared advice (toolbar for
frequent commands, status at the window edge, resizable panes, secondary
information in dialogs/inspectors, no custom reproduction of platform widgets).

## Target composition

```
┌ menu bar (platform-native) ───────────────────────────────────────────┐
│ Filter toolbar: Town · Flat type · Price range · Registered · Reset   │
├───────────────────────────────────────────────┬───────────────────────┤
│                                               │ Results / details     │
│   MAP (primary spatial surface)               │ (inspector pane,      │
│   overlays: zoom cluster, coverage badge      │  resizable)           │
├───────────────────────────────────────────────┴───────────────────────┤
│ Status bar: counts · data mode · attribution/licence                  │
└───────────────────────────────────────────────────────────────────────┘
```

* The window title already names the app; the redundant in-window title is gone.
* Data provenance (“Bundled development sample…”) moves to the status bar next
  to the counts, with the Data dialog one click away.
* The map is always visible at desktop sizes. Below ~50 text-heights wide
  the inspector becomes a second tab; nothing is removed.
* The breakpoint derives from text metrics (desktop justification: a usable map
  plus a usable inspector ≈ 22 + 24 text heights plus margins), not from web
  device widths.

## Design vocabulary (`Theme.qml`)

Kept small and derived from the active Qt style/palette:

* spacing rhythm in multiples of a quarter text-height (`xs`, `s`, `m`, `l`);
* typography roles by scaling the platform font: `title`, `body`, `caption`;
* semantic colours from `palette`: text, secondary text (text/background mix),
  separator, panel (`base`), chrome (`window`), accent (`highlight`);
* map overlays: translucent `window` fill, 1 px separator outline, one corner
  radius; the OneMap attribution remains an opaque light panel with dark text.

## PR stack

| # | Branch | Purpose |
| - | ------ | ------- |
| 1 | `codex/native-desktop-ui-20261005` (PR #1) | Native commands, menus, Settings, focus, adaptive scaffold |
| 2 | `ui/02-shell` | Design vocabulary, application shell, status bar, breakpoint, screenshot QA gate |
| 3 | filters | Filter toolbar, active-state summary, reset, progressive disclosure |
| 4 | results | Result rows hierarchy, selection/focus, empty state |
| 5 | details | Structured details model, location-confidence language, back navigation |
| 6 | map | Markers, selected state, controls, overlays, loading/error states |
| 7 | a11y | Whole-app keyboard, screen-reader, text-scale, contrast, non-colour state |
| 8 | platform | Isolated macOS/Windows/KDE/GNOME refinements after native runs |

Boundaries may move after each visual review; PR 5 requires adding structured
properties to the C# model (today details are preformatted strings), which is
presentation-only and keeps matching and corpus semantics unchanged.

## Skills and references actually applied

No Qt, QML, product-design, accessibility or screenshot-audit skill is installed in
this agent environment (installed: document/office skills, `engineering:*`
review/testing/architecture skills, `run`, `artifact-design`, browser/computer-use
skills). That gap is recorded rather than filled with an invented skill; Qt 6.12
documentation and the official HIGs were used instead.

| Skill / source | Used for | Resulting decision |
| --- | --- | --- |
| `run` | Launching and driving the real app, looking at the saved window captures | Real KDE/Wayland captures are taken from the running app (`ui_screenshots.py --mode native`); blank frames count as failure |
| `artifact-design` | Publishing review images as a themed, reviewer-readable page | Evidence page built from redacted/synthetic images only |
| Qt 6.12 [SplitView](https://doc.qt.io/qt-6.12/qml-qtquick-controls-splitview.html) | Handle behaviour | “Handles should be purely visual and not handle events”; a thin handle needs `containmentMask` for hit area. The custom 1 px handle was removed in favour of the style's native handle |
| Qt 6.12 [QStyleHints](https://doc.qt.io/qt-6.12/qstylehints.html) | Dark appearance without touching the desktop | `colorScheme` is writable since Qt 6.8, “not supported on all platforms”. Verified per run, not assumed (below) |
| Qt 6.12 [High-DPI](https://doc.qt.io/qt-6.12/highdpi.html) | Scale testing | `QT_SCALE_FACTOR` multiplies the native ratio; the effective ratio is recorded in each manifest |
| [KDE HIG layout](https://develop.kde.org/hig/layout_and_nav/) | Shell composition | Toolbar above content, status bar below it, contextual/preview area trailing the content, adapt fluidly to narrow windows |
| [Apple HIG split views](https://developer.apple.com/design/human-interface-guidelines/split-views) | Divider and inspector | Inspector trails the content; dividers need hover feedback and adequate target (satisfied by the native handle) |

The GNOME sidebar page and the Windows/HarmonyOS pages were not re-read for this
layer; PR #1's references stand for them.

## Evidence method

* `tools/ui_screenshots.py` runs the in-app `ScreenshotGate` and fails on: capture
  not starting, a grab callback timing out, an image not saving, a state's
  preconditions (selection, filters, pane, dialog, font, window size, colour
  scheme) not met within a bounded wait, or a missing/empty/undecodable/uniform
  image. Every scenario restores the baseline font first and verifies it.
* Generation is not acceptance: images are inspected by a person.
* `--synthetic-basemap` serves neutral tiles from `tools/synthetic_basemap.py`
  (honoured only together with `HDB_SCREENSHOT_DIR`) and marks the map
  “SYNTHETIC BASEMAP · UI EVIDENCE ONLY”. These images are shareable. Captures
  with OneMap tiles stay local.
* Each manifest records head SHA, working-tree state, dataset, logical window
  size, effective device-pixel ratio, platform plugin, style, requested and
  effective colour scheme, and whether the image is a native window or a grab.
* Dark appearance: `Qt.styleHints.colorScheme = Qt.Dark` takes effect on the
  Wayland platform plugin (asynchronously; the gate waits for it) and changes the
  effective palette to Qt's generic dark palette. It does **not** demonstrate
  Plasma's Breeze Dark, nor following a real system switch. The offscreen plugin
  ignores the override.
* Scale: `QT_SCALE_FACTOR` is process-local. On this Wayland session it
  multiplies a native ratio of 1.

## Verification limits (honest status)

* Native runtime here: KDE Plasma on Wayland (Fedora), Qt 6.12.0, Bridge
  0.4.0-beta. macOS, Windows and GNOME were **not** run in this environment.
* No synthetic input device is available on this Wayland session, so pointer
  and keyboard interaction beyond the in-app gates is not driven externally.
* Dark-appearance and high-DPI captures require changing the user's system
  settings and are only claimed when actually captured.
