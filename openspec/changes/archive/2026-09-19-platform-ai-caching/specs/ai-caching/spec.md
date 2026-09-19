## ADDED Requirements

### Requirement: Local-first policy-gated AI

The system SHALL route polish through a policy gateway defaulting to local Ollama, with HTTPS-only remote endpoints, PII scrub, and per-day token budgets.

#### Scenario: PII scrubbed before send

- **WHEN** `Ai:ScrubPii=true` and polish input contains ID/phone patterns
- **THEN** the outbound LLM prompt contains redacted placeholders, mandatory values are re-substituted from `AiGuard`, and the scrubbed prompt is never logged

#### Scenario: Token budget enforced

- **WHEN** a user exceeds `Ai:MaxTokensPerDay`
- **THEN** the API returns `429` with reset time and no provider call is made

#### Scenario: Insecure remote rejected

- **WHEN** provider is remote `http:` without explicit loopback allowance
- **THEN** the polish call fails closed with a safe configuration error

### Requirement: Safe logs and render cache

The system SHALL redact AI logs to 500 chars and SHALL cache identical renders via HybridCache with safe keys.

#### Scenario: Log redaction

- **WHEN** AI usage is logged
- **THEN** the snippet is at most 500 chars with `\d{6,}` redacted and no endpoint key

#### Scenario: Identical render hits cache

- **WHEN** the same template definition plus normalized inputs render twice within TTL
- **THEN** the second render returns identical text via cache hit recorded with redaction-safe telemetry and no raw PII in metric tags
