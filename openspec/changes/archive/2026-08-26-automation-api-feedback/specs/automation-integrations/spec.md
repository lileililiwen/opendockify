## ADDED Requirements

### Requirement: Machine-readable automation feedback

The automation API SHALL return structured, self-describing feedback on every
outcome: finalize success payloads SHALL include the operation id, authorization
and rate-limit failures SHALL use the same `{error:{code,message}}` shape as
validation failures, and all response timestamps SHALL be UTC ISO-8601 with an
explicit `Z` offset.

#### Scenario: Finalize exposes its operation

- **WHEN** a scoped client finalizes a document (fresh or replayed)
- **THEN** the response payload includes the `operationId` that `GET /operations/{id}` accepts

#### Scenario: Authorization failures are machine-readable

- **WHEN** a request lacks a valid service token or the token lacks the endpoint's scope
- **THEN** the API returns 401/403 with a structured error body (`token_invalid`, `forbidden_scope`) and performs no action

#### Scenario: Rate-limited clients get actionable copy

- **WHEN** an automation client exceeds the automation rate-limit policy
- **THEN** the API returns 429 with a structured body whose message describes automation throttling, not login throttling

#### Scenario: Timestamps are unambiguous UTC

- **WHEN** any automation response includes a timestamp
- **THEN** it is serialized as UTC ISO-8601 with an explicit `Z` offset
