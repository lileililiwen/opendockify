## Why

Integrators must hand-write clients from markdown examples, and the docs bury
the fastest path to a first successful finalize while omitting fixes for the
most common webhook failures — slowing every integration and inviting drift
between prose and behavior.

## What Changes

- Publish a machine-readable OpenAPI 3.1 document for `/api/v1/automation`, served at `GET /api/v1/automation/openapi.json` and committed at `docs/openapi.json`.
- Add a copy-paste curl quickstart (login → token → templates → finalize → status) to the top of the integration guide.
- Add a troubleshooting section covering signature failures, replay semantics, rate limits, and blocked deliveries.

## Capabilities

### Modified Capabilities

- `automation-integrations`: the versioned automation API additionally exposes a machine-readable contract.

## Non-goals

- No code-generated server types; no Swagger UI hosting; no changes to endpoint behavior.

## Impact

- Committed `docs/openapi.json` + a serving endpoint + a snapshot test keeping
  the file and route in lockstep; documentation-only additions otherwise.
