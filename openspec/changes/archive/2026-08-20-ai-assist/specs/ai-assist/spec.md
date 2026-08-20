## ADDED Requirements

### Requirement: AI feature gate

The system SHALL enable or disable all AI functionality based on the
`Ai.Enabled` system setting; when disabled, AI endpoints SHALL return a
disabled response and SHALL NOT call any LLM.

#### Scenario: Disabled by default

- **WHEN** the system config has `Ai.Enabled=false`
- **THEN** every `/api/ai/*` call returns a disabled response and no external
  call is made

#### Scenario: Enabled after configuration

- **WHEN** an admin sets `Ai.Enabled=true` with an endpoint and key
- **THEN** AI endpoints accept requests

### Requirement: Config-driven LLM client

The system SHALL obtain the LLM endpoint, API key, and model from system
config (`Ai.Endpoint`, `Ai.ApiKey`, `Ai.Model`) and SHALL NOT contain
hardcoded keys. The client SHALL speak an OpenAI-compatible chat-completions
protocol so it works with OpenAI and Ollama.

#### Scenario: Endpoint from config

- **WHEN** an AI request is processed
- **THEN** the configured endpoint/model are used and the key is read from
  config at call time (never from source code)

#### Scenario: Ollama compatible

- **WHEN** `Ai.Endpoint` points at a local Ollama server exposing the
  OpenAI-compatible API
- **THEN** AI polish works without any OpenAI account

### Requirement: Clause polish

The system SHALL polish a user-drafted clause into standard contract language
given the template's field/constraint context, and SHALL NOT add amounts,
ID numbers, or clauses not selected/provided by the user.

#### Scenario: Polish a draft clause

- **WHEN** a user submits draft text plus template field context
- **THEN** the response is polished clause text consistent with the template's
  constraints

#### Scenario: No fabricated values

- **WHEN** the LLM response would contain an amount or ID number not present in
  the input
- **THEN** the system's guard rejects or strips the fabricated value before
  returning

### Requirement: Full-document polish preserves mandatory fields

The system SHALL polish the document's prose while preserving every mandatory
field value exactly; the response SHALL be re-substituted server-side so no
field value can change.

#### Scenario: Field values unchanged

- **WHEN** a document is polished with field values like `1234` and borrower
  `张三`
- **THEN** the returned text contains exactly those values (e.g. the uppercase
  amount `壹仟贰佰叁拾肆元整` and `张三`), and the guard verifies each
  placeholder-backed value is identical

#### Scenario: Unselected clause not introduced

- **WHEN** an optional clause was not selected
- **THEN** the polished document does not introduce its content

### Requirement: AI usage logging

The system SHALL log every AI invocation (user, action, timestamp,
success/failure, and redacted prompt/response snippets) to a persistent table.

#### Scenario: Log written on call

- **WHEN** an AI endpoint completes (success or failure)
- **THEN** an `AiUsageLog` row is created with the action and user

#### Scenario: Sensitive values redacted

- **WHEN** prompt/response snippets are logged
- **THEN** ID numbers and name-like values are redacted from the stored text

### Requirement: Best-effort failure handling

The system SHALL return the original (unpolished) text with a warning when the
LLM call fails, rather than failing the user's workflow.

#### Scenario: LLM unavailable

- **WHEN** the LLM endpoint errors or times out
- **THEN** the API returns the original text unchanged with a warning flag, and
  the failure is logged
