# Native desktop UI improvement

This independent UI change starts at fetched `origin/main`
`4796d7dfa1b8889efd796ae6b18d281b96e49cfb`. The original checkout was clean;
development uses a new isolated `codex/native-desktop-ui-20261005` worktree.
No database, provider, migration, provisioning, credentials, benchmark or
visualizer work is included. Domain semantics, import sources, minimized
historical evidence, source pins and Bridge mutation/lifetime rules are unchanged.

## Observed problems and changes

The current baseline was launched before edits. In macOS dark mode the
hardcoded dark lease/trend explanation and light chart theme were difficult to
read; white attribution text appeared on a white attribution panel. Fixed
filter widths and a 1120-pixel minimum prevented narrow tiled use. Camera
diagnostics competed with ordinary result and selection information.

- Native controls inherit system fonts and palette. Heading sizes are relative;
  detail/row text wraps. The Qt Graphs theme follows the system scheme, its axes
  use the inherited font, and the observed-point color uses the link palette.
  The official white OneMap attribution panel gets explicit dark text/links.
- Filters reflow to six, three or two columns according to available width and
  font metrics. The minimum window is 640 × 600. A resizable SplitView shows map
  and results together on wide windows; narrow windows use Map and Addresses
  and details tabs. Filter and selection state stay in the existing C# model.
  When focused details become a narrow pane, the address tab remains visible.
- Matching/address/mapped/omission counts stay prominent. Groups count addresses;
  individual pins count matching transactions. Address match quality and
  coordinate approximation remain distinct in accessible names and evidence.
  The default is explicitly labelled a bundled development sample. Local imports
  show their registration count without asserting full coverage. Import counts,
  source-month semantics and caveats are available in Data and import; row/file
  diagnostics remain visible in the main window.
- DesktopActions owns reset, focus/navigation, selected-address return,
  zoom/recenter, About, Settings and Quit. Buttons and menus share these Actions.
  Zoom limits, missing selected coordinates, map readiness and modal dialogs
  disable the relevant commands. Preparing-map and map-error feedback is visible.
  Optional camera coordinates move to a persistent Settings checkbox.
- Arrow/plus/minus/Home map commands are local Keys handlers and only run while
  the map itself has focus, with no modifiers except Shift for the plus character.
  List and editor keys retain their
  existing purpose. Dialog Close/Escape restores the previously focused item.
  List rows expose a button role/press action as well as full quality labels.

Map pan/pinch/wheel, selection, hidden-selection clearing, FIFO/re-entry,
aggregation and projection remain in their existing implementations. The only
marker presentation change is font-relative hit/display size. OneMap's direct
tile URL, exact attribution wording/logo, cache directory, zoom range and
provider parameters are unchanged; there is no tile proxy.

## Host conventions and official references

Precedence is accessibility and explicit system preferences, host OS/desktop,
shared usability, then application preferences. Runtime `QtQuick.Controls`
imports retain explicit Qt style overrides. No foreign platform plugin,
Kirigami, `org.kde.desktop`, copied toolkit skin, new toolkit or runtime AI is added.

