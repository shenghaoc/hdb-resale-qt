# PR #8 review fixes — Fedora, 2026-10-08

All three findings on `83b26addab167671607e56289a52a025f4553595` were reproduced or
confirmed in the existing Worker API branch. Work used a separate clone; the
original `main` checkout and `ui/07-accessibility` worktree were untouched.
No checkout AGENTS.md, .agents/skills, or local Codex memory_summary.md was found.

- Individual map rows now use ordinal key order before diffing. The recorded
  Bedok regression at zoom 15 and 16 proves the old price-ordered projection
  throws, checks transitions both ways, and preserves the address list order.
- A failed selected-address request exposes **Retry registrations**. The same
  production detail state is tested with a 503 followed by successful retry,
  20 registrations and an observed trend, without changing selection or filters.
  Request generations reject old successes and failures across A → B → A.
- Parsed manifest endpoints must be ordered. Tests cover equal months, an
  ordered year boundary, a reversed year boundary and reversed same-year months.

## Commands and results

The host is Fedora 45 KDE prerelease, with .NET SDK 10.0.401, Qt 6.12.0 and the
repository's Linux Bridge pin. These are runtime paths discovered on this host:

```sh
export DOTNET_ROOT=/home/sheng/.local/share/hdb-qt-toolchain/dotnet
export QtDir=/home/sheng/Qt/6.12.0/gcc_64
export PATH="$DOTNET_ROOT:/home/sheng/.local/share/hdb-qt-toolchain/venv/lib/python3.14/site-packages/cmake/data/bin:/home/sheng/.local/share/hdb-qt-toolchain/venv/bin:$PATH"
dotnet build -c Debug -m:1
dotnet test -c Debug --no-build -m:1
dotnet build -c Release -m:1
dotnet test -c Release --no-build -m:1
python3 -m unittest discover -s tools -p 'test_*.py' -v
```

Both builds: zero warnings/errors. Both C# suites: **260 passed, zero skipped**.
Python: **37 passed**. Initial attempts failed because the system dotnet lacked
the pinned SDK, the venv's CMake launcher used an incompatible Python, and the
sandbox denied test IPC/NuGet. The commands above use the installed SDK and real
CMake binary; builds/tests ran with approved IPC/network access. No checks or
package audits were disabled.

```sh
export QT_QPA_PLATFORM=xcb
exe="$PWD/src/HdbResale.App/bin/Release/net10.0/HdbResale.App"
for mode in recorded production unreachable high-zoom; do
  python3 tools/api_native_smoke.py --executable "$exe" --mode "$mode" \
    --log "/tmp/pr8-$mode.log" || exit
done
# Also ran high-zoom against the Debug native executable.
```

The real native Qt window ran on the existing XWayland display `:0`, with no
offscreen platform. Each process exited zero and the harness required positive
markers, rejecting timeout/failure markers and QML/runtime exceptions:

| Path | Observed result |
| --- | --- |
| Recorded API, Release | `HDB_PACKAGE_SHELL`, `DATA`, `MAP_READY`, `CHART_READY` |
| Production API, Release | Same four readiness markers; GET requests only |
| Unreachable API, Release | `HDB_API_UNREACHABLE_PASS Could not load addresses. The HDB Resale API could not be reached.` |
| High zoom, Debug and Release | `HDB_API_HIGH_ZOOM_PASS zoom=15`; four actual MapQuickItem delegates, four list addresses, zero clusters |

The high-zoom gate uses recorded Bedok responses and checks exact marker keys:
`bedok-115-bedok-nth-rd`, `bedok-747a-bedok-reservoir-cres`,
`bedok-748a-bedok-reservoir-cres`, `bedok-748b-bedok-reservoir-cres`.
The official logo must also be ready; attribution and map provider are unchanged.

Native screen/input APIs were unavailable in this task (the inventory exposed
no native apps). This is a native automated state/delegate check, not a physical
click or pixel inspection. The user's Chrome was not used. Evidence is text
only: no licensed map imagery, snapshots or datasets were published. No
deployment, database mutation, cutover or merge was performed.
