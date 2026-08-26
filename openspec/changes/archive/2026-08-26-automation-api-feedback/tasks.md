## 1. Tests First

- [x] 1.1 Update success/error contract snapshots for `operationId` and add timestamp-format assertions
- [x] 1.2 Add structured 401/403 body tests for the service-token scheme and scope policy

## 2. Feedback Implementation

- [x] 2.1 Add `operationId` to finalize payloads (fresh + replay) and surface it in `AutomationFinalizeResult`
- [x] 2.2 Write structured 401 (`token_invalid`) challenge bodies in `ServiceTokenHandler`
- [x] 2.3 Intercept forbidden automation outcomes with a structured 403 (`forbidden_scope`) result handler
- [x] 2.4 Split rate-limiter rejection copy per surface and normalize operation-view timestamps to UTC `Z`

## 3. Verify and Deliver

- [x] 3.1 Build zero-warning, run unit/architecture gates, refresh docs examples
- [x] 3.2 Archive and commit only related paths
