# Worker API client

This app and the [web app](https://github.com/shenghaoc/hdb-resale-visualizer) read the same HDB Resale
Explorer Worker API (`https://hdb-resale-visualizer.shenghaoc.workers.dev/api/*`). The Worker owns the data
and where it is stored; neither app reads a database, a snapshot or a provider directly. The two apps stay
independent implementations of one product: they converge on what a buyer observes and on the API's
semantics, not on shared code. There is no snapshot hosting, no Qt-specific backend and no shared
frontend or domain framework.

## Workflow inventory and API mapping

| Workflow | Before (local corpus) | Now (Worker API) |
| --- | --- | --- |
| Start and load data | CSV import of the six bundled rows, `HDB_DATA_DIRECTORY` or `--snapshot-cache` | `GET /api/manifest` and `GET /api/block-summaries` (about 1.4 MB compressed), off the UI thread, with loading, error and Retry states |
| Town and flat-type choices | Distinct values of the imported rows | `manifest.filterOptions` (the web app's lists) |
| Town and flat-type filters | Imported rows of that town or type | Addresses in that town; addresses that sold that flat type |
| Price bounds | Each sale's price, then re-summarised | Each address's median: the selected flat type's when one is chosen (web budget semantics) |
| Registration window (12 or 24 months) | Sales in the window, then re-summarised | Addresses whose latest registration is in the window: the selected type's latest when one is chosen, which then requires that type's figures (web `startMonth` semantics) |
| Address list | Statistics of the matching sales, ordered by key | The API's statistics, for the selected flat type when it publishes them; lowest median first, API order among equal medians (web default order) |
| Map | Grid groups and pins of mapped addresses | Unchanged presentation. Every API address has coordinates |
| Selected address | Metrics, lease estimate, 15 newest matching sales, trend of matching sales, identity/coordinate evidence | `GET /api/details/{addressKey}`: the API's summary with interquartile range and nearest MRT; the 20 newest registrations of every flat type with their source remaining lease; the 24-month trend of the API's monthly medians; approximate block location and postal code |
| About | Import summary and diagnostics | The manifest's publication time, data window and counts |
| Snapshot download and activation | `--sync-snapshot`, `--snapshot-cache` | Removed |

## Where the API could not support the old workflow

Each gap was resolved by simplifying this app to the web app's semantics. No API capability was added.

- **Statistics of an arbitrary set of sales.** The API publishes each address's statistics; recomputing
  them for any price or month filter would mean downloading every transaction (about a million). Prices
  and windows therefore select addresses, as in the web app, and the figures shown are the API's.
- **Per-address identity and coordinate evidence.** The API's addresses are geocoded when they are
  published, and it publishes no match evidence. The details show the approximate block location and
  postal code; the About dialog shows the dataset's provenance.
- **Remaining lease from source observations.** The app shows the web app's estimate (what remains of a
  99-year lease from the commencement year, in the current calendar year), and each registration keeps
  the remaining lease recorded at its resale application.
- **Newest matching sales.** The details list the API's 20 newest registrations at the address, of every
  flat type, as the web app does.

## Remaining differences from the web app

These are deliberate platform choices, not different semantics:

- The default maximum median is S$1,000,000 (the web app starts without a maximum).
- This app offers the town, flat type, price and window filters only. The web app adds model, floor area,
  remaining lease, MRT distance, text search and affordability.
- Map grouping and markers are this app's own rendering.

## Parity

`tests/HdbResale.Tests/ProductCoreParityTests.cs` runs the web app's golden fixtures
(`tests/fixtures/web-parity`, copied with its source commit and hash) for every filter this app offers, and
fails if a scenario for an offered filter would be skipped. `AddressExplorerTests` checks filters, order and
selection against recorded production responses (`tests/fixtures/worker-api`).

## Native checks

The tests above run without Qt. These tools drive the real app; each needs a graphical session and a built
`HdbResale.App`, keeps text logs only and sends the API nothing but GET requests.

- **Buyer acceptance** (`tools/api_acceptance.py` with `ApiAcceptanceGate.qml`) runs sixteen steps over the
  recorded responses:
  - loading, then the four filters, including crossed price bounds and an empty result;
  - selection from a list row and from a map marker, and a selection hidden by a filter;
  - a refused detail request (the test server answers 503 once) and its Retry;
  - Reset, and intents queued while a model reset is in progress;
  - a burst of map movements.

  The tool's oracle derives every expected state from `tests/fixtures/worker-api` independently of the app's
  C#. That covers the web app's filter semantics and order, each list row and individual marker as displayed,
  and the summary. For the selected address it covers the figures, lease, location, registrations and the
  24-month trend. After each step the gate compares the window with this, including:
  - the plotted chart points;
  - which addresses the map counts as in view, by Qt's own projection;
  - the details pane returning to its top;
  - 4.5:1 text contrast.

  `--fault reorder|trend|no-503|skip-reentry` plants one defect and passes only if the gate fails at that
  defect's step.
- **Smoke** (`tools/api_native_smoke.py`). `recorded` and `production` load data, select an address and wait
  for the map and chart. `unreachable` checks that a refused API is reported without readiness. `high-zoom`
  checks the individual markers at zoom 15.
- **Linux package launch** (`tools/package/launch_check.py`) runs the relocated package with `--api recorded`
  (the default), `production` or `unreachable`. Packages bundle no data
  ([packaging](product-rc/licensing-packaging.md)).

## Slices

1. **Browse and inspect through the Worker API** (#8): the API client, the web app's filter semantics, the
   list, map and details on API data; the CSV-based native gates and the snapshot runtime path retired with
   the local data path.
2. **API-only packaging and native acceptance** (this change): the Linux package bundles no data and its
   launch check reads the recorded API, the production API or a refused port. A native buyer acceptance gate
   over the recorded API replaces the retired CSV gates.
3. **Retire the local-corpus research tooling**: decide on the offline research modes and their acquisition
   and normalisation tools. The window and the package never read them.
4. **Further web workflows, where they earn their place**: for example address search through
   `GET /api/suggest`, or comparable evidence through `POST /api/comparable-transactions`. Shortlists and any
   other private writes stay behind the Worker.
