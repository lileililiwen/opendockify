## ADDED Requirements

### Requirement: Standard edge pipeline

The system SHALL serve correlation IDs, sanitized RFC9457 errors with `code`, security headers, deny-by-default CORS, and version negotiation on all API responses.

#### Scenario: Correlation echoed

- **WHEN** a client calls any `/api/*` endpoint with or without `X-Correlation-Id`
- **THEN** the response carries `X-Correlation-Id` and server logs include it with no secrets

#### Scenario: Sanitized errors

- **WHEN** an unhandled exception occurs
- **THEN** the API returns `500` with a generic safe body plus stable `code`, and no stack trace, SQL, or secret

#### Scenario: CORS deny by default

- **WHEN** a browser preflights from an unlisted origin
- **THEN** the API omits `Access-Control-Allow-Origin` and the call fails closed

### Requirement: Proxy-aware security posture

The system SHALL honor forwarded headers behind trusted proxies, enforce secret separation, and require HTTPS for remote AI endpoints.

#### Scenario: Forwarded IP rate key

- **WHEN** the app runs behind a configured trusted proxy sending `X-Forwarded-For`
- **THEN** rate limiting keys on the client IP from the forwarded header, not the proxy IP

#### Scenario: Secret separation enforced

- **WHEN** startup sees `Sharing:HashKey` missing or equal to `Jwt:Secret`
- **THEN** startup fails with a clear secret-free error

#### Scenario: Insecure AI endpoint rejected

- **WHEN** `Ai:Endpoint` is remote `http:` without loopback + explicit flag
- **THEN** startup or AI calls fail closed directing the deployer to HTTPS or local Ollama
