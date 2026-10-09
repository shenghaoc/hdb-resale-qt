# Tasks: Linux packaging, package CI and dependency automation

> Execution record. Phases follow the commits of the pull request. All items below were done and verified
> before this spec was written; the final section lists follow-ups that were deliberately not done here.

## Phase 1 — Audit and staging boundary

- [x] **T1.1** Compare the pinned Bridge 0.4.0-beta with upstream `dev` (targets, generated CMake, a real build).
  → `docs/packaging/qt-bridge-deployment-audit.md`. (R1.2–R1.4)
- [x] **T1.2** Rename `linux_rc.py` to `stage_linux.py`; stop bundling the corpus; take Bridge-deployed Qt files first;
  record suppliers, floors, `system-libraries.txt`. → `test_package_staging.py`. (R2.1, R3)
- [x] **T1.3** `package_common.py`: single version source, forbidden-content audit, exact manifest verification. (R2.3, R3.2)
- [x] **T1.4** SPDX licence texts into `LICENSES/`; drop the `--bridge-source` argument. (R3.5)

## Phase 2 — API-era package smoke

- [x] **T2.1** `launch_check.py` starts the recorded API server, launches via the user-facing launcher on real X11,
  clears overrides, verifies the manifest, library provenance and (strace) file access. → `test_package_launch.py`. (R5.3–R5.4, R5.6)

## Phase 3 — DEB and RPM

- [x] **T3.1** `packaging/` metadata, desktop entry, icon, AppStream, copyright; `system-dependencies.json`. (R4.1–R4.2)
- [x] **T3.2** `build_packages.py` (tar, deb via `dpkg-deb`, rpm via `rpmbuild`). → `test_package_formats.py`. (R2.2, R2.4)
- [x] **T3.3** `verify_installed.sh/.py`: clean-container install, ELF audit before tooling, mapping ownership,
  launch, negative control, uninstall. (R4.3, R5)

## Phase 4 — CI

- [x] **T4.1** `linux-package.yml` with scoped triggers, tests and native gates before packaging, Debian 13 and Fedora 43
  verification, reports-only artifact. → `test_ci_configuration.py`. (R6)

## Phase 5 — Dependabot

- [x] **T5.1** `.github/dependabot.yml` (NuGet + Actions, weekly, grouped, Bridge ignored) and schema validation workflow. (R7)

## Phase 6 — Documentation and spec

- [x] **T6.1** `docs/packaging/linux.md`, README, native-acceptance, licensing-packaging cross-references. (R8)
- [x] **T6.2** This spec.

## Follow-ups (not done here, none blocks this PR)

- [ ] **F1** When a *published* Bridge deploys QML imports and a relative RPATH, delete the QML scanner/RPATH steps
  (`supplier: qt` entries are the work list).
- [ ] **F2** `setDesktopFileName("io.github.shenghaoc.hdb-resale-qt")` so Wayland windows match the desktop entry
  (UI-layer change; resume with the UI work).
- [ ] **F3** Launch the packages under a headless Wayland compositor (weston/sway) to move Wayland from "staged" to "tested".
- [ ] **F4** Single source for the .NET SDK/runtime so the `dotnet-sdk` Dependabot ecosystem can be enabled safely.
- [ ] **F5** Corpus-free distribution blockers (source delivery, notices, signing): owner decisions, see
  `docs/product-rc/licensing-packaging.md`.
