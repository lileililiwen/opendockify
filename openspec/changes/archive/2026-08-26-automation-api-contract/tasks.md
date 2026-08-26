## 1. Tests First

- [x] 1.1 Contract test: served `/api/v1/automation/openapi.json` is valid OpenAPI 3.1, byte-identical to `docs/openapi.json`, and lists all six paths

## 2. Contract and Docs

- [x] 2.1 Author `docs/openapi.json` (paths, Idempotency-Key header, request/response schemas, structured error schema)
- [x] 2.2 Serve the committed document at `GET /api/v1/automation/openapi.json` (no auth)
- [x] 2.3 Prepend curl quickstart and append Troubleshooting to `docs/integrations.md`

## 3. Verify and Deliver

- [x] 3.1 Build zero-warning, run unit/architecture gates
- [x] 3.2 Archive and commit only related paths
