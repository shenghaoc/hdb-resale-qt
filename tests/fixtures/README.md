# Test fixtures

## `worker-api/`

Responses recorded from the production HDB Resale Explorer Worker API
(`https://hdb-resale-visualizer.shenghaoc.workers.dev/api/*`) on 2026-10-08: the manifest, eleven
address summaries from `/api/block-summaries` (in the API's own order) and each address's
`/api/details/{addressKey}` document. They are public data under the Singapore Open Data Licence, used
to test the API client offline. Re-record them when the API contract changes.

## `web-parity/product-core-golden.json`

A copy of `tests/fixtures/platform-parity/product-core-golden.json` from
[hdb-resale-visualizer](https://github.com/shenghaoc/hdb-resale-visualizer) at commit `a2afa050b`
(SHA-256 `fc46ae383f6aa67979da0e3be7143d48fd9b7f4ef228bf2e5d66167fb6000964`). It is the web app's cross-language contract for buyer logic;
`ProductCoreParityTests` requires this app to reproduce every scenario for the filters it offers. When
the web app changes the fixture, copy the new version here and update the commit and hash.