| Host/reference | Application choice and limit |
| --- | --- |
| [Windows design](https://learn.microsoft.com/en-us/windows/apps/design/) and [commanding](https://learn.microsoft.com/en-us/windows/apps/design/basics/commanding-basics) | Frequent filters/navigation remain on canvas, secondary information uses a dialog/menu. The Windows resource selector chooses supported Qt FluentWinUI3; explicit style overrides still win. Native frame/lifecycle stay with Qt/Windows. No Windows build or physical acceptance is claimed. |
| [Apple macOS HIG](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos) | Stock macOS controls, Cocoa frame and the existing bundle identity remain. A macOS-only Qt platform menu adapter assigns explicit AboutRole, PreferencesRole and QuitRole. Native app menu includes system Hide/Services. Dialogs use existing Qt Quick Controls with standard Close roles; they are not claimed to be AppKit sheets. |
| [KDE HIG](https://develop.kde.org/hig/) and [layout/navigation](https://develop.kde.org/hig/layout_and_nav/) | Resizable panes, narrow tiling, system font/palette and consistent contextual commands. Portable application identity matches the existing bundle ID. Qt's Linux host default and any installed platform theme remain responsible for appearance. No new KDE dependency or unverified desktop-entry installation is claimed. |
| [GNOME HIG](https://developer.gnome.org/hig/) and [principles](https://developer.gnome.org/hig/principles.html) | Focused adaptive panes and secondary detail disclosure. GNOME is a distinct convention, not KDE. This remains a Qt app; GTK/libadwaita header bars and complete portal integration are not promised. |
| [HarmonyOS PC adaptation](https://developer.huawei.com/consumer/en/doc/design-guides/2in1-0000001777531700), [window framework](https://developer.huawei.com/consumer/en/doc/design-guides/window-0000002321868010) and [multi-window interaction](https://developer.huawei.com/consumer/en/doc/design-guides/system-features-multi-window-interaction-0000001795392917) | Reference for retained task state, native window controls and adaptive columns/system fonts. No HarmonyOS port or toolkit is introduced. |
| Qt 6.12 [styles](https://doc.qt.io/qt-6.12/qtquickcontrols-styles.html), [focus](https://doc.qt.io/qt-6.12/qtquickcontrols-focus.html), [MenuBar](https://doc.qt.io/qt-6.12/qml-qtquick-controls-menubar.html), [platform roles](https://doc.qt.io/qt-6.12/qml-qt-labs-platform-menuitem.html), [Settings](https://doc.qt.io/qt-6.12/qml-qtcore-settings.html) | Match the actual installed Qt version. The native menu adapter is instantiated only on macOS: no Linux QWidget fallback requirement. Qt.labs.platform is an existing Qt API with a future compatibility caveat. Settings use a platform-safe file URL under the existing local application-data convention. |

RowPlay was inspected read-only: ADR 0015, design-system/native-integration
notes, macOS verification, relevant native UI history and the local memory
pointer to PR145 verification. Its native style/palette, role-menu and focused
keyboard patterns informed this change; current HDB constraints take precedence.
No RowPlay code, assets or Bridge workaround was copied. HDB has no root
AGENTS.md or repository .agents skill overlays at this checkpoint.

## Verification on this Mac

macOS 27.0.1 (26A434), arm64/Apple M5, Cocoa/Metal. Verified installed versions:
.NET SDK 10.0.401, runtime 10.0.12, Qt 6.12.0, macOS Bridge 0.4.0.22-beta.
Linux Bridge remains pinned to 0.4.0-beta. Toolchain versions were not upgraded.

| Check | Result |
| --- | --- |
| Normal NuGet restore/audit; root Debug and Release build | Pass, zero MSBuild warnings/errors after normal restore refresh. Initial NU1900 network warnings were resolved without suppressing audit. Existing CMake private-Qt/optional-module notices remain, as on the base. |
| C# tests | 191 passed, zero failed/skipped in each configuration. |
| Offline Python tests | 40 passed. |
| Existing canonical and strict original 10,000-row native matrix | 12 positive runs pass in Debug/Release: canonical, classic, extended, reentrant, presentation, buyer. 22 deliberate failures are detected with clean managed teardown. Existing state/process budgets remain unchanged. |
| Focused DesktopUiGate | Debug/Release native-window events check 640 × 600, 1000 × 700, 1360 × 900 and 150% font at 640 × 760; long actual row wrapping; list keyboard selection; map zoom; editor/combo/list isolation; modal command disabling; Close/Escape focus restoration; reset, Settings and retained selection. Both negative controls detect failure/clean teardown. |
| Original full-corpus startup | Native production full-view readiness and clean teardown pass in Debug/Release using the already available hash-verified original public snapshot. This is not expanded normalized input and is not a cold/network or universal performance claim. |
| Visible native desktop input | Before/after Release screenshots and actual list Down/Enter, native About/Preferences menu, Command-comma, address-focus shortcut, settings checkbox, Quit and pan observed through supported desktop tools. Exact narrow sizes are set by the opt-in test harness and then inspected visibly. Synthetic input is not a human hardware wheel/pinch or VoiceOver acceptance claim. |
| Expanded normalized profile | Blocked equally on base and branch: `/tmp/hdb-m11-expanded/manifest.json` absent. The inherited 27 pinned ACRA snapshots and ten positive/twenty negative runs remain unavailable. Prior acquisition HTTP403 is documented; no reacquisition or ingestion bypass was attempted. |
| Windows/KDE/GNOME/HarmonyOS | No execution or physical acceptance available here; platform choices are based on official guidance and Qt APIs. |

The original 10,000-row local fixture matches its recorded derived hashes;
the original full fixture matches the committed scale manifest. No full or
private snapshot, raw historical OneMap export, generated package or new asset
is included in the diff. The generic all-ref publication audit is rerun for the
candidate commit; this does not claim a new private-reference rescan.

### Reproduction

Use the existing native toolchain setup and a real desktop. Run root Debug and
Release builds/tests, the existing native/scale/presentation/buyer runners, and:

```sh
python3 tools/desktop_ui_gate.py --executable /absolute/native/executable --log /tmp/desktop-ui.log
python3 tools/desktop_ui_gate.py --negative --executable /absolute/native/executable --log /tmp/desktop-ui-negative.log
```

The new gate has a five-second state deadline and thirty-second bounded process
teardown. `HDB_DESKTOP_UI_GATE=1 HDB_DESKTOP_UI_FAULT=inspect-narrow` holds a real
640 × 600 window for visual inspection and deliberately emits no pass. Set
`HDB_UI_SETTINGS_FILE` to an isolated absolute path during manual checks.

Required expanded checks still block readiness, so the PR remains draft.
Development staging is not a redistributable package: existing relocated-load,
closure/signing/licensing work remains outside this UI PR. No release, tag,
deployment or merge is performed. Screenshots and detailed evidence are saved
separately in Library and linked from the PR.
