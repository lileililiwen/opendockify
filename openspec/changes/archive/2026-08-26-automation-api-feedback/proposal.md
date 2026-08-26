## Why

The automation API's feedback layer is inconsistent: finalize responses omit the
operation id clients need for status checks, authorization failures return empty
bodies that break the structured-error contract, rate-limited automation clients
receive login-themed copy, and timestamp formats diverge between endpoints —
forcing integrators to guess instead of program against stable behavior.

## What Changes

- Include the operation id in finalize (fresh and replayed) success payloads so status checks are self-service.
- Return structured, machine-readable JSON bodies for automation 401/403 outcomes instead of empty responses.
- Give the automation rate-limit policy its own rejection copy instead of reusing the login message.
- Normalize all automation response timestamps to UTC ISO-8601 with an explicit Z offset.

## Capabilities

### Modified Capabilities

- `automation-integrations`: Versioned automation API feedback contract tightened.

## Non-goals

- No new endpoints, scopes, or UI; no changes to idempotency semantics or webhook delivery.

## Impact

- `OpenDockify.Integrations` payload/view records, `OpenDockify.Api` auth challenge,
  authorization result handling, rate-limiter rejection copy, contract snapshot tests,
  and docs examples.
