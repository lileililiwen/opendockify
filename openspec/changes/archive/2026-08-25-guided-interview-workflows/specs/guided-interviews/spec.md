## ADDED Requirements

### Requirement: Declarative interview definitions

The system SHALL allow a template to define ordered question steps, conditional visibility, and review labels using a bounded declarative schema; it MUST reject unknown field references, cycles, unreachable required fields, and executable content.

#### Scenario: Valid conditional flow

- **WHEN** an administrator saves an interview whose conditions reference fields in the same template
- **THEN** the definition is accepted and its deterministic start step is recorded

#### Scenario: Unsafe or invalid graph rejected

- **WHEN** a definition contains a cycle, unknown field, unreachable required field, script, or unsupported operator
- **THEN** the API returns `400 Bad Request` with path-specific errors and persists nothing

### Requirement: Server-authoritative interview progression

The system SHALL calculate visible steps and progress on the server from validated answers and SHALL validate each submitted answer against the template field rules.

#### Scenario: Branch skips irrelevant questions

- **WHEN** an answer makes a conditional step inapplicable
- **THEN** that step is omitted and any previously supplied answer for it is removed from the effective answer set

#### Scenario: Forged hidden answer rejected

- **WHEN** a client submits an answer for a field that is not visible on the current branch
- **THEN** the API returns `400 Bad Request` and does not advance the session

### Requirement: Private resumable interview sessions

The system SHALL persist authenticated interview sessions with template revision, current step, answers, and expiry; only the owner MAY read, update, complete, or delete a session.

#### Scenario: Owner resumes a session

- **WHEN** the owner reopens an unexpired session
- **THEN** the server returns the same effective answers and next visible step

#### Scenario: Foreign session hidden

- **WHEN** another user requests or mutates the session
- **THEN** the API returns `404 Not Found` and reveals no session metadata

#### Scenario: Template changed during session

- **WHEN** the referenced template revision no longer matches
- **THEN** completion is blocked with a conflict response and the stored answers remain exportable to the owner

### Requirement: Review and completion

The system SHALL show a review of effective answers before completion and SHALL generate only through the existing preview/finalization validation pipeline.

#### Scenario: Review before preview

- **WHEN** all visible required questions are valid
- **THEN** the owner receives a review grouped by step and may return to any visible answer

#### Scenario: Complete interview

- **WHEN** the owner confirms the reviewed answers
- **THEN** the effective answers are projected to the existing preview endpoint without bypassing generation validation

### Requirement: Flutter guided interview experience

The Flutter application SHALL render one logical step at a time, expose progress and back navigation, preserve recoverable state, and provide accessible validation and review controls.

#### Scenario: Interrupted mobile flow

- **WHEN** the app closes after a successful answer save
- **THEN** reopening offers to resume at the server-reported next step

